using GalaSoft.MvvmLight.Threading;

namespace SteamFriendsManager
{
    public partial class App
    {
        public App()
        {
            DispatcherHelper.Initialize();
        }
    }
}