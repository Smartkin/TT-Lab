using TT_Lab.AssetData.Instance.Particle;

namespace TT_Lab.ViewModels.Editors.Descs;

/// <summary>
/// A particle system's blend mode picked by what the game does with it
/// </summary>
public record ParticleBlendModeEditorDesc : EditorDesc
{
    protected override DocumentNodeViewModel ConstructInternal() => new ByteChoiceFieldViewModel(Document, Node, ParticleBlendModes.Modes, ParticleBlendModes.FindMode);
}

/// <summary>
/// The list the game draws a particle system from
/// </summary>
public record ParticleDrawListEditorDesc : EditorDesc
{
    protected override DocumentNodeViewModel ConstructInternal() => new ByteChoiceFieldViewModel(Document, Node, ParticleBlendModes.DrawLists, ParticleBlendModes.FindDrawList);
}
