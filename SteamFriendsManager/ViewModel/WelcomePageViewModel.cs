using System.Reflection;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;

namespace SteamFriendsManager.ViewModel
{
    public class WelcomePageViewModel : ViewModelBase
    {
        private RelayCommand _switchToLoginPage;

        public RelayCommand SwitchToLoginPage
        {
            get
            {
                return _switchToLoginPage ??= new RelayCommand(
                    () => { MessengerInstance.Send(new SwitchPageMessage(SwitchPageMessage.Page.Login)); });
            }
        }

        public string Version
        {
            get
            {
                var version = Assembly.GetExecutingAssembly().GetName().Version;
                return $"{version.Major}.{version.Minor}.{version.Build}";
            }
        }
    }
}