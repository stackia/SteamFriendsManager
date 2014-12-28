using System;
using GalaSoft.MvvmLight.Messaging;
using SteamFriendsManager.Page;

namespace SteamFriendsManager
{
    internal class SwitchPageMessage : MessageBase
    {
        public enum Page
        {
            Login,
            Welcome,
            FriendList
        }

        public SwitchPageMessage(Page targetPage, bool clearPageHistory = false)
        {
            TargetPage = targetPage;
            ClearPageHistory = clearPageHistory;
        }

        public Page TargetPage { get; private set; }
        public bool ClearPageHistory { get; private set; }

        public static Type GetPageType(Page pageEnum)
        {
            switch (pageEnum)
            {
                case Page.Login:
                    return typeof (LoginPage);

                case Page.Welcome:
                    return typeof (WelcomePage);

                case Page.FriendList:
                    return typeof (FriendListPage);

                default:
                    throw new ArgumentOutOfRangeException("pageEnum");
            }
        }
    }

    internal interface IMessageDialogMessage
    {
        string Title { get; set; }
        string Message { get; set; }
    }

    internal class ShowMessageDialogMessage : IMessageDialogMessage
    {
        public ShowMessageDialogMessage(string title, string message)
        {
            Title = title;
            Message = message;
        }

        public string Title { get; set; }
        public string Message { get; set; }
    }

    internal class ShowMessageDialogMessageWithCallback : NotificationMessageAction, IMessageDialogMessage
    {
        public ShowMessageDialogMessageWithCallback(string title, string message, Action callback)
            : base(null, callback)
        {
            Title = title;
            Message = message;
        }

        public string Title { get; set; }
        public string Message { get; set; }
    }

    internal class ShowInputDialogMessage : NotificationMessageAction<string>, IMessageDialogMessage
    {
        public ShowInputDialogMessage(string title, string message, Action<string> callback) : base(null, callback)
        {
            Title = title;
            Message = message;
        }

        public string Title { get; set; }
        public string Message { get; set; }
    }
}