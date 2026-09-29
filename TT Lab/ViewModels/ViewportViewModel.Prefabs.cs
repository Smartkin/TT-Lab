using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables.Fluent;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using GlmSharp;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using TT_Lab.Assets;
using TT_Lab.Assets.Instance;
using TT_Lab.Project.Prefabs;
using TT_Lab.Rendering;
using TT_Lab.Util;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using TT_Lab.ViewModels.Interfaces;

namespace TT_Lab.ViewModels;

// Prefabs: what the selection can be saved as, and placing one at the cursor or where it got dropped. The Prefabs panel drives it
public partial class ViewportViewModel
{
    private const string PrefabsHint = "Select an instance, or a part of a resource like a chunk link or an emitter, to save it as a prefab";

    [Reactive(SetModifier = AccessModifier.Private)]
    private string _prefabDefaultName = string.Empty;

    [Reactive(SetModifier = AccessModifier.Private)]
    private bool _canSavePrefab;

    [Reactive(SetModifier = AccessModifier.Private)]
    private string _prefabHint = PrefabsHint;

    private void InitPrefabs()
    {
        this.WhenAnyValue(x => x.SelectedObject, x => x.SelectionCount).Subscribe(_ => FollowPrefabSource(SelectedObject)).DisposeWith(_closeDisposables);
    }

    private void FollowPrefabSource(ViewportObject? selected)
    {
        if (selected == null)
        {
            CanSavePrefab = false;
            PrefabHint = PrefabsHint;
            return;
        }

        if (SelectionCount > 1)
        {
            var members = GroupMembers();
            CanSavePrefab = members != null;
            PrefabDefaultName = $"Group of {SelectionCount}";
            PrefabHint = members == null
                ? "Only instances can be saved together, take the parts of resources out of the selection"
                : $"The {members.Count} instances are saved together, placed around the cursor the way they stand to each other";
            return;
        }

        if (!PrefabLibrary.TryGetSource(selected.Property, selected.DuplicatedElement, out var source, out var reason))
        {
            CanSavePrefab = false;
            PrefabHint = reason;
            return;
        }

        CanSavePrefab = true;
        PrefabDefaultName = source.DefaultName;
        PrefabHint = source.Kind == PrefabKind.Instance
            ? "Everything of it but its links to the chunk's instances, positions and paths is kept"
            : $"Saved as a part to put into any chunk's {PrefabLibrary.Describe(source.Asset.GetType()).ToLowerInvariant()}";
    }

    /// <summary>
    /// Saves the selection as a prefab of the project under the name, or the selection's own name when it's empty
    /// </summary>
    internal async Task<bool> SavePrefabAsync(string name)
    {
        var library = PrefabLibrary.ForOpenedProject();
        if (library == null || SelectedObject is not { } selected)
        {
            return false;
        }

        name = name.Trim();
        try
        {
            Prefab prefab;
            if (SelectionCount > 1)
            {
                if (GroupMembers() is not { } members)
                {
                    return false;
                }

                prefab = library.CaptureGroup(members, name.Length == 0 ? $"Group of {members.Count}" : name);
            }
            else
            {
                if (!PrefabLibrary.TryGetSource(selected.Property, selected.DuplicatedElement, out var source, out _))
                {
                    return false;
                }

                prefab = library.Capture(source, name.Length == 0 ? source.DefaultName : name);
            }

            var preview = await RenderPrefabPreviewAsync();
            library.Save(prefab, preview);
            Log.WriteLine($"Saved prefab {prefab.Name}", Log.LogType.Info);
            return true;
        }
        catch (Exception e)
        {
            Log.WriteLine($"Prefab {name} couldn't be saved: {e.Message}", Log.LogType.Error);
            return false;
        }
    }

    // The selected instances with where they are, none when something selected isn't an instance of a layout
    private List<(SerializableInstance Asset, vec3 Position)>? GroupMembers()
    {
        var members = new List<(SerializableInstance, vec3)>();
        foreach (var viewportObject in SelectedObjects)
        {
            if (viewportObject.DuplicatedElement != null || viewportObject.Property.Find("[data]")?.GetValue() is not SerializableInstance { LayoutID: not null } asset)
            {
                return null;
            }

            if (members.Any(member => member.Item1 == asset))
            {
                continue;
            }

            members.Add((asset, viewportObject.Render.WorldTransform.Column3.xyz));
        }

        return members;
    }

    // A picture of the selection on its own, rendered on the render thread between two frames: the rest of the scene hidden, the
    // camera at a three quarter view framing it. None when the scene isn't rendering
    internal Task<Bitmap?> RenderPrefabPreviewAsync()
    {
        var context = _renderContext;
        var renderer = _renderer;
        var scene = _scene;
        if (context == null || renderer == null || scene == null || !_renderInit || SelectedObject == null)
        {
            return Task.FromResult<Bitmap?>(null);
        }

        var properties = SelectedObjects.Select(viewportObject => viewportObject.Property).ToHashSet();
        var elements = SelectedObjects.Where(viewportObject => viewportObject.DuplicatedElement != null).Select(viewportObject => viewportObject.DuplicatedElement!.Path).ToList();
        bool IsShown(ViewportObject viewportObject) => properties.Contains(viewportObject.Property) && (elements.Count == 0 || elements.Any(path => IsOfElement(viewportObject, path)));
        var completion = new TaskCompletionSource<Bitmap?>(TaskCreationOptions.RunContinuationsAsynchronously);
        context.QueueRenderAction(() =>
        {
            try
            {
                completion.TrySetResult(PreviewRender.Render(context, renderer, scene, _viewportObjects, IsShown, PreviewImage.Size));
            }
            catch (Exception e)
            {
                Log.WriteLine($"Couldn't take a picture of the selection: {e.Message}", Log.LogType.Warning);
                completion.TrySetResult(null);
            }
        });
        // A viewport that isn't rendering only gets to the picture once it does
        return Task.WhenAny(completion.Task, Task.Delay(PreviewTimeout)).ContinueWith(_ => completion.Task.IsCompletedSuccessfully ? completion.Task.Result : null);
    }

    private static readonly TimeSpan PreviewTimeout = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Places the prefab where the ray through the viewport point hits the chunk's collision, or at the cursor when it hits nothing
    /// </summary>
    internal bool PlacePrefabAt(Prefab prefab, float x, float y)
    {
        if (_editingContext != null && TryHitCollision(x, y, out var hit))
        {
            _editingContext.SetCursorCoordinates(hit);
        }

        return PlacePrefab(prefab);
    }

    /// <summary>
    /// Places the prefab at the cursor, selected and shown in the inspector, as one step to undo
    /// </summary>
    internal bool PlacePrefab(Prefab prefab)
    {
        var library = PrefabLibrary.ForOpenedProject();
        if (library == null || _document?.DocumentModel is not LevelChunk chunk || _renderContext == null || _editingContext == null)
        {
            return false;
        }

        if (!library.CanPlace(prefab, chunk, _document, out var reason))
        {
            Log.WriteLine($"Prefab {prefab.Name} can't be placed here: {reason}", Log.LogType.Warning);
            return false;
        }

        try
        {
            switch (prefab.Kind)
            {
                case PrefabKind.Instance:
                    PlaceInstance(library.PlaceInstance(prefab, chunk), true, $"Placed {prefab.Name}");
                    break;
                case PrefabKind.Group:
                    var cursor = _editingContext.GetCursorCoordinates();
                    PlaceInstances(library.PlaceGroup(prefab, chunk).Select(placed => ((IAsset)placed.Instance, (vec3?)(cursor + placed.Offset))).ToList(), $"Placed {prefab.Name}");
                    break;
                default:
                    PlaceElement(library, prefab);
                    break;
            }
        }
        catch (Exception e)
        {
            Log.WriteLine($"Prefab {prefab.Name} couldn't be placed: {e.Message}", Log.LogType.Error);
            return false;
        }

        return true;
    }

    // The element goes into its list and the resource's objects get made again, the ones standing for it are moved to the cursor
    // and the first of them selected, one step to undo
    private void PlaceElement(PrefabLibrary library, Prefab prefab)
    {
        var placing = _document!.History.BeginGroup($"Placed {prefab.Name}");
        PropertyNode element;
        _isPlacingElement = true;
        try
        {
            element = library.PlaceElement(prefab, _document);
        }
        catch
        {
            placing.Dispose();
            throw;
        }
        finally
        {
            _isPlacingElement = false;
        }

        ShowPlacedElement(element, placing);
    }

    /// <summary>
    /// The data node of the chunk's resource of the asset type (its links, its particles), none when the chunk has no such resource
    /// </summary>
    internal PropertyNode? FindResourceData(Type assetType)
    {
        return _document?.PropertyGraph.Root.Find(nameof(LevelChunk.ChunkResources))?.Children
            .Select(resource => resource.Find("[data]"))
            .FirstOrDefault(data => data?.GetValue()?.GetType() == assetType);
    }

    /// <summary>
    /// Puts a new element at the end of a list of one of the chunk's resources (a link, an emitter) and shows it at the cursor, selected,
    /// as one step to undo. The node of the element, none when the chunk has no such resource
    /// </summary>
    internal PropertyNode? CreateElement(Type resourceAssetType, string listPath, object element, string description)
    {
        var list = FindResourceData(resourceAssetType)?.Find(listPath);
        if (list == null || _document == null)
        {
            Log.WriteLine($"The chunk has no {PrefabLibrary.Describe(resourceAssetType).ToLowerInvariant()} to put it into", Log.LogType.Warning);
            return null;
        }

        if (list.IsFull)
        {
            Log.WriteLine($"{list.Name} has {list.MaxElements}, as many as the game takes", Log.LogType.Warning);
            return null;
        }

        var placing = _document.History.BeginGroup(description);
        var count = (list.GetValue() as System.Collections.IList)?.Count ?? list.Children.Count;
        PropertyNode? node;
        _isPlacingElement = true;
        try
        {
            node = list.InsertElement(count, element);
        }
        finally
        {
            _isPlacingElement = false;
        }

        if (node == null)
        {
            placing.Dispose();
            return null;
        }

        ShowPlacedElement(node, placing);
        return node;
    }

    // Makes the resource's objects again with the element among them, moves the element's objects that have a position to the cursor
    // and selects the first of them. The group ends once they're there
    private void ShowPlacedElement(PropertyNode element, IDisposable placing)
    {
        var resource = _document!.PropertyGraph.Find(element.Path[..element.Path.IndexOf("[data]", StringComparison.Ordinal)]);
        if (resource == null)
        {
            placing.Dispose();
            return;
        }

        var elementPath = element.Path;
        var cursorCoords = NewResourcePosition();
        RebuildViewportObjects(resource, viewportObject => IsOfElement(viewportObject, elementPath) && viewportObject.Render.IsSelectable, true, viewportObjects =>
        {
            using (placing)
            {
                foreach (var viewportObject in viewportObjects.Where(viewportObject => IsOfElement(viewportObject, elementPath) && viewportObject.Position != null
                                                                                     && viewportObject.PositionConverter == null))
                {
                    viewportObject.Render.SetPosition(cursorCoords);
                    viewportObject.Position!.SetValue(ViewportObject.PositionValue(viewportObject.Position, cursorCoords));
                }
            }
        });
    }

    // The objects of an element are named after the paths of its properties
    private static bool IsOfElement(ViewportObject viewportObject, string elementPath)
    {
        var name = viewportObject.DocumentName;
        return name.EndsWith(elementPath, StringComparison.Ordinal) || name.Contains($"{elementPath}.", StringComparison.Ordinal) || name.Contains($"{elementPath}[", StringComparison.Ordinal);
    }
}
