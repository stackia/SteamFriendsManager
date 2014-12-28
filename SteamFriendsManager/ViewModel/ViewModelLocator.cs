/*
  In App.xaml:
  <Application.Resources>
      <vm:ViewModelLocator xmlns:vm="clr-namespace:SteamFriendsManager"
                           x:Key="Locator" />
  </Application.Resources>
  
  In the View:
  DataContext="{Binding Source={StaticResource Locator}, Path=ViewModelName}"

  You can also use Blend to do all this with the tool's support.
  See http://www.galasoft.ch/mvvm
*/

using GalaSoft.MvvmLight.Ioc;
using Microsoft.Practices.ServiceLocation;
using SteamFriendsManager.Service;

namespace SteamFriendsManager.ViewModel
{
    public class ViewModelLocator
    {
        public ViewModelLocator()
        {
            ServiceLocator.SetLocatorProvider(() => SimpleIoc.Default);

            SimpleIoc.Default.Register<MainWindowViewModel>();
            SimpleIoc.Default.Register<WelcomePageViewModel>();
            SimpleIoc.Default.Register<LoginPageViewModel>();
            SimpleIoc.Default.Register<FriendListPageViewModel>();

            SimpleIoc.Default.Register<ApplicationSettingsService>(true);
            SimpleIoc.Default.Register(() =>
            {
                var steamClientService =
                    new SteamClientService(ServiceLocator.Current.GetInstance<ApplicationSettingsService>());
                steamClientService.Start();
                return steamClientService;
            });
        }

        public MainWindowViewModel MainWindow
        {
            get { return ServiceLocator.Current.GetInstance<MainWindowViewModel>(); }
        }

        public WelcomePageViewModel WelcomePage
        {
            get { return ServiceLocator.Current.GetInstance<WelcomePageViewModel>(); }
        }

        public LoginPageViewModel LoginPage
        {
            get { return ServiceLocator.Current.GetInstance<LoginPageViewModel>(); }
        }

        public FriendListPageViewModel FriendListPage
        {
            get { return ServiceLocator.Current.GetInstance<FriendListPageViewModel>(); }
        }
    }
}