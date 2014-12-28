using System;
using System.Collections;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Threading;
using SteamFriendsManager.Page;
using SteamFriendsManager.Service;

namespace SteamFriendsManager.ViewModel
{
    public class MainWindowViewModel : ViewModelBase
    {
        private object _currentPage;
        private RelayCommand _stopSteamService;
        private RelayCommand _switchBack;
        private readonly SteamClientService _steamClientService;

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

        private Stack PageHistory { get; set; }

        public object CurrentPage
        {
            get { return _currentPage; }
            set
            {
                if (_currentPage == value)
                    return;

                _currentPage = value;

                RaisePropertyChanged(() => CurrentPage);
            }
        }

        public bool SwitchBackButtonVisible
        {
            get { return PageHistory.Count > 1; }
        }

        public RelayCommand SwitchBack
        {
            get
            {
                return _switchBack ?? (_switchBack = new RelayCommand(() =>
                {
                    if (PageHistory.Count <= 1)
                        return;

                    PageHistory.Pop();
                    CurrentPage = PageHistory.Peek();
                    RaisePropertyChanged(() => SwitchBackButtonVisible);
                }));
            }
        }

        public RelayCommand StopSteamService
        {
            get
            {
                return _stopSteamService ??
                       (_stopSteamService = new RelayCommand(async () => { await _steamClientService.StopAsync(); }));
            }
        }
    }
}