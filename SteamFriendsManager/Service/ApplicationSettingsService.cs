using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace SteamFriendsManager.Service
{
    public class ApplicationSettingsService
    {
        private const string SettingsFileName = "settings.json";

        public ApplicationSettingsService()
        {
            Load();
        }

        public ApplicationSettings Settings { get; private set; }

        public void Save()
        {
            FileStream settingsFileStream = null;
            try
            {
                settingsFileStream = File.Open(Path.Combine(Environment.CurrentDirectory, SettingsFileName),
                    FileMode.Create, FileAccess.Write);
                using (var streamWriter = new StreamWriter(settingsFileStream))
                {
                    settingsFileStream = null;

                    var jsonSerializer = new JsonSerializer();
                    jsonSerializer.Serialize(streamWriter, Settings);
                }
            }
            finally
            {
                if (settingsFileStream != null)
                    settingsFileStream.Dispose();
            }
        }

        public Task SaveAsync()
        {
            return Task.Run(() => Save());
        }

        public void Load()
        {
            FileStream settingsFileStream = null;
            try
            {
                settingsFileStream = File.Open(Path.Combine(Environment.CurrentDirectory, SettingsFileName),
                    FileMode.OpenOrCreate, FileAccess.Read);
                StreamReader streamReader = null;
                try
                {
                    streamReader = new StreamReader(settingsFileStream);
                    settingsFileStream = null;
                    using (var jsonTextReader = new JsonTextReader(streamReader))
                    {
                        streamReader = null;

                        var jsonSerializer = new JsonSerializer();
                        Settings = jsonSerializer.Deserialize<ApplicationSettings>(jsonTextReader) ??
                                   new ApplicationSettings();
                    }
                }
                finally
                {
                    if (streamReader != null)
                        streamReader.Dispose();
                }
            }
            finally
            {
                if (settingsFileStream != null)
                    settingsFileStream.Dispose();
            }
        }

        public Task LoadAsync()
        {
            return Task.Run(() => Load());
        }

        public class ApplicationSettings
        {
            public bool ShouldRememberAccount { get; set; }
            public string LastUsername { get; set; }
            public string LastPassword { get; set; }
            public Dictionary<string, byte[]> SentryHashStore { get; set; }
            public List<string> PreferedCmServers { get; set; }
        }
    }
}