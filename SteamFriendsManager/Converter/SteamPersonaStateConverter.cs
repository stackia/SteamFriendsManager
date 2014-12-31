using System;
using System.Globalization;
using System.Windows.Data;
using SteamKit2;

namespace SteamFriendsManager.Converter
{
    [ValueConversion(typeof (string), typeof (EPersonaState))]
    public class SteamPersonaStateConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var stateString = value as string;
            if (stateString == null)
                throw new ArgumentException("Cannot convert null value.");

            EPersonaState state;
            if (Enum.TryParse(stateString, true, out state))
                return state;
            throw new ArgumentException("Cannot convert this value.");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var state = value as EPersonaState?;
            if (state == null)
                throw new ArgumentException("Cannot convert back null value.");

            return state.ToString();
        }
    }
}