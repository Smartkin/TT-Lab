using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using ReactiveUI.SourceGenerators;
using TT_Lab.AssetData.Graphics;
using TT_Lab.AssetData.Graphics.Shaders;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Enumerations;

namespace TT_Lab.ViewModels.Editors;

public sealed record ShaderTypeItem(string Name, bool IsUsed, string? Hint);

/// <summary>
/// The shader types a material's activated shaders stand for, each ticked when one of the material's shaders has it. The stored bits can't
/// tell them apart (StandardUnlit and UnlitBillboard share one) and building writes them from the shaders anyway
/// </summary>
public partial class ActivatedShadersFieldViewModel : DocumentDataViewModel<object>
{
    // The types the game gives a bit, then the ones a shader has without one
    private static readonly TwinShader.Type[] TypesWithBits = Enum.GetValues<TwinShader.Type>()
        .Where(type => Enum.TryParse<Enums.AppliedShaders>(type.ToString(), out _)).ToArray();

    [Reactive]
    private IReadOnlyList<ShaderTypeItem> _types = [];

    public ActivatedShadersFieldViewModel(DocumentViewModel document, PropertyNode node, params DocumentNodeViewModel[] dependencies) : base(document, node, dependencies)
    {
        Update();
    }

    internal static IReadOnlyList<ShaderTypeItem> TypesOf(IEnumerable<LabShader> shaders)
    {
        var used = shaders.Select(shader => shader.ShaderType).ToHashSet();
        return TypesWithBits.Concat(used.Where(type => !TypesWithBits.Contains(type)).Order())
            .Select(type => new ShaderTypeItem(type.ToString(), used.Contains(type), EnumCaptions.HintOf(typeof(TwinShader.Type), type.ToString()))).ToList();
    }

    protected override void OnActivated(CompositeDisposable disposables)
    {
        base.OnActivated(disposables);
        Update();
        Document.PropertyGraph.Changed += OnGraphChanged;
        Disposable.Create(() => Document.PropertyGraph.Changed -= OnGraphChanged).DisposeWith(disposables);
    }

    private void OnGraphChanged(PropertyChange change)
    {
        Update();
    }

    private void Update()
    {
        var shaders = Property.Parent?.Find(nameof(MaterialData.Shaders))?.GetValue() as IEnumerable<LabShader> ?? [];
        var types = TypesOf(shaders);
        if (!types.SequenceEqual(Types))
        {
            Types = types;
        }
    }
}
