using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using GalaSoft.MvvmLight.Messaging;
using GalaSoft.MvvmLight.Threading;
using SteamKit2;

namespace SteamFriendsManager.Service
{
    public class SteamClientService : IDisposable
    {
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
        private readonly ApplicationSettingsService _applicationSettingsService;
        private readonly SteamClient _steamClient;
        private readonly SteamFriends _steamFriends;
        private readonly SteamUser _steamUser;

        public SteamClientService(ApplicationSettingsService applicationSettingsService)
        {
            _applicationSettingsService = applicationSettingsService;

            DefaultTimeout = TimeSpan.FromMilliseconds(10000);
            _steamClient = new SteamClient();
            _steamUser = _steamClient.GetHandler<SteamUser>();
            _steamFriends = _steamClient.GetHandler<SteamFriends>();
            Friends = new ObservableCollection<Friend>();
        }

        public TimeSpan DefaultTimeout { get; set; }
        public bool ReconnectOnDisconnected { get; set; }

        public bool IsConnected
        {
            get { return _steamClient.IsConnected; }
        }

        public ObservableCollection<Friend> Friends { get; private set; }

        public string PersonaName
        {
            get { return _steamFriends.GetPersonaName(); }
        }

        public EPersonaState PersonaState
        {
            get { return _steamFriends.GetPersonaState(); }
        }

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
            }
            _disposed = true;
        }

        public void Start()
        {
            _callbackHandlerCancellationTokenSource = new CancellationTokenSource();
            Task.Run(() =>
            {
                while (!_callbackHandlerCancellationTokenSource.IsCancellationRequested)
                {
                    var callback = _steamClient.WaitForCallback(true, TimeSpan.FromMilliseconds(50));
                    if (callback == null)
                        continue;

                    callback.Handle<SteamClient.ConnectedCallback>(cb =>
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

                    callback.Handle<SteamClient.DisconnectedCallback>(cb =>
                    {
                        if (_disconnectTaskCompletionSource != null && !_disconnectTaskCompletionSource.Task.IsCompleted)
                        {
                            _disconnectTaskCompletionSource.TrySetResult(cb);
                        }
                        else if (ReconnectOnDisconnected)
                        {
                            TryReconnect();
                        }
                    });

                    callback.Handle<SteamUser.LoggedOnCallback>(cb =>
                    {
                        if (_loginTaskCompletionSource != null && !_loginTaskCompletionSource.Task.IsCompleted)
                            _loginTaskCompletionSource.TrySetResult(cb);
                    });

                    //callback.Handle<SteamUser.LoggedOffCallback>(cb =>
                    //{
                    //    if (_logoutTaskCompletionSource != null && !_logoutTaskCompletionSource.Task.IsCompleted)
                    //        _logoutTaskCompletionSource.TrySetResult(cb);
                    //});

                    callback.Handle<SteamUser.UpdateMachineAuthCallback>(async cb =>
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

                    callback.Handle<SteamFriends.PersonaStateCallback>(cb =>
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

                    callback.Handle<SteamClient.CMListCallback>(async cb =>
                    {
                        if (_applicationSettingsService.Settings.PreferedCmServers == null)
                            _applicationSettingsService.Settings.PreferedCmServers = new List<string>();
                        _applicationSettingsService.Settings.PreferedCmServers.Clear();
                        _applicationSettingsService.Settings.PreferedCmServers.AddRange(
                            (from sv in cb.Servers select sv.ToString()).Take(8));
                        await _applicationSettingsService.SaveAsync();
                    });

                    callback.Handle<SteamUser.AccountInfoCallback>(cb =>
                    {
                        if (_setPersonaNameTaskCompletionSource != null &&
                            !_setPersonaNameTaskCompletionSource.Task.IsCompleted)
                            _setPersonaNameTaskCompletionSource.TrySetResult(cb);
                        Messenger.Default.Send(new PersonaNameChangedMessage());
                    });

                    callback.Handle<SteamFriends.FriendsListCallback>(cb =>
                    {
                        DispatcherHelper.CheckBeginInvokeOnUI(() =>
                        {
                            if (!cb.Incremental)
                            {
                                Friends.Clear();
                            }

                            foreach (var friend in from friendRaw in cb.FriendList
                                where friendRaw.SteamID.IsIndividualAccount
                                select new Friend(friendRaw.SteamID, _steamFriends))
                            {
                                if (Friends.Contains(friend))
                                {
                                    if (friend.Relationship == EFriendRelationship.None)
                                        Friends.Remove(friend);
                                }
                                else
                                {
                                    Friends.Add(friend);
                                }
                            }
                        });
                    });

                    callback.Handle<SteamFriends.FriendAddedCallback>(cb =>
                    {
                        if (_addFriendTaskCompletionSource != null && !_addFriendTaskCompletionSource.Task.IsCompleted)
                            _addFriendTaskCompletionSource.TrySetResult(cb);
                    });
                }
            }, _callbackHandlerCancellationTokenSource.Token);
        }

        private void TryReconnect()
        {
            if (_retryCount < 3)
            {
                // Unexceptedly disconnect, try reconnect.
                if (_retryCountResetCancellationTokenSource != null &&
                    !_retryCountResetCancellationTokenSource.IsCancellationRequested)
                {
                    _retryCountResetCancellationTokenSource.Cancel();
                }
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
            {
                while (true)
                {
                    try
                    {
                        await LogoutAsync(); // Not sure if it is of any use to logout here 
                        await DisconnectAsync();
                        break;
                    }
                    catch (TimeoutException)
                    {
                    }
                }
            }

            _callbackHandlerCancellationTokenSource.Cancel();
        }

        public SteamClient.ConnectedCallback Connect()
        {
            return ConnectAsync().Result;
        }

        public Task<SteamClient.ConnectedCallback> ConnectAsync()
        {
            _connectTaskCompletionSource = new TaskCompletionSource<SteamClient.ConnectedCallback>();
            Task.Run(() =>
            {
                if (_applicationSettingsService.Settings.PreferedCmServers == null)
                {
                    _steamClient.Connect();
                }
                else
                {
                    var cmServer =
                        _applicationSettingsService.Settings.PreferedCmServers[
                            new Random().Next(0, _applicationSettingsService.Settings.PreferedCmServers.Count)];

                    var ep = cmServer.Split(':');
                    var ip = IPAddress.Parse(ep.Length > 2 ? string.Join(":", ep, 0, ep.Length - 1) : ep[0]);
                    var port = int.Parse(ep[ep.Length - 1], NumberStyles.None, NumberFormatInfo.CurrentInfo);
                    _steamClient.Connect(new IPEndPoint(ip, port));
                }
            });
            ThrowIfTimeout(_connectTaskCompletionSource);
            return _connectTaskCompletionSource.Task;
        }

        public SteamUser.LoggedOnCallback Login(SteamUser.LogOnDetails logOnDetails)
        {
            return LoginAsync(logOnDetails).Result;
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

        public SteamUser.AccountInfoCallback SetPersonaName(string personaName)
        {
            return SetPersonaNameAsync(personaName).Result;
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

        public SteamFriends.PersonaStateCallback SetPersonaState(EPersonaState state)
        {
            return SetPersonaStateAsync(state).Result;
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

        public void SendChatMessage(SteamID target, EChatEntryType type, string message)
        {
            _steamFriends.SendChatMessage(target, type, message);
        }

        public Task SendChatMessageAsync(SteamID target, EChatEntryType type, string message)
        {
            var taskCompletionSource = new TaskCompletionSource<bool>();
            Task.Run(() =>
            {
                SendChatMessage(target, type, message);
                if (!taskCompletionSource.Task.IsCompleted)
                    taskCompletionSource.TrySetResult(true);
            });
            ThrowIfTimeout(taskCompletionSource);
            return taskCompletionSource.Task;
        }

        public void RemoveFriend(SteamID target)
        {
            _steamFriends.RemoveFriend(target);
        }

        public Task RemoveFriendAsync(SteamID target)
        {
            var taskCompletionSource = new TaskCompletionSource<bool>();
            Task.Run(() =>
            {
                RemoveFriend(target);
                if (!taskCompletionSource.Task.IsCompleted)
                    taskCompletionSource.TrySetResult(true);
            });
            ThrowIfTimeout(taskCompletionSource);
            return taskCompletionSource.Task;
        }

        public SteamFriends.FriendAddedCallback AddFriend(SteamID target)
        {
            return AddFriendAsync(target).Result;
        }

        public Task<SteamFriends.FriendAddedCallback> AddFriendAsync(SteamID target)
        {
            _addFriendTaskCompletionSource = new TaskCompletionSource<SteamFriends.FriendAddedCallback>();
            Task.Run(() => { _steamFriends.AddFriend(target); });
            ThrowIfTimeout(_addFriendTaskCompletionSource);
            return _addFriendTaskCompletionSource.Task;
        }

        public SteamFriends.FriendAddedCallback AddFriend(string targetAccountNameOrEmail)
        {
            return AddFriendAsync(targetAccountNameOrEmail).Result;
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
            return Task.Run(() => Logout());
        }

        public void Disconnect()
        {
            DisconnectAsync().Wait();
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
                var task = taskCompletionSource.GetType().GetProperty("Task").GetValue(taskCompletionSource) as Task;
                var trySetExceptionMethod = taskCompletionSource.GetType()
                    .GetMethod("TrySetException", new[] {typeof (Exception)});
                if (task == null || trySetExceptionMethod == null || task.IsCompleted) return;
                if (timeoutAction != null)
                    timeoutAction.Invoke();
                trySetExceptionMethod.Invoke(taskCompletionSource,
                    new object[] {new TimeoutException(taskCompletionSource.GetType().Name)});
            });
        }

        public class Friend : INotifyPropertyChanged
        {
            private bool _show;
            private readonly SteamFriends _steamFriends;

            public Friend(SteamID steamId, SteamFriends steamFriends)
            {
                SteamId = steamId;
                _steamFriends = steamFriends;
                Show = true;
            }

            public SteamID SteamId { get; private set; }

            public byte[] Avatar
            {
                get { return _steamFriends.GetFriendAvatar(SteamId); }
            }

            public GameID GamePlayed
            {
                get { return _steamFriends.GetFriendGamePlayed(SteamId); }
            }

            public string GamePlayedName
            {
                get { return _steamFriends.GetFriendGamePlayedName(SteamId); }
            }

            public string PersonaName
            {
                get { return _steamFriends.GetFriendPersonaName(SteamId); }
            }

            public EPersonaState PersonaState
            {
                get { return _steamFriends.GetFriendPersonaState(SteamId); }
            }

            public EFriendRelationship Relationship
            {
                get { return _steamFriends.GetFriendRelationship(SteamId); }
            }

            public bool Show
            {
                get { return _show; }
                set
                {
                    if (_show == value)
                        return;

                    _show = value;
                    OnPropertyChanged("Show");
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;

            private void OnPropertyChanged(string propertyName = null)
            {
                var handler = PropertyChanged;
                if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
            }

            public void OnStateChanged()
            {
                OnPropertyChanged("Avatar");
                OnPropertyChanged("GamePlayed");
                OnPropertyChanged("GamePlayedName");
                OnPropertyChanged("PersonaName");
                OnPropertyChanged("PersonaState");
                OnPropertyChanged("Relationship");
                OnPropertyChanged("Show");
            }

            public override bool Equals(object obj)
            {
                if (obj == null)
                    return false;

                var target = obj as Friend;
                if ((object) target == null)
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

                if (((object) a == null) || ((object) b == null))
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