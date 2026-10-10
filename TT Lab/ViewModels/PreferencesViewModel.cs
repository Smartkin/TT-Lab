using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using Caliburn.Micro;
using TT_Lab.Command;
using TT_Lab.Tools.Discord;
using TT_Lab.Tools.Pcsx2;
using TT_Lab.Util;
using ICommand = System.Windows.Input.ICommand;

namespace TT_Lab.ViewModels;

public class PreferencesViewModel : Screen
{
    // The sections' titles, what settings.json keeps the collapsed ones by
    public const string GeneralSection = "General";
    public const string DirectGameLaunchSection = "Direct Game Launch";
    public const string DiscordSection = "Discord Rich Presence";

    private static readonly IBrush PresenceOff = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
    private static readonly IBrush PresenceConnecting = new SolidColorBrush(Color.FromRgb(0xE8, 0xC3, 0x3A));
    private static readonly IBrush PresenceConnected = new SolidColorBrush(Color.FromRgb(0x3D, 0xBA, 0x4E));
    private static readonly IBrush PresenceNotConnected = new SolidColorBrush(Color.FromRgb(0xD9, 0x44, 0x3A));

    private readonly DiscordPresence _presence;

    public PreferencesViewModel(DiscordPresence presence)
    {
        _presence = presence;
    }

    protected override Task OnActivatedAsync(CancellationToken cancellationToken)
    {
        _presence.ConnectionChanged += OnPresenceConnectionChanged;
        NotifyOfPropertyChange(nameof(DiscordStatus));
        return base.OnActivatedAsync(cancellationToken);
    }

    protected override Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
    {
        if (close)
        {
            _presence.ConnectionChanged -= OnPresenceConnectionChanged;
            Preferences.Save();
        }
        
        return base.OnDeactivateAsync(close, cancellationToken);
    }

    private void OnPresenceConnectionChanged() => NotifyOfPropertyChange(nameof(DiscordStatus));

    public bool IsGeneralExpanded
    {
        get => IsExpanded(GeneralSection);
        set => SetExpanded(GeneralSection, value);
    }

    public bool IsDirectGameLaunchExpanded
    {
        get => IsExpanded(DirectGameLaunchSection);
        set => SetExpanded(DirectGameLaunchSection, value);
    }

    public bool IsDiscordExpanded
    {
        get => IsExpanded(DiscordSection);
        set => SetExpanded(DiscordSection, value);
    }

    private static bool IsExpanded(string section) => !Preferences.GetPreference<List<string>>(Preferences.CollapsedPreferenceSections).Contains(section);

    private void SetExpanded(string section, bool expanded, [CallerMemberName] string? property = null)
    {
        if (IsExpanded(section) == expanded)
        {
            return;
        }

        var collapsed = Preferences.GetPreference<List<string>>(Preferences.CollapsedPreferenceSections).Where(name => name != section).ToList();
        if (!expanded)
        {
            collapsed.Add(section);
        }

        Preferences.SetPreference(Preferences.CollapsedPreferenceSections, collapsed);
        NotifyOfPropertyChange(property);
    }
    
    public ICommand SetProjectsPathCommand => new SelectFolderCommand(null, this, nameof(ProjectsPath));

    public ICommand SetPs2DiscContentPathCommand => new SelectFolderCommand(null, this, nameof(Ps2ContentPath));

    public ICommand SetXboxDiscContentPathCommand => new SelectFolderCommand(null, this, nameof(XboxContentPath));

    public bool AreEasterEggsEnabled
    {
        get => Preferences.GetPreference<bool>(Preferences.SillinessEnabled);
        set
        {
            Preferences.SetPreference(Preferences.SillinessEnabled, value);
            NotifyOfPropertyChange();
        }
    }

    public string Ps2ContentPath
    {
        get => Preferences.GetPreference<string>(Preferences.Ps2DiscContentPath);
        set
        {
            Preferences.SetPreference(Preferences.Ps2DiscContentPath, value);
            NotifyOfPropertyChange();
        }
    }

    public string XboxContentPath
    {
        get => Preferences.GetPreference<string>(Preferences.XboxDiscContentPath);
        set
        {
            Preferences.SetPreference(Preferences.XboxDiscContentPath, value);
            NotifyOfPropertyChange();
        }
    }

    public string ProjectsPath
    {
        get => Preferences.GetPreference<string>(Preferences.ProjectsPath);
        set
        {
            Preferences.SetPreference(Preferences.ProjectsPath, value);
            NotifyOfPropertyChange();
        }
    }

    // Direct Game Launch: what the Play button of a chunk's scene plays it with (Tools/Pcsx2), none picked finds them on its own
    public string Pcsx2Path
    {
        get => Preferences.GetPreference<string>(Preferences.Pcsx2Path);
        set
        {
            Preferences.SetPreference(Preferences.Pcsx2Path, value);
            NotifyOfPropertyChange();
        }
    }

    public string Pcsx2DiscImage
    {
        get => Preferences.GetPreference<string>(Preferences.Pcsx2DiscImage);
        set
        {
            Preferences.SetPreference(Preferences.Pcsx2DiscImage, value);
            NotifyOfPropertyChange();
        }
    }

    public bool ReloadGameAfterSaving
    {
        get => Preferences.GetPreference<bool>(Preferences.Pcsx2ReloadOnSave);
        set
        {
            Preferences.SetPreference(Preferences.Pcsx2ReloadOnSave, value);
            NotifyOfPropertyChange();
        }
    }

    public string Pcsx2Arguments
    {
        get => Preferences.GetPreference<string>(Preferences.Pcsx2Arguments);
        set
        {
            Preferences.SetPreference(Preferences.Pcsx2Arguments, value);
            NotifyOfPropertyChange();
        }
    }

    public bool DiscordEnabled
    {
        get => Preferences.GetPreference<bool>(Preferences.DiscordRichPresence);
        set
        {
            Preferences.SetPreference(Preferences.DiscordRichPresence, value);
            NotifyOfPropertyChange();
        }
    }

    // Dark gray while it's off, yellow connecting, green connected, red while Discord can't be reached
    public IBrush DiscordStatus => _presence.Connection switch
    {
        PresenceConnection.Connecting => PresenceConnecting,
        PresenceConnection.Connected => PresenceConnected,
        PresenceConnection.NotConnected => PresenceNotConnected,
        _ => PresenceOff
    };

    public string FoundPcsx2 => Pcsx2Install.Find("") switch
    {
        { IsFlatpak: true } => $"Found: its Flatpak ({Pcsx2Install.FlatpakId})",
        { } install => $"Found: {install.Executable}",
        null => "Not found, pick its executable"
    };

    public async Task ChoosePcsx2()
    {
        var picked = await MiscUtils.GetFileFromDialogueAsync("Pick PCSX2's executable", "PCSX2", OperatingSystem.IsWindows() ? ["*.exe"] : ["*"]);
        if (!string.IsNullOrEmpty(picked))
        {
            Pcsx2Path = picked;
        }
    }

    public void ForgetPcsx2() => Pcsx2Path = "";

    public async Task ChooseDiscImage()
    {
        var picked = await MiscUtils.GetFileFromDialogueAsync("Pick a disc image of Crash Twinsanity (PS2)", "Disc images", ["*.iso", "*.bin", "*.chd", "*.cso"]);
        if (!string.IsNullOrEmpty(picked))
        {
            Pcsx2DiscImage = picked;
        }
    }

    public void ForgetDiscImage() => Pcsx2DiscImage = "";
}