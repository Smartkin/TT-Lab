using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Instance;
using TT_Lab.AssetData.Instance.Particle;
using TT_Lab.Assets;
using TT_Lab.ViewModels.Editors.PropertyGraph;

namespace TT_Lab.ViewModels.Editors.Instance;

public record DefaultParticleSystemChoice(UInt16 Index, string Name)
{
    public string Label => Index == DefaultParticleSystemFieldViewModel.None ? Name : $"{Index}  {Name}";
}

/// <summary>
/// One of the default chunk's particle systems, kept by its index: collision surfaces play theirs through the game's table of systems,
/// which starts with the default chunk's (<c>FUN_00223ad0</c> plays entry index + 1, entry 0 is none), 0xFFFF plays none
/// </summary>
public partial class DefaultParticleSystemFieldViewModel(DocumentViewModel document, PropertyNode node, params DocumentNodeViewModel[] dependencies)
    : DocumentDataViewModel<UInt16>(document, node, dependencies)
{
    public const UInt16 None = 0xFFFF;

    private IReadOnlyList<DefaultParticleSystemChoice> _choices = [];

    [Reactive]
    private string _search = string.Empty;

    [Reactive(SetModifier = AccessModifier.Private)]
    private IReadOnlyList<DefaultParticleSystemChoice> _shownChoices = [];

    [Reactive(SetModifier = AccessModifier.Private)]
    private string _systemName = string.Empty;

    [Reactive(SetModifier = AccessModifier.Private)]
    private string _linkState = string.Empty;

    [Reactive(SetModifier = AccessModifier.Private)]
    private bool _isLinkBroken;

    [Reactive(SetModifier = AccessModifier.Private)]
    private bool _hasSystem;

    protected override void OnActivated(CompositeDisposable disposables)
    {
        base.OnActivated(disposables);
        this.WhenAnyValue(x => x.Search).Subscribe(_ => Filter()).DisposeWith(disposables);
        UpdateLinkState();
    }

    protected override void OnCurrentValueChanged()
    {
        base.OnCurrentValueChanged();
        UpdateLinkState();
    }

    /// <summary>
    /// Lists the default chunk's systems again, they can be renamed or added while the editor is open
    /// </summary>
    public void LoadChoices()
    {
        var systems = GetSystems();
        _choices = [new DefaultParticleSystemChoice(None, "None"), ..systems.Select((system, index) => new DefaultParticleSystemChoice((UInt16)index, system.Name))];
        Search = string.Empty;
        Filter();
    }

    public void Pick(DefaultParticleSystemChoice choice)
    {
        SetCurrentValue(choice.Index);
    }

    public void GoToSystem()
    {
        if (GetOwner() is not { } owner || ParticleSystemFieldViewModel.DefaultParticlesOf(owner) is not { } defaults || CurrentValue >= GetSystems().Count)
        {
            return;
        }

        ParticleSystemFieldViewModel.ShowDefaultSystem(owner, defaults, CurrentValue);
    }

    private void Filter()
    {
        ShownChoices = string.IsNullOrEmpty(Search)
            ? _choices
            : _choices.Where(choice => choice.Label.Contains(Search, StringComparison.CurrentCultureIgnoreCase)).ToList();
    }

    private void UpdateLinkState()
    {
        var index = CurrentValue;
        if (index == None)
        {
            (SystemName, LinkState, IsLinkBroken, HasSystem) = ("None", "Plays no particles", false, false);
            return;
        }

        var systems = GetSystems();
        if (index < systems.Count)
        {
            (SystemName, LinkState, IsLinkBroken, HasSystem) = ($"{index}  {systems[index].Name}", "The default chunk's", false, true);
            return;
        }

        // Past the default chunk's systems the game's table has the systems of whatever levels are loaded
        (SystemName, LinkState, IsLinkBroken, HasSystem) = (index.ToString(), $"The default chunk has {systems.Count} particle systems", true, false);
    }

    private IReadOnlyList<ParticleSystem> GetSystems()
    {
        return GetOwner() is { } owner && ParticleSystemFieldViewModel.DefaultParticlesOf(owner) is { } defaults
            ? ((IAsset)defaults).GetData<DefaultParticleData>().ParticleSystems
            : [];
    }

    // The surface is edited on its own or in a chunk's document, where its data is under the chunk's resources
    private IAsset? GetOwner()
    {
        for (var node = Property; node != null; node = node.Parent)
        {
            if (node.Target is AbstractAssetData data)
            {
                return data.GetOwner();
            }
        }

        return null;
    }
}
