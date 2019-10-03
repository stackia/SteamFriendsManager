using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using GalaSoft.MvvmLight.Messaging;
using GalaSoft.MvvmLight.Threading;
using SteamKit2;
using SteamKit2.Discovery;

namespace SteamFriendsManager.Service
{
    public class SteamClientService : IDisposable
    {
        private readonly ApplicationSettingsService _applicationSettingsService;
        private readonly SteamClient _steamClient = new SteamClient();
        private readonly SteamFriends _steamFriends;

        private readonly SteamUser _steamUser;

        //private TaskCompletionSource<SteamUser.LoggedOffCallback> _logoutTaskCompletionSource;
        private TaskCompletionSource<SteamFriends.FriendAddedCallback> _addFriendTaskCompletionSource;
        private CancellationTokenSource _callbackHandlerCancellationTokenSource;
        private TaskCompletionSource<SteamClient.ConnectedCallback> _connectTaskCompletionSource;
        private TaskCompletionSource<SteamClient.DisconnectedCallback> _disconnectTaskCompletionSource;
        private bool _disposed;
        private SteamUser.LogOnDetails _lastLoginDetails;
        private TaskCompletionSource<SteamUser.LoggedOnCallback> _loginTaskCompletionSource;
        private int _retryCount;
        private CancellationTokenSource _retryCountResetCancellationTokenSource;
        private TaskCompletionSource<SteamUser.AccountInfoCallback> _setPersonaNameTaskCompletionSource;
        private TaskCompletionSource<SteamFriends.PersonaStateCallback> _setPersonaStateTaskCompletionSource;

        public SteamClientService(ApplicationSettingsService applicationSettingsService)
        {
            _applicationSettingsService = applicationSettingsService;

            _steamUser = _steamClient.GetHandler<SteamUser>();
            _steamFriends = _steamClient.GetHandler<SteamFriends>();
        }

        public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromMilliseconds(10000);
        public bool ReconnectOnDisconnected { get; set; }

        public bool IsConnected => _steamClient.IsConnected;

        public ObservableCollection<Friend> Friends { get; } = new ObservableCollection<Friend>();

        public string PersonaName => _steamFriends.GetPersonaName();

        public EPersonaState PersonaState => _steamFriends.GetPersonaState();

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                _callbackHandlerCancellationTokenSource.Dispose();
                _retryCountResetCancellationTokenSource.Dispose();
            }

            _disposed = true;
        }

        public void Start()
        {
            _callbackHandlerCancellationTokenSource = new CancellationTokenSource();
            Task.Run(() =>
            {
                var manager = new CallbackManager(_steamClient);
                manager.Subscribe<SteamClient.ConnectedCallback>(cb =>
                {
                    if (_connectTaskCompletionSource != null && !_connectTaskCompletionSource.Task.IsCompleted)
                        _connectTaskCompletionSource.TrySetResult(cb);

                    // If the connection doesn't disconnect in 3 seconds, reset the retry count.
                    _retryCountResetCancellationTokenSource = new CancellationTokenSource();
                    Task.Delay(3000, _retryCountResetCancellationTokenSource.Token).ContinueWith(task =>
                    {
                        if (!task.IsCanceled)
                            _retryCount = 0;
                    });
                });

                manager.Subscribe<SteamClient.DisconnectedCallback>(cb =>
                {
                    if (_disconnectTaskCompletionSource != null && !_disconnectTaskCompletionSource.Task.IsCompleted)
                        _disconnectTaskCompletionSource.TrySetResult(cb);
                    else if (ReconnectOnDisconnected)
                        TryReconnect();
                });

                manager.Subscribe<SteamUser.LoggedOnCallback>(cb =>
                {
                    if (_loginTaskCompletionSource != null && !_loginTaskCompletionSource.Task.IsCompleted)
                        _loginTaskCompletionSource.TrySetResult(cb);
                });

                //manager.Subscribe<SteamUser.LoggedOffCallback>(cb =>
                //{
                //    if (_logoutTaskCompletionSource != null && !_logoutTaskCompletionSource.Task.IsCompleted)
                //        _logoutTaskCompletionSource.TrySetResult(cb);
                //});

                manager.Subscribe<SteamUser.UpdateMachineAuthCallback>(async cb =>
                {
                    if (_applicationSettingsService.Settings.SentryHashStore == null)
                        _applicationSettingsService.Settings.SentryHashStore = new Dictionary<string, byte[]>();

                    var sentryHash = CryptoHelper.SHAHash(cb.Data);
                    _applicationSettingsService.Settings.SentryHashStore[_lastLoginDetails.Username] = sentryHash;
                    await _applicationSettingsService.SaveAsync();

                    _steamUser.SendMachineAuthResponse(new SteamUser.MachineAuthDetails
                    {
                        BytesWritten = cb.BytesToWrite,
                        FileName = cb.FileName,
                        FileSize = cb.Data.Length,
                        JobID = cb.JobID,
                        LastError = 0,
                        Offset = cb.Offset,
                        OneTimePassword = cb.OneTimePassword,
                        Result = EResult.OK,
                        SentryFileHash = sentryHash
                    });
                });

                manager.Subscribe<SteamFriends.PersonaStateCallback>(cb =>
                {
                    if (cb.FriendID == _steamUser.SteamID)
                    {
                        if (_setPersonaStateTaskCompletionSource != null &&
                            !_setPersonaStateTaskCompletionSource.Task.IsCompleted)
                            _setPersonaStateTaskCompletionSource.TrySetResult(cb);

                        return;
                    }

                    var query = from f in Friends where f.SteamId.Equals(cb.FriendID) select f;
                    var friends = query as Friend[] ?? query.ToArray();

                    if (!friends.Any())
                        return;

                    var friend = friends.First();
                    friend.OnStateChanged();
                });

                manager.Subscribe<SteamClient.CMListCallback>(async cb =>
                {
                    if (_applicationSettingsService.Settings.PreferredCmServers == null)
                        _applicationSettingsService.Settings.PreferredCmServers = new List<string>();
                    _applicationSettingsService.Settings.PreferredCmServers.Clear();
                    _applicationSettingsService.Settings.PreferredCmServers.AddRange(
                        (from sv in cb.Servers select sv.EndPoint.ToString()).Take(8));
                    await _applicationSettingsService.SaveAsync();
                });

                manager.Subscribe<SteamUser.AccountInfoCallback>(cb =>
                {
                    if (_setPersonaNameTaskCompletionSource != null &&
                        !_setPersonaNameTaskCompletionSource.Task.IsCompleted)
                        _setPersonaNameTaskCompletionSource.TrySetResult(cb);
                    Messenger.Default.Send(new PersonaNameChangedMessage());
                });

                manager.Subscribe<SteamFriends.FriendsListCallback>(cb =>
                {
                    DispatcherHelper.CheckBeginInvokeOnUI(() =>
                    {
                        if (!cb.Incremental)
                            Friends.Clear();

                        foreach (var friend in from friendRaw in cb.FriendList
                            where friendRaw.SteamID.IsIndividualAccount
                            select new Friend(friendRaw.SteamID, _steamFriends))
                            if (Friends.Contains(friend))
                            {
                                if (friend.Relationship == EFriendRelationship.None)
                                    Friends.Remove(friend);
                            }
                            else
                            {
                                Friends.Add(friend);
                            }
                    });
                });

                manager.Subscribe<SteamFriends.FriendAddedCallback>(cb =>
                {
                    if (_addFriendTaskCompletionSource != null && !_addFriendTaskCompletionSource.Task.IsCompleted)
                        _addFriendTaskCompletionSource.TrySetResult(cb);
                });

                while (!_callbackHandlerCancellationTokenSource.IsCancellationRequested)
                    manager.RunWaitCallbacks(TimeSpan.FromMilliseconds(50));
            }, _callbackHandlerCancellationTokenSource.Token);
        }

        private void TryReconnect()
        {
            if (_retryCount < 3)
            {
                // Unexpectedly disconnect, try reconnect.
                if (_retryCountResetCancellationTokenSource != null &&
                    !_retryCountResetCancellationTokenSource.IsCancellationRequested)
                    _retryCountResetCancellationTokenSource.Cancel();
                Task.Run(async () =>
                {
                    try
                    {
                        await ConnectAsync();
                        if (_lastLoginDetails != null)
                            await LoginAsync(_lastLoginDetails);
                    }
                    catch (TimeoutException)
                    {
                        if (ReconnectOnDisconnected)
                            TryReconnect();
                    }
                });
                _retryCount++;
            }
            else
            {
                ReconnectOnDisconnected = false;
                _retryCount = 0;
                Messenger.Default.Send(new ReconnectFailedMessage());
            }
        }

        public async Task StopAsync()
        {
            if (_steamClient.IsConnected)
                while (true)
                    try
                    {
                        await LogoutAsync(); // Not sure if it is of any use to logout here 
                        await DisconnectAsync();
                        break;
                    }
                    catch (TimeoutException)
                    {
                    }

            _callbackHandlerCancellationTokenSource.Cancel();
        }

        public Task<SteamClient.ConnectedCallback> ConnectAsync()
        {
            _connectTaskCompletionSource = new TaskCompletionSource<SteamClient.ConnectedCallback>();
            Task.Run(() =>
            {
                if (_applicationSettingsService.Settings.PreferredCmServers == null)
                {
                    _steamClient.Connect();
                }
                else
                {
                    var cmServer =
                        _applicationSettingsService.Settings.PreferredCmServers[
                            new Random().Next(0, _applicationSettingsService.Settings.PreferredCmServers.Count)];

                    var ep = cmServer.Split(':');
                    var host = ep[0];
                    var port = int.Parse(ep[^1], NumberStyles.None, NumberFormatInfo.CurrentInfo);
                    _steamClient.Connect(ServerRecord.CreateServer(host, port, ProtocolTypes.All));
                }
            });
            ThrowIfTimeout(_connectTaskCompletionSource);
            return _connectTaskCompletionSource.Task;
        }

        public Task<SteamUser.LoggedOnCallback> LoginAsync(SteamUser.LogOnDetails logOnDetails)
        {
            _loginTaskCompletionSource = new TaskCompletionSource<SteamUser.LoggedOnCallback>();
            Task.Run(() =>
            {
                _lastLoginDetails = logOnDetails;
                if (_applicationSettingsService.Settings.SentryHashStore != null &&
                    _applicationSettingsService.Settings.SentryHashStore.ContainsKey(logOnDetails.Username))
                    logOnDetails.SentryFileHash =
                        _applicationSettingsService.Settings.SentryHashStore[logOnDetails.Username];
                _steamUser.LogOn(logOnDetails);
            });
            ThrowIfTimeout(_loginTaskCompletionSource);
            return _loginTaskCompletionSource.Task;
        }

        public Task<SteamUser.AccountInfoCallback> SetPersonaNameAsync(string personaName)
        {
            _setPersonaNameTaskCompletionSource = new TaskCompletionSource<SteamUser.AccountInfoCallback>();
            var oldName = PersonaName;
            Task.Run(() =>
            {
                _steamFriends.SetPersonaName(personaName);
                Messenger.Default.Send(new PersonaNameChangedMessage());
            });
            ThrowIfTimeout(_setPersonaNameTaskCompletionSource, () =>
            {
                Task.Run(() =>
                {
                    _steamFriends.SetPersonaName(oldName);
                    Messenger.Default.Send(new PersonaNameChangedMessage());
                });
            });
            return _setPersonaNameTaskCompletionSource.Task;
        }

        public Task<SteamFriends.PersonaStateCallback> SetPersonaStateAsync(EPersonaState state)
        {
            _setPersonaStateTaskCompletionSource = new TaskCompletionSource<SteamFriends.PersonaStateCallback>();
            var oldState = PersonaState;
            Task.Run(() =>
            {
                _steamFriends.SetPersonaState(state);
                Messenger.Default.Send(new PersonaStateChangedMessage());
            });
            ThrowIfTimeout(_setPersonaStateTaskCompletionSource, () =>
            {
                Task.Run(() =>
                {
                    _steamFriends.SetPersonaState(oldState);
                    Messenger.Default.Send(new PersonaStateChangedMessage());
                });
            });
            return _setPersonaStateTaskCompletionSource.Task;
        }

        public Task SendChatMessageAsync(SteamID target, EChatEntryType type, string message)
        {
            var taskCompletionSource = new TaskCompletionSource<bool>();
            Task.Run(() =>
            {
                _steamFriends.SendChatMessage(target, type, message);
                if (!taskCompletionSource.Task.IsCompleted)
                    taskCompletionSource.TrySetResult(true);
            });
            ThrowIfTimeout(taskCompletionSource);
            return taskCompletionSource.Task;
        }

        public Task RemoveFriendAsync(SteamID target)
        {
            var taskCompletionSource = new TaskCompletionSource<bool>();
            Task.Run(() =>
            {
                _steamFriends.RemoveFriend(target);
                if (!taskCompletionSource.Task.IsCompleted)
                    taskCompletionSource.TrySetResult(true);
            });
            ThrowIfTimeout(taskCompletionSource);
            return taskCompletionSource.Task;
        }

        public Task<SteamFriends.FriendAddedCallback> AddFriendAsync(SteamID target)
        {
            _addFriendTaskCompletionSource = new TaskCompletionSource<SteamFriends.FriendAddedCallback>();
            Task.Run(() => { _steamFriends.AddFriend(target); });
            ThrowIfTimeout(_addFriendTaskCompletionSource);
            return _addFriendTaskCompletionSource.Task;
        }

        public Task<SteamFriends.FriendAddedCallback> AddFriendAsync(string targetAccountNameOrEmail)
        {
            _addFriendTaskCompletionSource = new TaskCompletionSource<SteamFriends.FriendAddedCallback>();
            Task.Run(() => { _steamFriends.AddFriend(targetAccountNameOrEmail); });
            ThrowIfTimeout(_addFriendTaskCompletionSource);
            return _addFriendTaskCompletionSource.Task;
        }

        public void Logout()
        {
            //LogoutAsync().Wait();
            _steamUser.LogOff();
        }

        // The LoggedOffCallback doesn't work. We cannot get any response on this operation.
        public Task LogoutAsync()
        {
            //_logoutTaskCompletionSource = new TaskCompletionSource<SteamUser.LoggedOffCallback>();
            //Task.Run(() => _steamUser.LogOff());
            //ThrowIfTimeout(_logoutTaskCompletionSource);
            //return _logoutTaskCompletionSource.Task;
            return Task.Run(Logout);
        }

        public Task DisconnectAsync()
        {
            _disconnectTaskCompletionSource = new TaskCompletionSource<SteamClient.DisconnectedCallback>();
            Task.Run(() => _steamClient.Disconnect());
            ThrowIfTimeout(_disconnectTaskCompletionSource);
            return _disconnectTaskCompletionSource.Task;
        }

        private void ThrowIfTimeout(object taskCompletionSource, Action timeoutAction = null)
        {
            var cts = new CancellationTokenSource(DefaultTimeout);
            cts.Token.Register(() =>
            {
                var trySetExceptionMethod = taskCompletionSource.GetType()
                    .GetMethod("TrySetException", new[] {typeof(Exception)});
                if (!(taskCompletionSource.GetType().GetProperty("Task")
                        ?.GetValue(taskCompletionSource) is Task task) || trySetExceptionMethod == null ||
                    task.IsCompleted) return;
                timeoutAction?.Invoke();
                trySetExceptionMethod.Invoke(taskCompletionSource,
                    new object[] {new TimeoutException(taskCompletionSource.GetType().Name)});
            });
        }

        public class Friend : INotifyPropertyChanged
        {
            private readonly SteamFriends _steamFriends;
            private bool _visible;

            public Friend(SteamID steamId, SteamFriends steamFriends)
            {
                SteamId = steamId;
                _steamFriends = steamFriends;
                Visible = true;
            }

            public SteamID SteamId { get; }

            public byte[] Avatar => _steamFriends.GetFriendAvatar(SteamId);

            public GameID GamePlayed => _steamFriends.GetFriendGamePlayed(SteamId);

            public string GamePlayedName => _steamFriends.GetFriendGamePlayedName(SteamId);

            public string PersonaName => _steamFriends.GetFriendPersonaName(SteamId);

            public EPersonaState PersonaState => _steamFriends.GetFriendPersonaState(SteamId);

            public Brush PersonaStateColor
            {
                get
                {
                    return PersonaState switch
                    {
                        EPersonaState.Online => new SolidColorBrush(Color.FromRgb(115, 209, 245)),
                        EPersonaState.LookingToTrade => new SolidColorBrush(Color.FromRgb(115, 209, 245)),
                        EPersonaState.LookingToPlay => new SolidColorBrush(Color.FromRgb(115, 209, 245)),
                        EPersonaState.Away => new SolidColorBrush(Color.FromRgb(66, 103, 119)),
                        EPersonaState.Snooze => new SolidColorBrush(Color.FromRgb(66, 103, 119)),
                        EPersonaState.Busy => new SolidColorBrush(Color.FromRgb(66, 103, 119)),
                        _ => new SolidColorBrush(Color.FromRgb(255, 255, 255))
                    };
                }
            }

            public EFriendRelationship Relationship => _steamFriends.GetFriendRelationship(SteamId);

            public bool Visible
            {
                get => _visible;
                set
                {
                    if (_visible == value)
                        return;

                    _visible = value;
                    OnPropertyChanged(nameof(Visible));
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;

            private void OnPropertyChanged(string propertyName = null)
            {
                var handler = PropertyChanged;
                handler?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }

            public void OnStateChanged()
            {
                OnPropertyChanged(nameof(Avatar));
                OnPropertyChanged(nameof(GamePlayed));
                OnPropertyChanged(nameof(GamePlayedName));
                OnPropertyChanged(nameof(PersonaName));
                OnPropertyChanged(nameof(PersonaState));
                OnPropertyChanged(nameof(Relationship));
                OnPropertyChanged(nameof(Visible));
                OnPropertyChanged(nameof(PersonaStateColor));
            }

            public override bool Equals(object obj)
            {
                if (!(obj is Friend target))
                    return false;

                return SteamId == target.SteamId;
            }

            public bool Equals(Friend friend)
            {
                if ((object) friend == null)
                    return false;

                return SteamId == friend.SteamId;
            }

            public override int GetHashCode()
            {
                return SteamId.GetHashCode();
            }

            public static bool operator ==(Friend a, Friend b)
            {
                if (ReferenceEquals(a, b))
                    return true;

                if (a is null || b is null)
                    return false;

                return a.SteamId == b.SteamId;
            }

            public static bool operator !=(Friend a, Friend b)
            {
                return !(a == b);
            }
        }
    }
}