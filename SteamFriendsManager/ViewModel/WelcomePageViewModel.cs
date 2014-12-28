using System;
using System.Diagnostics;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using SteamFriendsManager.Service;

namespace SteamFriendsManager.ViewModel
{
    public class WelcomePageViewModel : ViewModelBase
    {
        private RelayCommand<Uri> _navigateToSupportPage;
        private RelayCommand _switchToLoginPage;
        private RelayCommand _testConnect;
        private readonly SteamClientService _steamClientService;

        public WelcomePageViewModel(SteamClientService steamClientService)
        {
            _steamClientService = steamClientService;
        }

        public RelayCommand<Uri> NavigateToSupportPage
        {
            get
            {
                return _navigateToSupportPage ??
                       (_navigateToSupportPage =
                           new RelayCommand<Uri>(uri => { Process.Start(new ProcessStartInfo(uri.AbsoluteUri)); }));
            }
        }

        public RelayCommand SwitchToLoginPage
        {
            get
            {
                return _switchToLoginPage ??
                       (_switchToLoginPage =
                           new RelayCommand(
                               () => { MessengerInstance.Send(new SwitchPageMessage(SwitchPageMessage.Page.Login)); }));
            }
        }

        public RelayCommand TestConnect
        {
            get
            {
                return _testConnect ?? (_testConnect = new RelayCommand(async () =>
                {
                    string message;
                    try
                    {
                        message = (await _steamClientService.ConnectAsync()).ToString();
                    }
                    catch (TimeoutException e)
                    {
                        message = e.Message;
                    }
                    MessengerInstance.Send(new ShowMessageDialogMessage("Debug Info", message));
                }));
            }
        }
    }
}