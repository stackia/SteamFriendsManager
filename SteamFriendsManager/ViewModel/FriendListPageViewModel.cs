using System;
using System.Collections.Generic;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using SteamFriendsManager.Service;

namespace SteamFriendsManager.ViewModel
{
    public class FriendListPageViewModel : ViewModelBase
    {
        private RelayCommand _changePersonalName;
        private RelayCommand _refreshFriends;
        private RelayCommand _switchAccount;
        private readonly SteamClientService _steamClientService;

        public FriendListPageViewModel(SteamClientService steamClientService)
        {
            _steamClientService = steamClientService;
            _steamClientService.PropertyChanged += (sender, args) =>
            {
                switch (args.PropertyName)
                {
                    case "PersonalName":
                        RaisePropertyChanged(() => PersonalName);
                        break;
                }
            };
        }

        public IReadOnlyList<SteamClientService.Friend> Friends
        {
            get { return _steamClientService.Friends; }
        }

        public string PersonalName
        {
            get { return _steamClientService.PersonalName; }
            set
            {
                if (_steamClientService.PersonalName == value)
                    return;

                _steamClientService.PersonalName = value;
            }
        }

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

        public RelayCommand RefreshFrineds
        {
            get
            {
                return _refreshFriends ??
                       (_refreshFriends = new RelayCommand(() => { RaisePropertyChanged(() => Friends); }));
            }
        }

        public RelayCommand ChangePersonalName
        {
            get
            {
                return _changePersonalName ?? (_changePersonalName = new RelayCommand(() =>
                {
                    MessengerInstance.Send(new ShowInputDialogMessage("修改昵称", "请输入新昵称：", PersonalName, async s =>
                    {
                        if (s == null || s == PersonalName)
                            return;

                        if (string.IsNullOrWhiteSpace(s))
                        {
                            MessengerInstance.Send(new ShowMessageDialogMessage("修改失败", "你不能使用空的昵称。"));
                            return;
                        }

                        try
                        {
                            await _steamClientService.SetPersonalNameAsync(s);
                        }
                        catch (TimeoutException)
                        {
                            MessengerInstance.Send(new ShowMessageDialogMessage("昵称修改失败", "连接超时，请重试。"));
                        }
                    }));
                }));
            }
        }
    }
}