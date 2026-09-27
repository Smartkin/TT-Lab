using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Splat;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Assets.Instance;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using TT_Lab.ViewModels.Interfaces;

namespace TT_Lab.ViewModels.Editors.Instance;

public record ParticleSystemChoice(string Name, bool IsDefault)
{
    public string Source => IsDefault ? "Default" : "Chunk";
}

/// <summary>
/// Name of the particle system an emitter plays, picked from the chunk's own systems and the default chunk's
/// </summary>
public partial class ParticleSystemFieldViewModel(DocumentViewModel document, PropertyNode data, params DocumentNodeViewModel[] dependencies)
    : TextFieldViewModel(document, data, dependencies)
{
    private IReadOnlyList<ParticleSystemChoice> _choices = [];

    [Reactive]
    private string _search = string.Empty;

    [Reactive(SetModifier = AccessModifier.Private)]
    private IReadOnlyList<ParticleSystemChoice> _shownChoices = [];

    [Reactive(SetModifier = AccessModifier.Private)]
    private string _linkState = string.Empty;

    [Reactive(SetModifier = AccessModifier.Private)]
    private bool _isLinkBroken;

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
    /// Lists the systems again, the chunk's can be renamed or added while the editor is open
    /// </summary>
    public void LoadChoices()
    {
        _choices = GetParticles()?.GetUsableSystems().Select(usable => new ParticleSystemChoice(usable.System.Name, usable.IsDefault)).ToList() ?? [];
        Search = string.Empty;
        Filter();
    }

    public void Pick(ParticleSystemChoice choice)
    {
        Text = choice.Name;
    }

    private void Filter()
    {
        ShownChoices = string.IsNullOrEmpty(Search)
            ? _choices
            : _choices.Where(choice => choice.Name.Contains(Search, StringComparison.CurrentCultureIgnoreCase)).ToList();
    }

    private void UpdateLinkState()
    {
        var name = Property.GetValue() as string ?? string.Empty;
        var found = GetParticles()?.FindSystem(name);
        IsLinkBroken = found == null;
        LinkState = found switch
        {
            null => "No particle system of the chunk or the default chunk has this name",
            { IsDefault: true } => "The default chunk's",
            _ => "The chunk's",
        };
    }

    // Emitters are edited in the chunk's particles or in the chunk's document where the particles are a link
    private ParticleData? GetParticles()
    {
        for (var node = Property.Parent; node != null; node = node.Parent)
        {
            if (node.Target is ParticleData particles)
            {
                return particles;
            }
        }

        return null;
    }

    /// <summary>
    /// Shows the system the emitter plays: in the chunk's inspector when it's the chunk's own, in the default chunk's when it's one of the
    /// default ones
    /// </summary>
    public void GoToSystem()
    {
        var particles = GetParticles();
        if (particles?.FindSystem(Property.GetValue() as string ?? string.Empty) is not { } found)
        {
            return;
        }

        if (!found.IsDefault)
        {
            var dataNode = FindNodeOf(particles);
            if (dataNode?.Find(nameof(ParticleData.ParticleSystems)) is { } systems)
            {
                Reveal(Document, dataNode.Parent!, systems.Children[particles.ParticleSystems.IndexOf(found.System)]);
            }

            return;
        }

        var assetManager = AssetManager.Get();
        var owner = particles.GetOwner();
        var defaultChunk = assetManager.GetAllAssetsOf<LevelChunk>().FirstOrDefault(chunk => chunk.IsGlobalDefaultChunk && assetManager.IsRelated(chunk.Package, owner.Package));
        var defaults = assetManager.GetRelatedAssetsOf<DefaultParticles>(owner.Package).FirstOrDefault();
        if (defaultChunk == null || defaults == null)
        {
            return;
        }

        var systemIndex = ((IAsset)defaults).GetData<DefaultParticleData>().ParticleSystems.IndexOf(found.System);
        Locator.Current.GetService<ILabManager>()!.OpenEditor(defaultChunk);
        var tab = Locator.Current.GetService<EditorsViewModel>()!.ScenesEditorsViewModel.Tabs.FirstOrDefault(tab => tab.EditableResource == defaultChunk.URI);
        tab?.WhenAnyValue(x => x.IsLoaded, x => x.Document, (isLoaded, document) => isLoaded ? document : null)
            .WhereNotNull()
            .Take(1)
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(document =>
            {
                var resource = document.PropertyGraph.Find($"Root.{nameof(LevelChunk.ChunkResources)}")?.Children.FirstOrDefault(node => Equals(node.GetValue(), defaults.URI));
                if (resource?.Find("[data]") is { } asset && asset.Find($"AssetData.{nameof(ParticleData.ParticleSystems)}[{systemIndex}]") is { } system)
                {
                    Reveal(document, asset, system);
                }
            });
    }

    private PropertyNode? FindNodeOf(ParticleData particles)
    {
        for (var node = Property.Parent; node != null; node = node.Parent)
        {
            if (ReferenceEquals(node.GetValue(), particles))
            {
                return node;
            }
        }

        return null;
    }

    // Chunks' documents have their inspector, other documents show it in their own tree
    private static void Reveal(DocumentViewModel document, PropertyNode inspected, PropertyNode target)
    {
        if (document.DocumentModel is LevelChunk)
        {
            document.OpenInspector(inspected, target);
            return;
        }

        document.Root.Reveal(target);
    }
}
