using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using TT_Lab.Util;

namespace TT_Lab
{
    /// <summary>
    /// Stores information about user's preferences and settings
    /// </summary>
    // TODO: Move into IoC container
    public static class Preferences
    {
        /// <summary>
        /// This method is static so don't forget to unsubscribe from it anywhere where you subscribe to it otherwise memory leaks will happen
        /// </summary>
        public static event EventHandler<PreferenceChangedArgs>? PreferenceChanged;

        private static readonly Dictionary<string, object> Settings = new();
        private static readonly string ExePath = ManifestResourceLoader.GetPathInExe("");
        private static readonly string PrefFileName = "settings.json";
        private static readonly string PrefFilePath;

        // List of Named settings
        public const string SillinessEnabled = "SillinessEnabled";
        public const string Ps2DiscContentPath = "PS2DiscContentPath";
        public const string XboxDiscContentPath = "XboxDiscContentPath";
        public const string ProjectsPath = "ProjectsPath";

        static Preferences()
        {
            PrefFilePath = Path.Combine(ExePath, PrefFileName);
            Settings[SillinessEnabled] = true;
            Settings[Ps2DiscContentPath] = ExePath;
            Settings[XboxDiscContentPath] = ExePath;
            Settings[ProjectsPath] = ExePath;
        }

        public static void Save()
        {
            using FileStream settings = File.Create(PrefFilePath);
            using BinaryWriter writer = new(settings);
            writer.Write(JsonConvert.SerializeObject(Settings, Formatting.Indented).ToCharArray());
            writer.Flush();
            settings.Flush(true);
        }

        public static void Load()
        {
            if (File.Exists(PrefFilePath))
            {
                using FileStream settings = new(PrefFilePath, FileMode.Open, FileAccess.Read);
                using StreamReader reader = new(settings);
                try
                {
                    JsonConvert.PopulateObject(reader.ReadToEnd(), Settings);
                }
                catch (Exception)
                {
                    // ignored
                }
            }
        }

        public static void SetPreference(string prefName, Boolean value)
        {
            if ((Boolean)Settings[prefName] == value)
            {
                return;
            }
            
            Settings[prefName] = value;
            PreferenceChanged?.Invoke(null, new PreferenceChangedArgs { PreferenceName = prefName });
        }

        public static void SetPreference<T>(string prefName, T value) where T : class
        {
            if (Settings[prefName] == value)
            {
                return;
            }

            Settings[prefName] = value;
            PreferenceChanged?.Invoke(null, new PreferenceChangedArgs { PreferenceName = prefName });
        }

        public static T GetPreference<T>(string prefName)
        {
            var retT = typeof(T);
            if (retT.IsEnum)
            {
                return MiscUtils.ConvertEnum<T>(Settings[prefName])!;
            }
            return (T)Settings[prefName];
        }

        public class PreferenceChangedArgs : EventArgs
        {
            public string PreferenceName { get; set; } = "";
        }
    }
}
