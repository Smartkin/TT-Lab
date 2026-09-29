using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using TT_Lab.Assets;
using TT_Lab.Project.Build;

namespace TT_Lab.ViewModels;

/// <summary>
/// A chunk of the project the build dialog lets in or leaves out of a build
/// </summary>
public sealed partial class BuildChunkRow(LabURI uri, string name, string package, Project.Project.GamePlatform platform) : ReactiveObject
{
    public LabURI Uri { get; } = uri;

    public string Name { get; } = name;

    public string Package { get; } = package;

    public Project.Project.GamePlatform Platform { get; } = platform;

    [Reactive]
    private bool _isIncluded = true;
}

/// <summary>
/// Picks what to build: a profile with the version of the game and the chunks to leave out, kept in the project's profiles folder
/// </summary>
public sealed partial class BuildDialogViewModel : ReactiveObject
{
    private readonly BuildProfileLibrary _library;
    private readonly List<BuildChunkRow> _chunks;
    private bool _loadingProfile;

    [Reactive]
    private BuildProfile? _selectedProfile;

    [Reactive]
    private string _profileName = BuildProfile.DefaultName;

    [Reactive]
    private bool _isPs2 = true;

    [Reactive]
    private string _filter = string.Empty;

    [Reactive(SetModifier = AccessModifier.Private)]
    private IReadOnlyList<BuildChunkRow> _shownChunks = [];

    public BuildDialogViewModel(Project.Project project) : this(project, new BuildProfileLibrary(project))
    {
    }

    public BuildDialogViewModel(Project.Project project, BuildProfileLibrary library)
    {
        _library = library;
        // The default chunk is the startup's, every build has it
        _chunks = project.AssetManager.GetAssets().OfType<LevelChunk>()
            .Where(chunk => chunk.Name != "default")
            .Select(chunk => new BuildChunkRow(chunk.URI, chunk.GetChunkPath(), project.AssetManager.GetAsset<Package>(chunk.Package).Alias, project.GetPlatform(chunk.Package)))
            .OrderBy(row => row.Package, StringComparer.OrdinalIgnoreCase).ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        Profiles = new ObservableCollection<BuildProfile>(library.Load());
        if (Profiles.Count == 0)
        {
            Profiles.Add(new BuildProfile());
        }

        NewProfileCommand = ReactiveCommand.Create(NewProfile);
        DeleteProfileCommand = ReactiveCommand.Create(DeleteProfile);
        SaveProfileCommand = ReactiveCommand.Create(() => { SaveProfile(); });
        IncludeAllCommand = ReactiveCommand.Create(() => SetShownIncluded(true));
        ExcludeAllCommand = ReactiveCommand.Create(() => SetShownIncluded(false));
        BuildCommand = ReactiveCommand.Create(Build);
        CancelCommand = ReactiveCommand.Create(() => CloseRequested?.Invoke());

        this.WhenAnyValue(x => x.SelectedProfile).Subscribe(LoadProfile);
        this.WhenAnyValue(x => x.IsPs2, x => x.Filter).Subscribe(_ => UpdateShownChunks());
        var lastUsed = Preferences.GetPreference<string>(Preferences.LastBuildProfile);
        SelectedProfile = Profiles.FirstOrDefault(profile => profile.Name == lastUsed) ?? Profiles[0];
    }

    public ObservableCollection<BuildProfile> Profiles { get; }

    public ReactiveCommand<Unit, Unit> NewProfileCommand { get; }
    public ReactiveCommand<Unit, Unit> DeleteProfileCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveProfileCommand { get; }
    public ReactiveCommand<Unit, Unit> IncludeAllCommand { get; }
    public ReactiveCommand<Unit, Unit> ExcludeAllCommand { get; }
    public ReactiveCommand<Unit, Unit> BuildCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    /// <summary>
    /// The profile to build with once the dialog closes, none when it got cancelled
    /// </summary>
    public BuildProfile? Result { get; private set; }

    public event Action? CloseRequested;

    public Project.Project.GamePlatform Platform => IsPs2 ? Project.Project.GamePlatform.PS2 : Project.Project.GamePlatform.Xbox;

    public bool IsXbox
    {
        get => !IsPs2;
        set => IsPs2 = !value;
    }

    public int IncludedCount => _chunks.Count(chunk => chunk.Platform == Platform && chunk.IsIncluded);

    public int PlatformChunkCount => _chunks.Count(chunk => chunk.Platform == Platform);

    private void LoadProfile(BuildProfile? profile)
    {
        if (profile == null)
        {
            return;
        }

        _loadingProfile = true;
        try
        {
            ProfileName = profile.Name;
            IsPs2 = profile.Platform == Project.Project.GamePlatform.PS2;
            var excluded = profile.ExcludedChunkUris();
            foreach (var chunk in _chunks)
            {
                chunk.IsIncluded = !excluded.Contains(chunk.Uri);
            }
        }
        finally
        {
            _loadingProfile = false;
        }

        UpdateShownChunks();
    }

    private void UpdateShownChunks()
    {
        if (_loadingProfile)
        {
            return;
        }

        var platform = Platform;
        ShownChunks = _chunks.Where(chunk => chunk.Platform == platform
                                             && (Filter.Length == 0 || chunk.Name.Contains(Filter, StringComparison.OrdinalIgnoreCase) || chunk.Package.Contains(Filter, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        this.RaisePropertyChanged(nameof(IsXbox));
        this.RaisePropertyChanged(nameof(PlatformChunkCount));
    }

    private void SetShownIncluded(bool included)
    {
        foreach (var chunk in ShownChunks)
        {
            chunk.IsIncluded = included;
        }
    }

    private void NewProfile()
    {
        var names = Profiles.Select(profile => profile.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var number = Profiles.Count + 1;
        var name = $"Profile {number}";
        while (names.Contains(name))
        {
            name = $"Profile {++number}";
        }

        var profile = new BuildProfile { Name = name, Platform = Platform };
        Profiles.Add(profile);
        SelectedProfile = profile;
    }

    private void DeleteProfile()
    {
        if (SelectedProfile is not { } profile)
        {
            return;
        }

        _library.Delete(profile);
        Profiles.Remove(profile);
        if (Profiles.Count == 0)
        {
            Profiles.Add(new BuildProfile());
        }

        SelectedProfile = Profiles[0];
    }

    /// <summary>
    /// Puts the dialog's choices into the selected profile and writes its file
    /// </summary>
    public BuildProfile? SaveProfile()
    {
        if (SelectedProfile is not { } profile)
        {
            return null;
        }

        var name = ProfileName.Trim();
        profile.Name = name.Length == 0 ? BuildProfile.DefaultName : name;
        profile.Platform = Platform;
        profile.ExcludedChunks = _chunks.Where(chunk => chunk.Platform == profile.Platform && !chunk.IsIncluded).Select(chunk => chunk.Uri.ToString()).ToList();
        _library.Save(profile);
        return profile;
    }

    private void Build()
    {
        if (SaveProfile() is not { } profile)
        {
            return;
        }

        Preferences.SetPreference(Preferences.LastBuildProfile, profile.Name);
        Result = profile;
        CloseRequested?.Invoke();
    }
}
