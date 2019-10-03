using System;
using System.Globalization;
using System.Windows.Data;
using SteamKit2;

namespace SteamFriendsManager.Converter
{
    [ValueConversion(typeof(EPersonaState), typeof(string))]
    public class SteamPersonaStateDisplayNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is EPersonaState state))
                throw new ArgumentException("Cannot convert null value.");

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

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}