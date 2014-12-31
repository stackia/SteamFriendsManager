using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using MahApps.Metro.Controls.Dialogs;
using SteamFriendsManager.Service;
using SteamFriendsManager.Utility;
using SteamKit2;

namespace SteamFriendsManager.ViewModel
{
    public class FriendListPageViewModel : ViewModelBase
    {
        private RelayCommand _changePersonalName;
        private RelayCommand<IList> _removeFriend;
        private RelayCommand<IList> _sendChatMessage;
        private RelayCommand _switchAccount;
        private readonly SteamClientService _steamClientService;

        public FriendListPageViewModel(SteamClientService steamClientService)
        {
            _steamClientService = steamClientService;
            MessengerInstance.Register<PersonalNameChangedMessage>(this,
                msg => { RaisePropertyChanged(() => PersonalName); });
        }

        public ObservableCollection<SteamClientService.Friend> Friends
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

        public RelayCommand<IList> SendChatMessage
        {
            get
            {
                return _sendChatMessage ?? (_sendChatMessage = new RelayCommand<IList>(friends =>
                {
                    if (friends == null || friends.Count == 0)
                        return;

                    if (friends.Count == 1)
                    {
                        var friend = friends[0] as SteamClientService.Friend;
                        if (friend == null)
                            return;

                        var uri = new Uri("steam:");
                        if (uri.CheckSchemeExistance())
                        {
                            Process.Start(uri.GetSchemeExecutable(),
                                string.Format("steam://friends/message/{0}", friend.SteamId.ConvertToUInt64()));
                        }
                        else
                        {
                            MessengerInstance.Send(new ShowInputDialogMessage("发送消息", "请输入内容：",
                                s =>
                                {
                                    _steamClientService.SendChatMessageAsync(friend.SteamId, EChatEntryType.ChatMsg, s);
                                }));
                        }
                    }
                    else
                    {
                        MessengerInstance.Send(new ShowInputDialogMessage("群发消息", "请输入内容：", async s =>
                        {
                            if (string.IsNullOrEmpty(s))
                                return;

                            if (await Task<bool>.Factory.StartNew(() =>
                            {
                                var sendTasks = from friend in friends.OfType<SteamClientService.Friend>()
                                    select _steamClientService.SendChatMessageAsync(friend.SteamId,
                                        EChatEntryType.ChatMsg, string.Format("{0} ♥", s));
                                try
                                {
                                    Task.WaitAll(sendTasks.ToArray());
                                    return true;
                                }
                                catch (AggregateException)
                                {
                                    return false;
                                }
                            }))
                            {
                                MessengerInstance.Send(new ShowMessageDialogMessage("群发成功", "所有消息都已成功送达！"));
                            }
                            else
                            {
                                MessengerInstance.Send(new ShowMessageDialogMessage("群发失败", "部分消息发送超时。"));
                            }
                        }));
                    }
                }));
            }
        }

        public RelayCommand<IList> RemoveFriend
        {
            get
            {
                return _removeFriend ?? (_removeFriend = new RelayCommand<IList>(friends =>
                {
                    if (friends == null || friends.Count == 0)
                        return;

                    MessengerInstance.Send(new ShowMessageDialogMessageWithCallback("删除好友", "你确认要删除选定好友吗？",
                        MessageDialogStyle.AffirmativeAndNegative, async result =>
                        {
                            if (result == MessageDialogResult.Negative)
                                return;

                            if (await Task<bool>.Factory.StartNew(() =>
                            {
                                var removeTasks = from friend in friends.OfType<SteamClientService.Friend>()
                                    select _steamClientService.RemoveFriendAsync(friend.SteamId);
                                try
                                {
                                    Task.WaitAll(removeTasks.ToArray());
                                    return true;
                                }
                                catch (AggregateException)
                                {
                                    return false;
                                }
                            }))
                            {
                                MessengerInstance.Send(new ShowMessageDialogMessage("删除成功", "选定好友删除成功。"));
                            }
                            else
                            {
                                MessengerInstance.Send(new ShowMessageDialogMessage("删除失败", "部分好友删除请求发送超时。"));
                            }
                        }));
                }));
            }
        }
    }
}