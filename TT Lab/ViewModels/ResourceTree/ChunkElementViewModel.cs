using System;
using System.Linq;
using System.Threading.Tasks;
using Caliburn.Micro;
using Splat;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Assets.Instance;
using TT_Lab.Project;

namespace TT_Lab.ViewModels.ResourceTree;

public class ChunkElementViewModel : ResourceTreeElementViewModel
{
    public ChunkElementViewModel(LabURI asset, ResourceTreeElementViewModel? parent = null) : base(asset, parent)
    {
    }

    protected override void CreateContextMenu()
    {
        RegisterMenuItem(new MenuItemSettings
        {
            Header = "Build chunk",
            Action = RebuildChunk
        });
        RegisterMenuItem(new MenuItemSettings
        {
            Header = "Build with neighbouring chunks",
            Action = RebuildChunkAndLinks
        });

    }

    private async void RebuildChunkAndLinks()
    {
        try
        {
            var projectManager = Locator.Current.GetService<ProjectManager>()!;
            projectManager.WorkableProject = false;
            using var buildTask = Task.Factory.StartNew(() =>
            {
                Locator.Current.GetService<ProjectManager>()!.OpenedProject!.PackChunk(Asset.URI);
            });
            await buildTask;

            var assetManager = AssetManager.Get();
            var links = GetAsset<LevelChunk>().ChunkResources.FirstOrDefault(uri => assetManager.GetAsset(uri) is ChunkLinks, LabURI.Empty);
            if (links != LabURI.Empty)
            {
                var assetLinks = assetManager.GetAsset(links);
                var linksData = assetLinks.GetData<ChunkLinksData>();
                foreach (var link in linksData.Links)
                {
                    using var linkTask = Task.Factory.StartNew(() =>
                    {
                        Locator.Current.GetService<ProjectManager>()!.OpenedProject!.PackChunk(link.Path);
                    });
                    await linkTask;
                }
            }
            projectManager.WorkableProject = true;
        }
        catch (Exception e)
        {
            Locator.Current.GetService<ProjectManager>()!.WorkableProject = true;
            Log.WriteLine($"Error when building chunk: {e.Message}");
        }
    }

    private async void RebuildChunk()
    {
        try
        {
            var projectManager = Locator.Current.GetService<ProjectManager>()!;
            projectManager.WorkableProject = false;
            var buildTask = Task.Factory.StartNew(() =>
            {
                Locator.Current.GetService<ProjectManager>()!.OpenedProject!.PackChunk(Asset.URI);
            });
            await buildTask;
            projectManager.WorkableProject = true;
        }
        catch (Exception e)
        {
            Locator.Current.GetService<ProjectManager>()!.WorkableProject = true;
            Log.WriteLine($"Error when building chunk: {e.Message}");
        }
    }
}