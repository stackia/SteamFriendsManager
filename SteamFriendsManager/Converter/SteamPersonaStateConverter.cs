using System;
using System.Globalization;
using System.Windows.Data;
using SteamKit2;

namespace SteamFriendsManager.Converter
{
    [ValueConversion(typeof(string), typeof(EPersonaState))]
    public class SteamPersonaStateConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is string stateString))
                throw new ArgumentException("Cannot convert null value.");

            if (Enum.TryParse(stateString, true, out EPersonaState state))
                return state;
            throw new ArgumentException("Cannot convert this value.");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is EPersonaState state))
                throw new ArgumentException("Cannot convert back null value.");

            return ((EPersonaState?) state).ToString();
        }
    }
}