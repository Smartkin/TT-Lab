using System.Linq;
using System.Reactive;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using TT_Lab.Assets;
using TT_Lab.Assets.Instance;
using TT_Lab.Tools.Blender;
using TT_Lab.ViewModels.Editors;

namespace TT_Lab.ViewModels;

// A chunk's scene edits its scenery in Blender, the scenery's model file (Edit in Blender)
public partial class ViewportViewModel
{
    [Reactive(SetModifier = AccessModifier.Private)]
    private bool _hasSceneryToEdit;

    public ReactiveCommand<Unit, Unit> EditSceneryInBlenderCommand { get; private set; } = null!;
    // Use an Existing Blend File: a scenery of a blend file replaces the chunk's
    public ReactiveCommand<Unit, Unit> UseBlendFileForSceneryCommand { get; private set; } = null!;

    private void InitBlenderEditing()
    {
        EditSceneryInBlenderCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            if (SceneryOfChunk() is { } scenery)
            {
                await BlenderEditing.EditAsync(scenery);
            }
        });
        UseBlendFileForSceneryCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            if (SceneryOfChunk() is { } scenery)
            {
                await BlenderEditing.UseBlendFileAsync(scenery);
            }
        });
    }

    private void FollowBlenderEditing() => HasSceneryToEdit = SceneryOfChunk() != null;

    private Scenery? SceneryOfChunk()
    {
        if (_document?.DocumentModel is not LevelChunk chunk)
        {
            return null;
        }

        var assetManager = AssetManager.Get();
        return chunk.ChunkResources.Where(assetManager.DoesAssetExist).Select(assetManager.GetAsset).OfType<Scenery>().FirstOrDefault();
    }
}
