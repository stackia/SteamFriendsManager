using System;
using System.Globalization;
using System.Windows.Data;
using SteamKit2;

namespace SteamFriendsManager.Converter
{
    [ValueConversion(typeof(EPersonaState), typeof (string))]
    public class SteamPersonaStateDisplayNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is EPersonaState state))
                throw new ArgumentException("Cannot convert null value.");

            switch (state)
            {
                case EPersonaState.Offline:
                    return "离线";

                case EPersonaState.Online:
                    return "在线";

                case EPersonaState.Busy:
                    return "忙碌";

                case EPersonaState.Away:
                    return "离开";

                case EPersonaState.Snooze:
                    return "打盹";

                case EPersonaState.LookingToTrade:
                    return "想交易";

                case EPersonaState.LookingToPlay:
                    return "想玩游戏";

                case EPersonaState.Invisible:
                    return "隐身";

                default:
                    return state.ToString();
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}