using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using MahApps.Metro.Controls.Dialogs;
using SteamFriendsManager.Service;
using SteamKit2;

namespace SteamFriendsManager.ViewModel
{
    public class FriendListPageViewModel : ViewModelBase
    {
        private readonly SteamClientService _steamClientService;
        private RelayCommand _addFriend;
        private RelayCommand _changePersonaName;
        private RelayCommand<IList> _removeFriend;
        private string _searchText;
        private RelayCommand<IList> _sendChatMessage;
        private RelayCommand _switchAccount;
        private RelayCommand<EPersonaState> _switchPersonaState;

        public FriendListPageViewModel(SteamClientService steamClientService)
        {
            _steamClientService = steamClientService;

            Task.Delay(2000).ContinueWith(task => { _steamClientService.SetPersonaStateAsync(EPersonaState.Online); });

            MessengerInstance.Register<PersonaNameChangedMessage>(this,
                msg => { RaisePropertyChanged(() => PersonaName); });

            MessengerInstance.Register<PersonaStateChangedMessage>(this,
                msg => RaisePropertyChanged(() => PersonaState));

            MessengerInstance.Register<ReconnectFailedMessage>(this, msg =>
            {
                MessengerInstance.Send(new ShowMessageDialogMessageWithCallback("连接中断",
                    "你与 Steam 的服务器连接已中断。重试三次均无法连通，请检查网络后重新登录。",
                    result =>
                    {
                        MessengerInstance.Send(new SwitchPageMessage(SwitchPageMessage.Page.Login));
                        MessengerInstance.Send(new ClearPageHistoryMessage());
                    }));
            });
        }

        public IEnumerable<SteamClientService.Friend> Friends => _steamClientService.Friends;

        public string PersonaName => _steamClientService.PersonaName;

        public string PersonaState
        {
            get
            {
                var state = _steamClientService.PersonaState;
                return state switch
                {
                    EPersonaState.Offline => "离线",
                    EPersonaState.Online => "在线",
                    EPersonaState.Busy => "忙碌",
                    EPersonaState.Away => "离开",
                    EPersonaState.Snooze => "打盹",
                    EPersonaState.LookingToTrade => "想交易",
                    EPersonaState.LookingToPlay => "想玩游戏",
                    EPersonaState.Invisible => "隐身",
                    _ => state.ToString()
                };
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText == value)
                    return;

                _searchText = value;
                RaisePropertyChanged(() => SearchText);

                if (!string.IsNullOrEmpty(_searchText))
                    foreach (var friend in _steamClientService.Friends)
                        friend.Visible = friend.PersonaName.ToLower().Contains(_searchText.ToLower());
                else
                    foreach (var friend in _steamClientService.Friends)
                        friend.Visible = true;
            }
        }

        public RelayCommand SwitchAccount =>
            _switchAccount ??= new RelayCommand(() =>
            {
                MessengerInstance.Send(new ClearPageHistoryOnNextTryLoginMessage());
                MessengerInstance.Send(new LogoutOnNextTryLoginMessage());
                MessengerInstance.Send(new SwitchPageMessage(SwitchPageMessage.Page.Login));
            });

        public RelayCommand ChangePersonaName =>
            _changePersonaName ??= new RelayCommand(() =>
            {
                MessengerInstance.Send(new ShowInputDialogMessage("修改昵称", "请输入新昵称：", PersonaName, async s =>
                {
                    if (s == null || s == PersonaName)
                        return;

                    if (string.IsNullOrWhiteSpace(s))
                    {
                        MessengerInstance.Send(new ShowMessageDialogMessage("修改失败", "你不能使用空的昵称。"));
                        return;
                    }

                    try
                    {
                        await _steamClientService.SetPersonaNameAsync(s);
                    }
                    catch (TimeoutException)
                    {
                        MessengerInstance.Send(new ShowMessageDialogMessage("昵称修改失败", "连接超时，请重试。"));
                    }
                }));
            });

        public RelayCommand<EPersonaState> SwitchPersonaState =>
            _switchPersonaState ??= new RelayCommand<EPersonaState>(async state =>
            {
                try
                {
                    await _steamClientService.SetPersonaStateAsync(state);
                }
                catch (TimeoutException)
                {
                    MessengerInstance.Send(new ShowMessageDialogMessage("切换状态失败", "连接超时，请重试。"));
                }
            });

        public RelayCommand<IList> SendChatMessage =>
            _sendChatMessage ??= new RelayCommand<IList>(friends =>
            {
                if (friends == null || friends.Count == 0)
                    return;

                if (friends.Count == 1)
                {
                    var friend = friends[0] as SteamClientService.Friend;
                    if (friend == null)
                        return;

                    MessengerInstance.Send(new ShowInputDialogMessage("发送消息", "请输入内容：",
                        s =>
                        {
                            _steamClientService.SendChatMessageAsync(friend.SteamId, EChatEntryType.ChatMsg, s);
                        }));
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
                                    EChatEntryType.ChatMsg, $"{s} ♥");
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
                            MessengerInstance.Send(new ShowMessageDialogMessage("群发成功", "所有消息都已成功送达！"));
                        else
                            MessengerInstance.Send(new ShowMessageDialogMessage("群发失败", "部分消息发送超时。"));
                    }));
                }
            });

        public RelayCommand AddFriend =>
            _addFriend ??= new RelayCommand(() =>
            {
                MessengerInstance.Send(new ShowInputDialogMessage("添加好友", @"请输入你要添加好友ID，当前支持以下几种格式：
* 对方用户名
* STEAM_0:0:19874118
* 76561198000013964
* http://steamcommunity.com/profiles/76561198000013964",
                    async s =>
                    {
                        if (s == null)
                            return;

                        if (string.IsNullOrWhiteSpace(s))
                            MessengerInstance.Send(new ShowMessageDialogMessage("添加失败", "输入无效"));

                        try
                        {
                            SteamFriends.FriendAddedCallback result;
                            do
                            {
                                if (Regex.IsMatch(s, @"^7656\d{13}$"))
                                {
                                    ulong.TryParse(s, out var steamId64);
                                    var steamId = new SteamID();
                                    steamId.SetFromUInt64(steamId64);
                                    result = await _steamClientService.AddFriendAsync(steamId64);
                                    break;
                                }

                                var match = Regex.Match(s,
                                    @"^http(?:s)?://steamcommunity.com/profiles/(7656\d{13})$");
                                if (match.Success)
                                {
                                    ulong.TryParse(match.Groups[1].Value, out var steamId64);
                                    var steamId = new SteamID();
                                    steamId.SetFromUInt64(steamId64);
                                    result = await _steamClientService.AddFriendAsync(steamId);
                                    break;
                                }

                                if (Regex.IsMatch(s, @"^(?i)STEAM(?-i)_\d:\d:\d{8}$"))
                                {
                                    result = await _steamClientService.AddFriendAsync(new SteamID(s));
                                    break;
                                }

                                result = await _steamClientService.AddFriendAsync(s);
                            } while (false);

                            if (result == null)
                                return;

                            switch (result.Result)
                            {
                                case EResult.OK:
                                    MessengerInstance.Send(new ShowMessageDialogMessage("添加成功",
                                        $"你成功向 {result.PersonaName} 发出了好友请求。"));
                                    break;

                                case EResult.DuplicateName:
                                    MessengerInstance.Send(new ShowMessageDialogMessage("添加失败", "对方已经是你的好友了。"));
                                    break;

                                case EResult.AccountNotFound:
                                    MessengerInstance.Send(new ShowMessageDialogMessage("添加失败", "指定用户不存在。"));
                                    break;

                                default:
                                    MessengerInstance.Send(new ShowMessageDialogMessage("添加失败",
                                        result.Result.ToString()));
                                    break;
                            }
                        }
                        catch (TimeoutException)
                        {
                            MessengerInstance.Send(new ShowMessageDialogMessage("添加失败", "连接超时。"));
                        }
                    }));
            });

        public RelayCommand<IList> RemoveFriend =>
            _removeFriend ??= new RelayCommand<IList>(friends =>
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
                            MessengerInstance.Send(new ShowMessageDialogMessage("删除成功", "选定好友删除成功。"));
                        else
                            MessengerInstance.Send(new ShowMessageDialogMessage("删除失败", "部分好友删除请求发送超时。"));
                    }));
            });
    }
}