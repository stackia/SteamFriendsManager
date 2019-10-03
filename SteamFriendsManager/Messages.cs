using System;
using GalaSoft.MvvmLight.Messaging;
using MahApps.Metro.Controls.Dialogs;
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

        public Page TargetPage { get; }
        public bool ClearPageHistory { get; }

        public static Type GetPageType(Page pageEnum)
        {
            return pageEnum switch
            {
                Page.Login => typeof(LoginPage),
                Page.Welcome => typeof(WelcomePage),
                Page.FriendList => typeof(FriendListPage),
                _ => throw new ArgumentOutOfRangeException(nameof(pageEnum))
            };
        }
    }

    internal class ClearPageHistoryMessage : MessageBase
    {
    }

    internal class ClearPageHistoryOnNextTryLoginMessage : MessageBase
    {
    }

    internal class LogoutOnNextTryLoginMessage : MessageBase
    {
    }

    internal class ReconnectFailedMessage : MessageBase
    {
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

    internal class ShowMessageDialogMessageWithCallback : NotificationMessageAction<MessageDialogResult>,
        IMessageDialogMessage
    {
        public ShowMessageDialogMessageWithCallback(string title, string message, Action<MessageDialogResult> callback)
            : this(title, message, MessageDialogStyle.Affirmative, callback)
        {
        }

        public ShowMessageDialogMessageWithCallback(string title, string message, MessageDialogStyle style,
            Action<MessageDialogResult> callback)
            : base(null, callback)
        {
            Title = title;
            Message = message;
            Style = style;
        }

        public MessageDialogStyle Style { get; }
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

        public ShowInputDialogMessage(string title, string message, string defaultText, Action<string> callback)
            : base(null, callback)
        {
            Title = title;
            Message = message;
            DefaultText = defaultText;
        }

        public string DefaultText { get; }
        public string Title { get; set; }
        public string Message { get; set; }
    }

    internal class PersonaNameChangedMessage : MessageBase
    {
    }

    internal class PersonaStateChangedMessage : MessageBase
    {
    }
}