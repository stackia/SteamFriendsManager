using System;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace SteamFriendsManager.Utility
{
    public static class UriExtension
    {
        public static bool CheckSchemeExistance(this Uri uri)
        {
            var schemeKey = Registry.ClassesRoot.OpenSubKey(uri.Scheme);
            return schemeKey != null && schemeKey.GetValue("URL Protocol") != null;
        }

        public static string GetSchemeExecutable(this Uri uri)
        {
            var commandKey = Registry.ClassesRoot.OpenSubKey($@"{uri.Scheme}\Shell\Open\Command");

            var command = commandKey?.GetValue(null) as string;
            if (command == null)
                return null;

            return Regex.Match(command, @"(?<="").*?(?="")").Value;
        }
    }
}