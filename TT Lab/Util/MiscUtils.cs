using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Splat;
using TT_Lab.ViewModels;
using TT_Lab.ViewModels.Interfaces;
using Twinsanity.TwinsanityInterchange.Enumerations;

namespace TT_Lab.Util;

public static class MiscUtils
{
    private static Bitmap? _boatguy;
    private static Dictionary<string, Bitmap> _labIconStorage = new();

    public static object? ConvertEnum(Type t, object? o)
    {
        return Enum.TryParse(t, o?.ToString(), out var result) ? result : null;
    }

    public static T? ConvertEnum<T>(object? o)
    {
        return (T?)ConvertEnum(typeof(T), o);
    }

    public static Bitmap GetBoatGuy()
    {
        _boatguy ??= new Bitmap(ManifestResourceLoader.GetPathInExe("Media/boat_guy.png"));
        return _boatguy;
    }

    public static Bitmap GetLabIcon(string iconName)
    {
        if (_labIconStorage.TryGetValue(iconName, out Bitmap? value))
        {
            return value;
        }

        _labIconStorage.Add(iconName, new Bitmap(ManifestResourceLoader.GetPathInExe($"Media/LabIcons/{iconName}.png")));

        return _labIconStorage[iconName];
    }

    public static Bitmap CloneBitmap(this Bitmap bitmap)
    {
        using var ms = new MemoryStream();
        bitmap.Save(ms);
        ms.Position = 0;
            
        return new Bitmap(ms);
    }
        
    public static async Task<string> GetFileFromDialogueAsync(string title, string filterName, IReadOnlyList<string> filters, string initialDirectory = "")
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

        var storageProvider = owner.StorageProvider;
        IStorageFolder? startingLocation;
        if (string.IsNullOrEmpty(initialDirectory))
        {
            startingLocation =
                await storageProvider.TryGetWellKnownFolderAsync(WellKnownFolder.Documents);
        }
        else
        {
            startingLocation =
                await storageProvider.TryGetFolderFromPathAsync(initialDirectory);
        }
        var pickerOptions = new FilePickerOpenOptions
        {
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType(filterName) { Patterns = filters }],
            Title = title,
            SuggestedStartLocation = startingLocation
        };
            
        var files = await storageProvider.OpenFilePickerAsync(pickerOptions);
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

    public static Window GetMainWindow()
    {
        return (Window)((ShellViewModel)Locator.Current.GetService<ILabManager>()!).GetView();
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