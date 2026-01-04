using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Twinsanity.TwinsanityInterchange.Enumerations;

namespace TT_Lab.Util
{
    public static class MiscUtils
    {
        private static Bitmap? _boatguy;
        private static Dictionary<string, Bitmap> _labIconStorage = new();

        public static object? ConvertEnum(Type t, object? o)
        {
            if (o == null)
            {
                return null;
            }
            return Enum.Parse(t, o.ToString()!);
        }

        public static T? ConvertEnum<T>(object? o)
        {
            return (T?)ConvertEnum(typeof(T), o);
        }

        public static Bitmap GetBoatGuy()
        {
            _boatguy ??= new Bitmap(ManifestResourceLoader.GetPathInExe("Media\\boat_guy.png"));
            return _boatguy;
        }

        public static Bitmap GetLabIcon(string iconName)
        {
            if (_labIconStorage.TryGetValue(iconName, out Bitmap? value))
            {
                return value;
            }

            _labIconStorage.Add(iconName, new Bitmap(ManifestResourceLoader.GetPathInExe($"Media\\LabIcons\\{iconName}.png")));

            return _labIconStorage[iconName];
        }

        public static string GetFileFromDialogue(string filter, string initial_directory = "")
        {
            Window? owner = null;
            if (Application.Current!.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                owner = desktop.MainWindow;
            }

            if (owner == null)
            {
                return string.Empty;
            }

            var pickerOptions = new FilePickerOpenOptions
            {
                AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType("tson")],
                Title = filter
            };
            var filePickerTask = Task.Factory.StartNew(async () =>
            {
                var getFolderTask = await TopLevel.GetTopLevel(owner)!.StorageProvider.OpenFilePickerAsync(pickerOptions);
            });
            // var getFolderTask = TopLevel.GetTopLevel(owner)!.StorageProvider.OpenFilePickerAsync(pickerOptions);
            // var folder = getFolderTask.Result;
            // if (folder.Count > 0)
            // {
            //     return folder[0].Name;
            // }
            
            return string.Empty;
        }
        
        public static async Task<string> GetFileFromDialogueAsync(string filter, string initial_directory = "")
        {
            Window? owner = null;
            if (Application.Current!.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                owner = desktop.MainWindow;
            }

            if (owner == null)
            {
                return string.Empty;
            }

            var startingLocation = await owner.StorageProvider.TryGetFolderFromPathAsync(new Uri($"file://{initial_directory}"));
            var pickerOptions = new FilePickerOpenOptions
            {
                AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType("tson")],
                Title = filter,
                SuggestedStartLocation = startingLocation
            };
            
            var files = await owner.StorageProvider.OpenFilePickerAsync(pickerOptions);
            if (files.Count > 0)
            {
                return files[0].TryGetLocalPath() ?? string.Empty;
            }
            
            return string.Empty;
        }

        public static Enums.InstanceState ChangeFlag(this Enums.InstanceState state, Enums.InstanceState flags, Boolean set)
        {
            if (!set)
            {
                return state.UnsetFlag(flags);
            }
            return state.SetFlag(flags);
        }
        public static Enums.InstanceState SetFlag(this Enums.InstanceState state, Enums.InstanceState flags)
        {
            state |= flags;
            return state;
        }
        public static Enums.InstanceState UnsetFlag(this Enums.InstanceState state, Enums.InstanceState flags)
        {
            state &= ~flags;
            return state;
        }

        public static Enums.TriggerActivatorObjects ChangeFlag(this Enums.TriggerActivatorObjects state, Enums.TriggerActivatorObjects flags, Boolean set)
        {
            if (!set)
            {
                return state.UnsetFlag(flags);
            }
            return state.SetFlag(flags);
        }
        public static Enums.TriggerActivatorObjects SetFlag(this Enums.TriggerActivatorObjects state, Enums.TriggerActivatorObjects flags)
        {
            state |= flags;
            return state;
        }
        public static Enums.TriggerActivatorObjects UnsetFlag(this Enums.TriggerActivatorObjects state, Enums.TriggerActivatorObjects flags)
        {
            state &= ~flags;
            return state;
        }
    }

    public static class GlobalConsts
    {
        public static string OgreGroup => "Project"; 
    }
}
