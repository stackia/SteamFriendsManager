using System;
using System.Threading;
using System.Threading.Tasks;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using SteamFriendsManager.Service;
using SteamKit2;

namespace SteamFriendsManager.ViewModel
{
    public class LoginPageViewModel : ViewModelBase
    {
        private readonly ApplicationSettingsService _applicationSettingsService;
        private readonly SteamClientService _steamClientService;
        private bool _clearPageHistoryOnNextTryLogin;
        private bool _isLoading;
        private RelayCommand _login;
        private bool _logoutOnNextTryLogin;
        private string _password;
        private bool _shouldRememberAccount;
        private string _username;

        public LoginPageViewModel(SteamClientService steamClientService,
            ApplicationSettingsService applicationSettingsService)
        {
            _steamClientService = steamClientService;
            _applicationSettingsService = applicationSettingsService;

            _shouldRememberAccount = _applicationSettingsService.Settings.ShouldRememberAccount;
            if (_shouldRememberAccount)
            {
                _username = _applicationSettingsService.Settings.LastUsername;
                _password = _applicationSettingsService.Settings.LastPassword;
            }

            MessengerInstance.Register<ClearPageHistoryOnNextTryLoginMessage>(this,
                msg => { _clearPageHistoryOnNextTryLogin = true; });

            MessengerInstance.Register<LogoutOnNextTryLoginMessage>(this, msg => { _logoutOnNextTryLogin = true; });
        }

        public bool ShouldRememberAccount
        {
            get => _shouldRememberAccount;
            set
            {
                if (_shouldRememberAccount == value)
                    return;

                _shouldRememberAccount = value;
                RaisePropertyChanged(() => ShouldRememberAccount);
            }
        }

        public string Username
        {
            get => _username;
            set
            {
                if (_username == value)
                    return;

                _username = value;
                RaisePropertyChanged(() => Username);
            }
        }

        public string Password
        {
            get => _password;
            set
            {
                if (_password == value)
                    return;

                _password = value;
                RaisePropertyChanged(() => Password);
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            private set
            {
                if (_isLoading == value)
                    return;

                _isLoading = value;
                RaisePropertyChanged(() => IsLoading);
            }
        }

        public RelayCommand Login =>
            _login ??= new RelayCommand(async () =>
            {
                await Task.Run(async () =>
                {
                    var stopTrying = false;
                    var success = false;
                    string authCode = null;
                    string twoFactorCode = null;
                    IsLoading = true;

                    if (string.IsNullOrWhiteSpace(Username))
                    {
                        MessengerInstance.Send(new ShowMessageDialogMessageWithCallback("提示", "请输入用户名。",
                            result => IsLoading = false));
                        return;
                    }

                    if (string.IsNullOrWhiteSpace(Password))
                    {
                        MessengerInstance.Send(new ShowMessageDialogMessageWithCallback("提示", "请输入密码。",
                            result => IsLoading = false));
                        return;
                    }

                    while (!stopTrying)
                        try
                        {
                            if (_clearPageHistoryOnNextTryLogin)
                            {
                                MessengerInstance.Send(new ClearPageHistoryMessage());
                                _clearPageHistoryOnNextTryLogin = false;
                            }

                            if (_logoutOnNextTryLogin)
                            {
                                _logoutOnNextTryLogin = false;
                                await _steamClientService.LogoutAsync();
                            }

                            _steamClientService.ReconnectOnDisconnected = false;

                            if (_steamClientService.IsConnected)
                            {
                                await _steamClientService.DisconnectAsync();
                                await Task.Delay(1000);
                                // It may fail to connect if we immediately reconnect after disconnected
                            }

                            await _steamClientService.ConnectAsync();
                            var result = await _steamClientService.LoginAsync(new SteamUser.LogOnDetails
                            {
                                Username = Username,
                                Password = Password,
                                AuthCode = authCode,
                                TwoFactorCode = twoFactorCode
                            });

                            var authCodeInputHint = "你的 Steam 帐号开启了两步验证，验证码已发往你的邮箱。请输入验证码：";
                            var twoFactorCodeInputHint = "你的 Steam 帐号开启了两步验证。请输入验证码：";
                            switch (result.Result)
                            {
                                case EResult.AlreadyLoggedInElsewhere:
                                    MessengerInstance.Send(
                                        new ShowMessageDialogMessage("登录失败", "你已经在其他地方登录了这个帐号。"));
                                    stopTrying = true;
                                    break;

                                case EResult.InvalidPassword:
                                    MessengerInstance.Send(new ShowMessageDialogMessage("登录失败", "密码错误。"));
                                    stopTrying = true;
                                    break;

                                case EResult.TwoFactorCodeMismatch:
                                    twoFactorCodeInputHint = "Steam 令牌验证码错误，请重新输入：";
                                    goto case EResult.AccountLoginDeniedNeedTwoFactor;

                                case EResult.AccountLoginDeniedNeedTwoFactor:
                                    var twoFactorCodeInputLock = new object();
                                    lock (twoFactorCodeInputLock)
                                    {
                                        MessengerInstance.Send(new ShowInputDialogMessage("Steam 令牌",
                                            twoFactorCodeInputHint,
                                            s =>
                                            {
                                                if (s == null)
                                                    stopTrying = true;

                                                twoFactorCode = s;
                                                lock (twoFactorCodeInputLock)
                                                {
                                                    Monitor.Pulse(twoFactorCodeInputLock);
                                                }
                                            }));
                                        Monitor.Wait(twoFactorCodeInputLock);
                                    }

                                    break;

                                case EResult.InvalidLoginAuthCode:
                                    authCodeInputHint = "Steam 令牌验证码错误，请重新输入：";
                                    goto case EResult.AccountLogonDenied;

                                case EResult.AccountLogonDenied:
                                    var authCodeInputLock = new object();
                                    lock (authCodeInputLock)
                                    {
                                        MessengerInstance.Send(new ShowInputDialogMessage("Steam 令牌",
                                            authCodeInputHint,
                                            s =>
                                            {
                                                if (s == null)
                                                    stopTrying = true;

                                                authCode = s;
                                                lock (authCodeInputLock)
                                                {
                                                    Monitor.Pulse(authCodeInputLock);
                                                }
                                            }));
                                        Monitor.Wait(authCodeInputLock);
                                    }

                                    break;

                                case EResult.OK:
                                    success = true;
                                    stopTrying = true;
                                    break;

                                default:
                                    MessengerInstance.Send(new ShowMessageDialogMessage("登录失败",
                                        "未知错误：" + result.Result));
                                    stopTrying = true;
                                    break;
                            }

                            if (success)
                            {
                                _steamClientService.ReconnectOnDisconnected = true;
                                _applicationSettingsService.Settings.ShouldRememberAccount =
                                    ShouldRememberAccount;
                                if (ShouldRememberAccount)
                                {
                                    _applicationSettingsService.Settings.LastUsername = Username;
                                    _applicationSettingsService.Settings.LastPassword = Password;
                                }
                                else
                                {
                                    _applicationSettingsService.Settings.LastUsername = null;
                                    _applicationSettingsService.Settings.LastPassword = null;
                                }

                                await _applicationSettingsService.SaveAsync();
                                MessengerInstance.Send(new SwitchPageMessage(SwitchPageMessage.Page.FriendList,
                                    true));
                            }
                            else
                            {
                                _applicationSettingsService.Settings.ShouldRememberAccount = false;
                                if (ShouldRememberAccount)
                                {
                                    _applicationSettingsService.Settings.LastUsername = null;
                                    _applicationSettingsService.Settings.LastPassword = null;
                                }

                                await _applicationSettingsService.SaveAsync();
                            }
                        }
                        catch (TimeoutException)
                        {
                            MessengerInstance.Send(new ShowMessageDialogMessage("登录失败", "连接超时，请重试。"));
                            stopTrying = true;
                        }

                    IsLoading = false;
                });
            });
    }
}