using System;
using System.Globalization;
using System.Windows.Data;

namespace SteamFriendsManager.Converter
{
    [ValueConversion(typeof(byte[]), typeof(string))]
    public class SteamFriendAvatarConverter : IValueConverter
    {
        private const string DefaultAvatar =
            "http://cdn.akamai.steamstatic.com/steamcommunity/public/images/avatars/fe/fef49e7fa7e1997310d705b2a6158ff8dc1cdfeb_medium.jpg";

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return DefaultAvatar;

            var avatarHash = BitConverter.ToString((byte[]) value).Replace("-", "").ToLower();
            return string.IsNullOrEmpty(avatarHash) || string.IsNullOrEmpty(avatarHash.Replace("0", ""))
                ? DefaultAvatar
                : $"http://cdn.akamai.steamstatic.com/steamcommunity/public/images/avatars/{avatarHash.Substring(0, 2)}/{avatarHash}_medium.jpg";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}