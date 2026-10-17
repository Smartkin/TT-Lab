using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Splat;
using TT_Lab.AssetData;
using TT_Lab.Assets;
using TT_Lab.ViewModels;
using TT_Lab.ViewModels.Composite;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.PropertyGraph;

namespace TT_Lab.Tools.Blender;

/// <summary>
/// The documents what a linked Blender changes goes into, every change a step of their histories ("From Blender: ..."): the asset's tab's
/// once it loaded, a level chunk's scenery its chunk's scene's, else one of no tab, kept until the project closes, which a tab opened on the
/// asset (or its chunk) takes over. What Blender didn't save goes when it drops it. Only on the UI thread
/// </summary>
public static class LinkedDocuments
{
    private const string FromBlender = "From Blender: ";
    private static readonly TimeSpan LongestLoading = TimeSpan.FromMinutes(1);
    // A document of no tab keeps fewer steps of data replaced whole (a model file each) than a tab's: nobody undoes there until a tab opens
    private const int HeadlessDataSteps = 8;

    private static readonly Dictionary<LabURI, DocumentViewModel> Headless = [];
    // The entries Blender's changes made, by the Blender that made each: what dropping its changes undoes
    private static readonly ConditionalWeakTable<UndoHistory.Entry, BlenderLinkSession> BlenderEntries = new();

    /// <summary>
    /// Where an asset's changes go: a document and the node of the asset in it (its root, or a chunk's link to its scenery)
    /// </summary>
    public sealed record Place(DocumentViewModel Document, PropertyNode Asset)
    {
        public PropertyNode Data => Value(nameof(SerializableAsset.AssetData));

        public PropertyNode Value(string property)
        {
            return Asset.FindChild($".{property}") ?? throw new InvalidOperationException($"{Document.DocumentModel.DocumentName} has no {property} to change");
        }
    }

    // The open tabs, tests have their own
    internal static Func<IEnumerable<TabbedEditorViewModel>> Tabs { get; set; } = () => Locator.Current.GetService<EditorsViewModel>()?.AllTabs ?? [];

    public static string Describe(string what) => FromBlender + what;

    /// <summary>
    /// Where Blender's change of the asset goes, a document made when there's none
    /// </summary>
    public static async Task<Place> ForAsync(IAsset asset)
    {
        var started = DateTime.UtcNow;
        while (TabsShowing(asset).Any(tab => !tab.IsLoaded || tab.Document == null))
        {
            if (DateTime.UtcNow - started > LongestLoading)
            {
                throw new InvalidOperationException($"{asset.Alias}'s editor didn't load");
            }

            await Task.Delay(50);
        }

        // Not into a step the user is making (a drag, placing): Blender's is a step of its own once that's done. A step that never ends (what
        // ends it failed) leaves Blender's change refused, the Blender's other requests wait for this one
        started = DateTime.UtcNow;
        while (Find(asset) is { Document.History.IsGrouping: true })
        {
            if (DateTime.UtcNow - started > LongestLoading)
            {
                throw new InvalidOperationException($"{asset.Alias}'s editor is in the middle of a step that doesn't end");
            }

            await Task.Delay(50);
        }

        if (Find(asset) is { } place)
        {
            return place;
        }

        asset.GetData<AbstractAssetData>();
        var document = new DocumentViewModel(asset);
        document.Initialize();
        document.History.MostDataSteps = HeadlessDataSteps;
        Headless[asset.URI] = document;
        return new Place(document, document.PropertyGraph.Root);
    }

    /// <summary>
    /// Where Blender's changes of the asset go, none when there's no document yet
    /// </summary>
    public static Place? Find(IAsset asset)
    {
        if (TabOf(asset) is { IsLoaded: true, Document: { } document })
        {
            return new Place(document, document.PropertyGraph.Root);
        }

        // A chunk's scene edits its scenery
        if (LevelChunk.Owning(asset) is { } chunk && TabOf(chunk) is { IsLoaded: true, Document: { } scene } && scene.FindChunkResource(asset.URI) is { } resource)
        {
            return new Place(scene, resource);
        }

        return HeadlessOf(asset) is { } headless ? new Place(headless, headless.PropertyGraph.Root) : null;
    }

    // The document of no tab of the asset, none when the one of its URI is another asset's (deleted, then made again of the same name)
    private static DocumentViewModel? HeadlessOf(IAsset asset)
    {
        if (!Headless.TryGetValue(asset.URI, out var headless))
        {
            return null;
        }

        if (ReferenceEquals(headless.DocumentModel, asset))
        {
            return headless;
        }

        Headless.Remove(asset.URI);
        headless.Close();
        return null;
    }

    /// <summary>
    /// The documents of no tab of assets that left the project (or that a new asset of the same URI took the place of) go, their changes
    /// with them
    /// </summary>
    public static void ForgetGone()
    {
        var assetManager = AssetManager.Get();
        foreach (var (uri, document) in Headless.ToList())
        {
            if (!assetManager.DoesAssetExist(uri) || !ReferenceEquals(assetManager.GetAsset(uri), document.DocumentModel))
            {
                Headless.Remove(uri);
                document.Close();
            }
        }
    }

    private static TabbedEditorViewModel? TabOf(IAsset asset) => Tabs().FirstOrDefault(tab => tab.EditableResource == asset.URI);

    private static IEnumerable<TabbedEditorViewModel> TabsShowing(IAsset asset)
    {
        var chunk = LevelChunk.Owning(asset);
        return Tabs().Where(tab => tab.EditableResource == asset.URI || chunk != null && tab.EditableResource == chunk.URI);
    }

    /// <summary>
    /// Every document open, the tabs' and the ones of no tab
    /// </summary>
    public static IEnumerable<DocumentViewModel> Documents()
    {
        return Tabs().Where(tab => tab.IsLoaded).Select(tab => tab.Document).OfType<DocumentViewModel>().Concat(Headless.Values).Distinct();
    }

    /// <summary>
    /// What's changed in the document until it's disposed is one step of the Blender's
    /// </summary>
    public static IDisposable Step(DocumentViewModel document, string what, BlenderLinkSession session)
    {
        return new StepEnd(document, document.History.BeginGroup(Describe(what)), session);
    }

    private sealed class StepEnd(DocumentViewModel document, IDisposable group, BlenderLinkSession session) : IDisposable
    {
        private readonly UndoHistory.Entry _before = document.History.Current;
        private bool _ended;

        public void Dispose()
        {
            if (_ended)
            {
                return;
            }

            _ended = true;
            group.Dispose();
            if (document.History.Current != _before)
            {
                BlenderEntries.AddOrUpdate(document.History.Current, session);
            }
        }
    }

    public static bool IsBlenders(UndoHistory.Entry entry) => BlenderEntries.TryGetValue(entry, out _);

    private static bool IsBlenders(UndoHistory.Entry entry, BlenderLinkSession session) => BlenderEntries.TryGetValue(entry, out var made) && made == session;

    /// <summary>
    /// The other documents showing the asset under their links show what Blender changed in it, their viewports draw it again
    /// </summary>
    public static void ShowElsewhere(DocumentViewModel changed, IAsset asset)
    {
        foreach (var document in Documents().Where(document => document != changed).ToList())
        {
            document.PropertyGraph.Follow(asset, [$".{nameof(SerializableAsset.AssetData)}"]);
        }
    }

    /// <summary>
    /// Takes back what the Blender changed of the asset and didn't save: its steps since the last of anyone else's (another Blender's or TT
    /// Lab's, which would go with them, keep them) and since the document was saved (what a save took is TT Lab's now) are undone. A document
    /// of no tab with nothing unsaved left goes, and so does one whose history let go of the oldest of the Blender's steps (a picture painted
    /// more times than it keeps): the asset is read again as its files have it
    /// </summary>
    public static void Drop(IAsset asset, BlenderLinkSession session)
    {
        if (Find(asset) is not { } place)
        {
            return;
        }

        var document = place.Document;
        var history = document.History;
        if (HeadlessOf(asset) == document && asset is SerializableAsset { IsUnsaved: false } && history.KeepsTooFewToUndo(entry => IsBlenders(entry, session)))
        {
            Headless.Remove(asset.URI);
            document.Close();
            ReadSaved(asset, document);
            Log.WriteLine($"{asset.Alias} is what it was saved as again, Blender's file wasn't saved");
            return;
        }

        var target = history.Current;
        while (target.Parent != null && target != history.Saved && IsBlenders(target, session))
        {
            target = target.Parent;
        }

        if (target != history.Current)
        {
            history.GoTo(target);
            DocumentViewModel.TellChanged(asset);
        }

        if (HeadlessOf(asset) == document && !document.IsDirty)
        {
            Headless.Remove(asset.URI);
            document.Close();
        }
    }

    // The asset as its files have it: its metadata and data read again (here, not lazily by every scene's render thread at once), which the
    // scenes drawing it and the documents showing it take like data replaced whole
    private static void ReadSaved(IAsset asset, DocumentViewModel closed)
    {
        var json = System.IO.Path.Combine(asset.FullPath, $"{asset.Name}.json");
        if (System.IO.File.Exists(json))
        {
            asset.Deserialize(System.IO.File.ReadAllText(json));
        }

        asset.UnloadData();
        asset.GetData<AbstractAssetData>();
        asset.GetResourceTreeElement().NotifyOfPropertyChange(nameof(IAsset.Alias));
        DocumentViewModel.TellDataReplaced(closed.PropertyGraph, asset);
        DocumentViewModel.TellChanged(asset);
    }

    /// <summary>
    /// Data replaced whole in a document (a model or picture Blender wrote, one of those steps undone or redone): the other open documents
    /// showing the asset under their links show it. A chunk's view of a shared asset whose data can't have the chunk's own values has the
    /// asset's data object, the new one from now on
    /// </summary>
    public static void FollowReplacedData(PropertyGraph changed, IAsset asset)
    {
        foreach (var document in Documents().Where(document => document.PropertyGraph != changed).ToList())
        {
            var graph = document.PropertyGraph;
            if (graph.Overrides?.ViewOf(asset) is { } view && asset.IsLoaded && !AssetOverrides.CanOverrideData(asset.GetData<AbstractAssetData>().GetType()))
            {
                view.SetViewData(asset.GetData<AbstractAssetData>());
                graph.Follow(view, [$".{nameof(SerializableAsset.AssetData)}"]);
                continue;
            }

            graph.Follow(asset, [$".{nameof(SerializableAsset.AssetData)}"]);
        }
    }

    /// <summary>
    /// The asset's tab opens, its document made over the same data: the document of no tab Blender changed it in goes into it with its
    /// history. A chunk's scene takes what Blender changed of its scenery and didn't save, saved with the chunk (the history of no tab's
    /// document is of other paths)
    /// </summary>
    internal static TabbedEditorViewModel.KeptState? HandOver(IAsset asset)
    {
        if (asset.URI == null)
        {
            return null;
        }

        var carried = new List<DocumentViewModel.Carried>();
        if (HeadlessOf(asset) is { } own)
        {
            Headless.Remove(asset.URI);
            carried.Add(own.Carry(new HashSet<LabURI>(), asset.Alias));
            own.Close();
        }

        if (asset is LevelChunk chunk)
        {
            var assetManager = AssetManager.Get();
            foreach (var uri in Headless.Keys.Where(uri => assetManager.DoesAssetExist(uri) && ReferenceEquals(assetManager.GetAsset(uri), Headless[uri].DocumentModel)
                                                            && LevelChunk.Owning(assetManager.GetAsset(uri)) == chunk).ToList())
            {
                Headless.Remove(uri, out var resource);
                carried.Add(resource!.Carry(new HashSet<LabURI> { uri }, $"{resource.DocumentModel.DocumentName} as Blender changed it"));
                resource.Close();
            }
        }

        if (carried.Count == 0)
        {
            return null;
        }

        var kept = carried.Count == 1 ? carried[0] : carried[0] with
        {
            IsDirty = carried.Any(each => each.IsDirty),
            ChangedAssets = carried.SelectMany(each => each.ChangedAssets).Distinct().ToList(),
            History = null,
            Reloaded = string.Join(", ", carried.Select(each => each.Reloaded))
        };
        return new TabbedEditorViewModel.KeptState(null, null, kept);
    }

    /// <summary>
    /// What the documents of no tab read, whose data stays loaded for them
    /// </summary>
    public static IEnumerable<LabURI> ReferencedAssets()
    {
        foreach (var (uri, document) in Headless)
        {
            yield return uri;
            foreach (var explored in document.PropertyGraph.Tracker.ExploredUris)
            {
                yield return explored;
            }
        }
    }

    public static bool Uses(LabURI uri) => ReferencedAssets().Contains(uri);

    /// <summary>
    /// Another program wrote the assets' files, Blender's unsaved changes to them go
    /// </summary>
    public static void Release(IReadOnlySet<LabURI> assets)
    {
        foreach (var uri in Headless.Keys.Where(assets.Contains).ToList())
        {
            Headless.Remove(uri, out var document);
            document!.Close();
            Log.WriteLine($"{document.DocumentModel.DocumentName} changed outside TT Lab, what Blender changed of it and didn't save went", Log.LogType.Warning);
        }
    }

    /// <summary>
    /// The asset left the project
    /// </summary>
    public static void Forget(LabURI asset)
    {
        if (Headless.Remove(asset, out var document))
        {
            document.Close();
        }
    }

    /// <summary>
    /// The project closed, what Blender didn't save goes with it
    /// </summary>
    public static void CloseAll()
    {
        foreach (var document in Headless.Values)
        {
            document.Close();
        }

        Headless.Clear();
    }
}
