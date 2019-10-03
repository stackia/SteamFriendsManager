using GalaSoft.MvvmLight.Messaging;
using GalaSoft.MvvmLight.Threading;
using MahApps.Metro.Controls.Dialogs;

namespace SteamFriendsManager
{
    public partial class MainWindow
    {
        public MainWindow()
        {
            InitializeComponent();

            Messenger.Default.Register<ShowMessageDialogMessage>(this,
                msg =>
                {
                    DispatcherHelper.CheckBeginInvokeOnUI(
                        async () => { await this.ShowMessageAsync(msg.Title, msg.Message); });
                });

            Messenger.Default.Register<ShowMessageDialogMessageWithCallback>(this,
                msg =>
                {
                    DispatcherHelper.CheckBeginInvokeOnUI(
                        async () => { msg.Execute(await this.ShowMessageAsync(msg.Title, msg.Message, msg.Style)); });
                });

            Messenger.Default.Register<ShowInputDialogMessage>(this,
                msg =>
                {
                    DispatcherHelper.CheckBeginInvokeOnUI(
                        async () =>
                        {
                            if (msg.DefaultText != null)
                                msg.Execute(await this.ShowInputAsync(msg.Title, msg.Message, new MetroDialogSettings
                                {
                                    DefaultText = msg.DefaultText
                                }));
                            else
                                msg.Execute(await this.ShowInputAsync(msg.Title, msg.Message));
                        });
                });
        }
    }
}