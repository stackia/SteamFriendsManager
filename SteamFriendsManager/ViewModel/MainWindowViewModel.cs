using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Threading;
using MahApps.Metro.Controls.Dialogs;
using Newtonsoft.Json;
using SteamFriendsManager.Page;
using SteamFriendsManager.Service;

namespace SteamFriendsManager.ViewModel
{
    public class MainWindowViewModel : ViewModelBase
    {
        private readonly SteamClientService _steamClientService;
        private RelayCommand _checkForNewVersion;
        private object _currentPage;
        private RelayCommand _stopSteamService;
        private RelayCommand _switchBack;

        public MainWindowViewModel(SteamClientService steamClientService)
        {
            _steamClientService = steamClientService;

            _currentPage = new WelcomePage();
            PageHistory = new Stack();
            PageHistory.Push(CurrentPage);

            MessengerInstance.Register<SwitchPageMessage>(this, msg =>
            {
                DispatcherHelper.CheckBeginInvokeOnUI(() =>
                {
                    CurrentPage = Activator.CreateInstance(SwitchPageMessage.GetPageType(msg.TargetPage));
                    if (msg.ClearPageHistory)
                    {
                        PageHistory.Clear();
                        PageHistory.Push(CurrentPage);
                    }
                    else
                    {
                        PageHistory.Push(CurrentPage);
                    }

                    RaisePropertyChanged(() => SwitchBackButtonVisible);
                });
            });

            MessengerInstance.Register<ClearPageHistoryMessage>(this, msg =>
            {
                DispatcherHelper.CheckBeginInvokeOnUI(() =>
                {
                    PageHistory.Clear();
                    PageHistory.Push(CurrentPage);
                    RaisePropertyChanged(() => SwitchBackButtonVisible);
                });
            });
        }

        private Stack PageHistory { get; }

        public object CurrentPage
        {
            get => _currentPage;
            set
            {
                if (_currentPage == value)
                    return;

                _currentPage = value;

                RaisePropertyChanged(() => CurrentPage);
            }
        }

        public bool SwitchBackButtonVisible => PageHistory.Count > 1;

        public RelayCommand SwitchBack =>
            _switchBack ??= new RelayCommand(() =>
            {
                if (PageHistory.Count <= 1)
                    return;

                PageHistory.Pop();
                CurrentPage = PageHistory.Peek();
                RaisePropertyChanged(() => SwitchBackButtonVisible);
            });

        public RelayCommand StopSteamService => _stopSteamService ??= new RelayCommand(async () => { await _steamClientService.StopAsync(); });

        public RelayCommand CheckForNewVersion =>
            _checkForNewVersion ??= new RelayCommand(async () =>
            {
                try
                {
                    var request = WebRequest.Create("http://steamcn.com/sfm_version_metadata.json");
                    var response = await request.GetResponseAsync();
                    var responseStream = response.GetResponseStream();
                    if (responseStream == null)
                        return;
                    using var reader = new StreamReader(responseStream);
                    dynamic versionMetadata = JsonConvert.DeserializeObject(await reader.ReadToEndAsync());
                    var version = Version.Parse(versionMetadata.LatestVersion.ToString());
                    if (version > Assembly.GetExecutingAssembly().GetName().Version)
                        MessengerInstance.Send(new ShowMessageDialogMessageWithCallback("更新提示",
                            $"现在有新版（v{version as Version}）可用，是否要前往下载？",
                            MessageDialogStyle.AffirmativeAndNegative,
                            result =>
                            {
                                if (result != MessageDialogResult.Affirmative)
                                    return;

                                Process.Start(new ProcessStartInfo(versionMetadata.DownloadUrl.ToString())
                                    {UseShellExecute = true});
                            }));
                }
                catch (WebException)
                {
                }
            });
    }
}