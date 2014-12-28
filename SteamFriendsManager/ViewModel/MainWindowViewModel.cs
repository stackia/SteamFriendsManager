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
                    RaiseSwitchBackCanExecuteChanged();
                });
            });

            MessengerInstance.Register<ClearPageHistoryMessage>(this, msg =>
            {
                DispatcherHelper.CheckBeginInvokeOnUI(() =>
                {
                    PageHistory.Clear();
                    PageHistory.Push(CurrentPage);
                    RaiseSwitchBackCanExecuteChanged();
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
                    PageHistory.Pop();
                    CurrentPage = PageHistory.Peek();
                    RaiseSwitchBackCanExecuteChanged();
                },
                    () => SwitchBackButtonVisible));
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

        private void RaiseSwitchBackCanExecuteChanged()
        {
            RaisePropertyChanged(() => SwitchBackButtonVisible);
            SwitchBack.RaiseCanExecuteChanged();
        }
    }
}