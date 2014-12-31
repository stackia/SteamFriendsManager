using System;
using System.Globalization;
using System.Windows.Data;

namespace SteamFriendsManager.Converter
{
    [ValueConversion(typeof (bool), typeof (double))]
    public class BooleanToOpacityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter,
            CultureInfo culture)
        {
            return (bool) value ? 1.0 : 0.1;
        }

        public object ConvertBack(object value, Type targetType, object parameter,
            CultureInfo culture)
        {
            return Math.Abs((double) value - 1.0) < 0.05;
        }
    }
}