using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SteamKit2;

namespace SteamFriendsManager.Service
{
    public class SteamClientService : IDisposable
    {
        private CancellationTokenSource _callbackHandlerCancellationTokenSource;
        private TaskCompletionSource<SteamClient.ConnectedCallback> _connectTaskCompletionSource;
        private TaskCompletionSource<SteamClient.DisconnectedCallback> _disconnectTaskCompletionSource;
        private bool _disposed;
        private string _lastLoginUsername;
        private TaskCompletionSource<SteamUser.LoggedOnCallback> _loginTaskCompletionSource;
        private TaskCompletionSource<SteamUser.LoggedOffCallback> _logoutTaskCompletionSource;
        private readonly ApplicationSettingsService _applicationSettingsService;
        private readonly SteamClient _steamClient;
        private readonly SteamUser _steamUser;

        public SteamClientService(ApplicationSettingsService applicationSettingsService)
        {
            _applicationSettingsService = applicationSettingsService;

            DefaultTimeout = TimeSpan.FromMilliseconds(15000);
            _steamClient = new SteamClient();
            _steamUser = _steamClient.GetHandler<SteamUser>();
            var steamFriends = _steamClient.GetHandler<SteamFriends>();
            Friends = new FriendList(steamFriends);
        }

        public TimeSpan DefaultTimeout { get; set; }

        public bool IsConnected
        {
            get { return _steamClient.IsConnected; }
        }

        public FriendList Friends { get; private set; }

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
                    });

                    callback.Handle<SteamClient.DisconnectedCallback>(cb =>
                    {
                        if (_disconnectTaskCompletionSource != null && !_disconnectTaskCompletionSource.Task.IsCompleted)
                            _disconnectTaskCompletionSource.TrySetResult(cb);
                    });

                    callback.Handle<SteamUser.LoggedOnCallback>(cb =>
                    {
                        if (_loginTaskCompletionSource != null && !_loginTaskCompletionSource.Task.IsCompleted)
                            _loginTaskCompletionSource.TrySetResult(cb);
                    });

                    callback.Handle<SteamUser.UpdateMachineAuthCallback>(async cb =>
                    {
                        if (_applicationSettingsService.Settings.SentryHashStore == null)
                            _applicationSettingsService.Settings.SentryHashStore = new Dictionary<string, byte[]>();

                        var sentryHash = CryptoHelper.SHAHash(cb.Data);
                        _applicationSettingsService.Settings.SentryHashStore[_lastLoginUsername] = sentryHash;
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
                        var query = from f in Friends where f.SteamId.Equals(cb.FriendID) select f;
                        var friends = query as Friend[] ?? query.ToArray();
                        if (friends.Any())
                        {
                            var friend = friends.First();
                            friend.OnStateChanged();
                        }
                    });
                }
            }, _callbackHandlerCancellationTokenSource.Token);
        }

        public async Task StopAsync()
        {
            if (_steamClient.IsConnected)
            {
                while (true)
                {
                    try
                    {
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
            var task = ConnectAsync();
            task.Wait();
            return task.Result;
        }

        public Task<SteamClient.ConnectedCallback> ConnectAsync()
        {
            _connectTaskCompletionSource = new TaskCompletionSource<SteamClient.ConnectedCallback>();
            Task.Run(() => _steamClient.Connect());
            ThrowIfTimeout(_connectTaskCompletionSource);
            return _connectTaskCompletionSource.Task;
        }

        public SteamUser.LoggedOnCallback Login(SteamUser.LogOnDetails logOnDetails)
        {
            var task = LoginAsync(logOnDetails);
            task.Wait();
            return task.Result;
        }

        public Task<SteamUser.LoggedOnCallback> LoginAsync(SteamUser.LogOnDetails logOnDetails)
        {
            _loginTaskCompletionSource = new TaskCompletionSource<SteamUser.LoggedOnCallback>();
            Task.Run(() =>
            {
                _lastLoginUsername = logOnDetails.Username;
                if (_applicationSettingsService.Settings.SentryHashStore != null &&
                    _applicationSettingsService.Settings.SentryHashStore.ContainsKey(logOnDetails.Username))
                    logOnDetails.SentryFileHash =
                        _applicationSettingsService.Settings.SentryHashStore[logOnDetails.Username];
                _steamUser.LogOn(logOnDetails);
            });
            ThrowIfTimeout(_loginTaskCompletionSource);
            return _loginTaskCompletionSource.Task;
        }

        public void Logout()
        {
            var task = LogoutAsync();
            task.Wait();
        }

        public Task LogoutAsync()
        {
            _logoutTaskCompletionSource = new TaskCompletionSource<SteamUser.LoggedOffCallback>();
            Task.Run(() => _steamUser.LogOff());
            ThrowIfTimeout(_logoutTaskCompletionSource);
            return _logoutTaskCompletionSource.Task;
        }

        public void Disconnect()
        {
            var task = DisconnectAsync();
            task.Wait();
        }

        public Task DisconnectAsync()
        {
            _disconnectTaskCompletionSource = new TaskCompletionSource<SteamClient.DisconnectedCallback>();
            Task.Run(() => _steamClient.Disconnect());
            ThrowIfTimeout(_disconnectTaskCompletionSource);
            return _disconnectTaskCompletionSource.Task;
        }

        private void ThrowIfTimeout(object taskCompletionSource)
        {
            var cts = new CancellationTokenSource(DefaultTimeout);
            cts.Token.Register(() =>
            {
                var task = taskCompletionSource.GetType().GetProperty("Task").GetValue(taskCompletionSource) as Task;
                var trySetExceptionMethod = taskCompletionSource.GetType()
                    .GetMethod("TrySetException", new[] {typeof (Exception)});
                if (task != null && trySetExceptionMethod != null && !task.IsCompleted)
                {
                    trySetExceptionMethod.Invoke(taskCompletionSource,
                        new object[] {new TimeoutException(taskCompletionSource.GetType().Name)});
                }
            });
        }

        public class Friend : INotifyPropertyChanged
        {
            private readonly SteamFriends _steamFriends;

            public Friend(SteamID steamId, SteamFriends steamFriends)
            {
                SteamId = steamId;
                _steamFriends = steamFriends;
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

            public string PersonalName
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
                OnPropertyChanged("PersonalName");
                OnPropertyChanged("PersonaState");
                OnPropertyChanged("Relationship");
            }
        }

        public class FriendList : IReadOnlyList<Friend>
        {
            private readonly Dictionary<SteamID, Friend> _cache;
            private readonly SteamFriends _steamFriends;

            public FriendList(SteamFriends steamFriends)
            {
                _steamFriends = steamFriends;
                _cache = new Dictionary<SteamID, Friend>();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            public IEnumerator<Friend> GetEnumerator()
            {
                for (var i = 0; i < Count; ++i)
                {
                    yield return this[i];
                }
            }

            public Friend this[int index]
            {
                get
                {
                    var steamId = _steamFriends.GetFriendByIndex(index);

                    if (!_cache.ContainsKey(steamId))
                        _cache[steamId] = new Friend(steamId, _steamFriends);

                    return _cache[steamId];
                }
            }

            public int Count
            {
                get { return _steamFriends.GetFriendCount(); }
            }
        }
    }
}