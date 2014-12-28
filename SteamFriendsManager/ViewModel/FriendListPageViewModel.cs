using System.Collections.Generic;
using GalaSoft.MvvmLight;
using SteamFriendsManager.Service;

namespace SteamFriendsManager.ViewModel
{
    public class FriendListPageViewModel : ViewModelBase
    {
        private readonly SteamClientService _steamClientService;

        public FriendListPageViewModel(SteamClientService steamClientService)
        {
            _steamClientService = steamClientService;
        }

        public IReadOnlyList<SteamClientService.Friend> Friends
        {
            get { return _steamClientService.Friends; }
        }
    }
}