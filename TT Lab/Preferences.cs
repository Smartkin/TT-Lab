using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
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
        public const string ViewportSnapping = "ViewportSnapping";
        public const string ViewportTranslationSnap = "ViewportTranslationSnap";
        public const string ViewportRotationSnap = "ViewportRotationSnap";
        public const string ViewportScaleSnap = "ViewportScaleSnap";
        public const string ViewportGridShown = "ViewportGridShown";
        // Megabytes of memory builds keep TT Lab within, chunks only build in parallel while there's room for more
        public const string BuildMemoryBudget = "BuildMemoryBudget";
        // Folders of the projects opened last, newest first
        public const string RecentProjects = "RecentProjects";
        public const string LastBuildProfile = "LastBuildProfile";
        // PCSX2's executable, or its Flatpak's ID, found on its own when empty
        public const string Pcsx2Path = "Pcsx2Path";
        // The disc image PCSX2 boots a chunk with, the project's own when empty
        public const string Pcsx2DiscImage = "Pcsx2DiscImage";
        public const string Pcsx2ReloadOnSave = "Pcsx2ReloadOnSave";
        // Arguments PCSX2 gets besides TT Lab's own, one line split like a shell's
        public const string Pcsx2Arguments = "Pcsx2Arguments";
        // Whether the OGI viewers draw the model's skeleton over it
        public const string OgiViewerSkeleton = "OgiViewerSkeleton";
        // Whether Discord shows what's done in TT Lab (Tools/Discord)
        public const string DiscordRichPresence = "DiscordRichPresence";
        // The Preferences window's sections left collapsed, by their titles
        public const string CollapsedPreferenceSections = "CollapsedPreferenceSections";

        static Preferences()
        {
            PrefFilePath = Path.Combine(ExePath, PrefFileName);
            Settings[SillinessEnabled] = true;
            Settings[Ps2DiscContentPath] = ExePath;
            Settings[XboxDiscContentPath] = ExePath;
            Settings[ProjectsPath] = ExePath;
            Settings[ViewportSnapping] = false;
            Settings[ViewportTranslationSnap] = 1.0;
            Settings[ViewportRotationSnap] = 15.0;
            Settings[ViewportScaleSnap] = 0.1;
            Settings[ViewportGridShown] = true;
            Settings[BuildMemoryBudget] = 2048.0;
            Settings[RecentProjects] = new List<string>();
            Settings[LastBuildProfile] = "";
            Settings[Pcsx2Path] = "";
            Settings[Pcsx2DiscImage] = "";
            Settings[Pcsx2ReloadOnSave] = true;
            Settings[Pcsx2Arguments] = "";
            Settings[OgiViewerSkeleton] = false;
            Settings[DiscordRichPresence] = false;
            Settings[CollapsedPreferenceSections] = new List<string>();
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

        public static void SetPreference(string prefName, Double value)
        {
            if (Settings[prefName] is IConvertible current && current.ToDouble(CultureInfo.InvariantCulture) == value)
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
            // Numbers read from the settings file are whatever JSON made of them, a whole number becomes a long
            if (retT.IsPrimitive && Settings[prefName] is IConvertible convertible)
            {
                return (T)convertible.ToType(retT, CultureInfo.InvariantCulture);
            }
            // Lists read from the settings file stay JSON until they're asked for
            if (Settings[prefName] is JToken token)
            {
                return token.ToObject<T>()!;
            }
            return (T)Settings[prefName];
        }

        public class PreferenceChangedArgs : EventArgs
        {
            public string PreferenceName { get; set; } = "";
        }
    }
}
