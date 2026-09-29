using System;
using System.Threading;
using System.Threading.Tasks;
using Caliburn.Micro;
using TT_Lab.Command;
using TT_Lab.Tools.Pcsx2;
using TT_Lab.Util;
using ICommand = System.Windows.Input.ICommand;

namespace TT_Lab.ViewModels;

public class PreferencesViewModel : Screen
{
    protected override Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
    {
        if (close)
        {
            Preferences.Save();
        }
        
        return base.OnDeactivateAsync(close, cancellationToken);
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