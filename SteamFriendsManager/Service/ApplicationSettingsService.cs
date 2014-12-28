using System.Collections.Generic;
using System.IO;
using System.IO.IsolatedStorage;
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
            var storage = IsolatedStorageFile.GetUserStoreForApplication();
            IsolatedStorageFileStream settingsFileStream = null;
            try
            {
                settingsFileStream = new IsolatedStorageFileStream(SettingsFileName, FileMode.OpenOrCreate,
                    FileAccess.ReadWrite, storage);
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
            var storage = IsolatedStorageFile.GetUserStoreForApplication();

            IsolatedStorageFileStream settingsFileStream = null;
            try
            {
                settingsFileStream = new IsolatedStorageFileStream(SettingsFileName, FileMode.OpenOrCreate,
                    FileAccess.ReadWrite, storage);
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
        }
    }
}