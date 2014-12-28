using System.Collections.Generic;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
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

        private RelayCommand _switchAccount;

        public RelayCommand SwitchAccount
        {
            get
            {
                return _switchAccount ?? (_switchAccount = new RelayCommand(() =>
                {
                    MessengerInstance.Send(new ClearPageHistoryOnNextTryLoginMessage());
                    MessengerInstance.Send(new SwitchPageMessage(SwitchPageMessage.Page.Login));
                }));
            }
        }
    }
}