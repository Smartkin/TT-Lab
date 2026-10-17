using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Newtonsoft.Json;
using Splat;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Graphics;
using TT_Lab.AssetData.Graphics.TlModel;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Graphics;
using TT_Lab.Project;
using TT_Lab.ServiceProviders;
using TT_Lab.Util;
using TT_Lab.ViewModels.Interfaces;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.PropertyGraph;

namespace TT_Lab.Tools.Blender;

/// <summary>
/// What a linked Blender asks of the project (LINK.md), on the UI thread, and what TT Lab tells it back. What it makes is the project's at
/// once but written only when it saves, what it changes goes into the documents of the assets as steps of their histories
/// (<see cref="LinkedDocuments"/>); dropped, what it didn't save goes
/// </summary>
public sealed class BlenderLinkAssets(BlenderLinkSession session)
{
    /// <summary>
    /// Parameters of a texture made of Blender's picture: the SHA-256 of the PNG Blender sent, and of the PNG of the texture made of it.
    /// While the texture is still what it was made of, Blender's picture is the one it knows, at its size (the game's textures are at most
    /// 256 pixels across)
    /// </summary>
    public const string BlenderPngParameter = "BlenderPng";
    public const string BlenderPictureParameter = "BlenderPicture";

    private static readonly ConcurrentDictionary<Type, Type?> DataTypes = new();

    // The models it linked, what its models draw (what TT Lab changes of them goes to it) by the names it knows them by
    private readonly HashSet<LabURI> _models = [];
    private readonly Dictionary<LabURI, string> _used = [];
    // What it made and didn't save, which has no files yet, and what it changed since it saved
    private readonly HashSet<LabURI> _made = [];
    private readonly HashSet<LabURI> _changed = [];
    // The revision of each of its models TT Lab last answered or told it: what TT Lab changes that leaves it so (a name, a save of what it
    // has) tells it nothing
    private readonly Dictionary<LabURI, string> _told = [];

    // The revisions Blender keeps of what it read of a file without TT Lab, and of what it wrote into one (LINK.md)
    private const string FileRevisionPrefix = "file:";
    private const string ExportedRevisionPrefix = "exported:";

    /// <summary>
    /// The Blender whose change TT Lab applies: what TT Lab tells of it goes to the other Blenders only
    /// </summary>
    internal static BlenderLinkSession? Applying { get; private set; }

    public IReadOnlyCollection<LabURI> Models => _models;

    /// <summary>
    /// The models it linked and what they draw
    /// </summary>
    public IEnumerable<LabURI> Linked => _models.Concat(_used.Keys);

    public async Task<(JsonObject Header, byte[]? Payload)> HandleAsync(LinkProtocol.Frame frame)
    {
        return frame.Type switch
        {
            "link_model" => LinkModel(frame),
            "unlink_model" => UnlinkModel(frame),
            "get_picture" => GetPicture(frame),
            "get_model" => GetModel(frame),
            "create_picture" => await CreatePicture(frame),
            "update_picture" => await UpdatePicture(frame),
            "create_material" => await CreateMaterial(frame),
            "update_material" => await UpdateMaterial(frame),
            "rename" => await Rename(frame),
            "check_model" => CheckModel(frame),
            "write_model" => await WriteModel(frame),
            "save" => Save(),
            "blend_saved" => BlendSaved(frame),
            "drop" => DropAnswer(),
            "open_asset" => OpenAsset(frame),
            "open_project" => OpenProject(frame),
            "new_model_targets" => NewModelTargets(),
            "create_model" => CreateModel(frame),
            "ping" => (Answer("ok"), null),
            _ => throw new InvalidOperationException($"TT Lab doesn't know {frame.Type}, install the add-on it comes with")
        };
    }

    // What TT Lab's model and Blender's draw (Blender's model may be another one put in its place), and what of Blender's the project
    // doesn't have: Blender makes it again
    private (JsonObject, byte[]?) LinkModel(LinkProtocol.Frame frame)
    {
        var path = Text(frame, "path");
        var model = ModelAt(path) ?? throw new InvalidOperationException($"The project has no model of the file {path}");
        _models.Add(model.URI);
        var materials = new JsonArray();
        var pictures = new JsonArray();
        var textures = new List<Texture>();
        var missing = new JsonArray();
        var drawn = MaterialsOf(model);
        foreach (var uri in Uris(frame, "materials"))
        {
            if (Existing<Material>(uri) is { } material)
            {
                if (!drawn.Contains(material))
                {
                    drawn.Add(material);
                }
            }
            else
            {
                missing.Add(uri.ToString());
            }
        }

        foreach (var uri in Uris(frame, "pictures"))
        {
            if (Existing<Texture>(uri) is { } texture)
            {
                textures.Add(texture);
            }
            else
            {
                missing.Add(uri.ToString());
            }
        }

        foreach (var material in drawn)
        {
            _used[material.URI] = material.Alias;
            materials.Add(MaterialJson(material));
            foreach (var texture in TexturesOf(material).Where(texture => !textures.Contains(texture)))
            {
                textures.Add(texture);
            }
        }

        foreach (var texture in textures)
        {
            _used[texture.URI] = texture.Alias;
            pictures.Add(new JsonObject
            {
                ["uri"] = texture.URI.ToString(),
                ["name"] = texture.Alias,
                ["hash"] = PictureHash(texture),
                ["blender_id"] = Parameter(texture, TlmMaterials.BlenderImageParameter)
            });
        }

        // Whether TT Lab changed the model since what Blender has of it (the revision it keeps), and what it has now
        var changed = !IsCurrent(model, OptionalText(frame, "revision"));
        var revision = RevisionOf(model);
        _told[model.URI] = revision;
        return (new JsonObject
        {
            ["type"] = "model",
            ["uri"] = model.URI.ToString(),
            ["revision"] = revision,
            ["changed"] = changed,
            ["materials"] = materials,
            ["pictures"] = pictures,
            ["missing"] = missing
        }, null);
    }

    // Take TT Lab's (the add-on's question after a save, Import the Model Again of a linked model): the model as TT Lab has it, its unsaved
    // changes too, and its revision, which Blender keeps once it took it
    private (JsonObject, byte[]?) GetModel(LinkProtocol.Frame frame)
    {
        var model = AssetOf<IAsset>(frame);
        var revision = RevisionOf(model);
        _told[model.URI] = revision;
        return (new JsonObject { ["type"] = "model", ["uri"] = model.URI.ToString(), ["revision"] = revision },
            AssetDataBytes.Of(model.GetData<AbstractAssetData>(), model));
    }

    // Its model was made another model, or left the blend file
    private (JsonObject, byte[]?) UnlinkModel(LinkProtocol.Frame frame)
    {
        _models.Remove(new LabURI(Text(frame, "uri")));
        return (Answer("ok"), null);
    }

    private (JsonObject, byte[]?) GetPicture(LinkProtocol.Frame frame)
    {
        var texture = AssetOf<Texture>(frame);
        var answer = Answer("picture");
        answer["name"] = texture.Alias;
        return (answer, DataOf<TextureData>(texture).GetPngBytes());
    }

    // Asked again (its answer lost, the model's file brought it first), the one made for the model of the Blender ID is the one
    private async Task<(JsonObject, byte[]?)> CreatePicture(LinkProtocol.Frame frame)
    {
        var owner = AssetOf<IAsset>(frame, "owner");
        var blenderId = OptionalText(frame, "blender_id");
        if (MadeFor<Texture>(owner, blenderId, TlmMaterials.BlenderImageParameter) is { } made)
        {
            await ApplyPicture(made, frame.Payload);
            return (new JsonObject { ["type"] = "created", ["uri"] = made.URI.ToString() }, null);
        }

        var texture = BlenderAssets.CreateTexture(owner, Name(frame), frame.Payload, blenderId, unsaved: true);
        Remember(texture, frame.Payload);
        _made.Add(texture.URI);
        _used[texture.URI] = texture.Alias;
        return (new JsonObject { ["type"] = "created", ["uri"] = texture.URI.ToString() }, null);
    }

    private async Task<(JsonObject, byte[]?)> UpdatePicture(LinkProtocol.Frame frame)
    {
        await ApplyPicture(AssetOf<Texture>(frame), frame.Payload);
        return (Answer("ok"), null);
    }

    private async Task ApplyPicture(Texture texture, byte[] png)
    {
        var place = await LinkedDocuments.ForAsync(texture);
        var document = place.Document;
        var current = DataOf<TextureData>(texture);
        var made = BlenderAssets.GameTexture(texture, texture.Alias, png);
        var size = made.Bitmap!.PixelSize;
        if (current.Bitmap != null && current.Bitmap.PixelSize == size && made.GetPixels().AsSpan().SequenceEqual(current.GetPixels()))
        {
            // The same picture at the game's size: Blender's PNG is the texture's from now on, saved with what it saves
            made.Dispose();
            Remember(texture, png);
            Changed(texture);
            return;
        }

        using (Apply())
        using (LinkedDocuments.Step(document, texture.Alias, session))
        {
            // A picture of another size gets the layout the game's tools gave pictures of its size, in the same step (undone with it)
            if (current.Bitmap == null || current.Bitmap.PixelSize != size)
            {
                var layout = new Texture();
                layout.UseGameLayout(size.Width, size.Height);
                place.Value(nameof(Texture.PixelFormat)).SetValue(layout.PixelFormat);
                place.Value(nameof(Texture.TextureFunction)).SetValue(layout.TextureFunction);
                place.Value(nameof(Texture.GenerateMipmaps)).SetValue(layout.GenerateMipmaps);
            }

            document.ReplaceData(place.Data, made, LinkedDocuments.Describe(texture.Alias));
        }

        Remember(texture, png);
        Changed(texture);
    }

    private async Task<(JsonObject, byte[]?)> CreateMaterial(LinkProtocol.Frame frame)
    {
        var owner = AssetOf<IAsset>(frame, "owner");
        var settings = WithPictures(Object(frame, "data"), frame.Header["images"] as JsonArray, owner.Package, Name(frame));
        var blenderId = OptionalText(frame, "blender_id");
        if (MadeFor<Material>(owner, blenderId, TlmMaterials.BlenderMaterialParameter) is { } made)
        {
            await ApplyMaterial(made, settings, null, null);
            return (new JsonObject { ["type"] = "created", ["uri"] = made.URI.ToString() }, null);
        }

        var probe = new MaterialData(owner);
        probe.PopulateFrom(settings.ToJsonString());
        var material = BlenderAssets.CreateMaterial(owner, Name(frame), asset =>
        {
            var data = new MaterialData(asset);
            data.PopulateFrom(settings.ToJsonString());
            return data;
        }, blenderId, TlmMaterials.DrawsSkins(probe) ? TlmMaterialUse.Skin : TlmMaterialUse.Rigid, unsaved: true);
        _made.Add(material.URI);
        _used[material.URI] = material.Alias;
        return (new JsonObject { ["type"] = "created", ["uri"] = material.URI.ToString() }, null);
    }

    private async Task<(JsonObject, byte[]?)> UpdateMaterial(LinkProtocol.Frame frame)
    {
        var material = AssetOf<Material>(frame);
        var settings = WithPictures(Object(frame, "data"), frame.Header["images"] as JsonArray, material.Package, material.Alias);
        await ApplyMaterial(material, settings, frame.Header["changed"] as JsonArray, frame.Header["shader_sources"] as JsonArray);
        return (Answer("ok"), null);
    }

    // What Blender changed since it last had TT Lab's (the paths changed, its shaders from TT Lab's at their sources), without them all of it
    private async Task ApplyMaterial(Material material, JsonObject settings, JsonArray? changed, JsonArray? sources)
    {
        var place = await LinkedDocuments.ForAsync(material);
        var document = place.Document;
        var before = JsonConvert.SerializeObject(DataOf<MaterialData>(material));
        var result = changed != null ? BlenderAssets.WithChanges(JsonNode.Parse(before)!.AsObject(), settings, changed, sources) : settings;
        var target = new MaterialData(material);
        target.PopulateFrom(result.ToJsonString());
        if (JsonConvert.SerializeObject(target) == before)
        {
            return;
        }

        using (Apply())
        using (LinkedDocuments.Step(document, material.Alias, session))
        {
            NodeValues.Apply(place.Data, target);
            // What has no nodes of its own, the data whole
            if (JsonConvert.SerializeObject(DataOf<MaterialData>(material)) != JsonConvert.SerializeObject(target))
            {
                document.ReplaceData(place.Data, target, LinkedDocuments.Describe(material.Alias));
            }
        }

        Changed(material);
        LinkedDocuments.ShowElsewhere(document, material);
    }

    private static T? MadeFor<T>(IAsset owner, string? blenderId, string parameter) where T : class, IAsset
    {
        return blenderId == null ? null : AssetManager.Get().GetAllAssetsOf<T>().FirstOrDefault(asset => !asset.IsInternal && BlenderAssets.IsOwnedBy(asset, owner) && Parameter(asset, parameter) == blenderId);
    }

    private async Task<(JsonObject, byte[]?)> Rename(LinkProtocol.Frame frame)
    {
        var asset = AssetOf<IAsset>(frame);
        var name = asset is Texture ? BlenderAssets.WithoutPictureExtension(Text(frame, "name")) : Text(frame, "name").Trim();
        if (name.Length == 0 || !NameRules.IsAscii(name))
        {
            throw new InvalidOperationException($"{asset.Alias} isn't renamed to {name}: a name {NameRules.AsciiOnly}");
        }

        if (name != asset.Alias)
        {
            var place = await LinkedDocuments.ForAsync(asset);
            using (Apply())
            using (LinkedDocuments.Step(place.Document, $"{asset.Alias} renamed to {name}", session))
            {
                place.Value(nameof(IAsset.Alias)).SetValue(name);
            }

            asset.GetResourceTreeElement().NotifyOfPropertyChange(nameof(IAsset.Alias));
            Changed(asset);
        }

        if (_used.ContainsKey(asset.URI))
        {
            _used[asset.URI] = asset.Alias;
        }

        return (new JsonObject { ["type"] = "ok", ["uri"] = asset.URI.ToString() }, null);
    }

    private (JsonObject, byte[]?) CheckModel(LinkProtocol.Frame frame)
    {
        var model = AssetOf<IAsset>(frame);
        return (new JsonObject { ["type"] = "checked", ["problems"] = ProblemsJson(Check(model, frame.Payload)) }, null);
    }

    private async Task<(JsonObject, byte[]?)> WriteModel(LinkProtocol.Frame frame)
    {
        var model = AssetOf<IAsset>(frame);
        // The data before is the document's. A chunk's scenery is a step of its chunk's scene. A document with unsaved changes gets it as one
        // more: its file stays what it was until TT Lab saves it, undoing to where it was saved had the file of Blender's under data marked
        // saved. A model Blender didn't change (a save writes every model) is no step and leaves the file alone. Asked once the document's
        // ready: what changed while it waited (the step being made) is TT Lab's change too
        var place = await LinkedDocuments.ForAsync(model);
        var document = place.Document;
        if (!Flag(frame, "force") && !IsCurrent(model, OptionalText(frame, "revision")))
        {
            var revision = RevisionOf(model);
            _told[model.URI] = revision;
            return (new JsonObject { ["type"] = "conflict", ["revision"] = revision, ["changed"] = true }, null);
        }

        var problems = Check(model, frame.Payload);
        if (problems.Any(problem => problem.Refused))
        {
            return (new JsonObject { ["type"] = "refused", ["text"] = problems.First(problem => problem.Refused).Text, ["problems"] = ProblemsJson(problems) }, null);
        }

        var wasSaved = !document.IsDirty;
        var data = AssetDataBytes.Read(model, DataTypeOf(model), frame.Payload);

        // Use an Existing Blend File: another model of a blend file takes its place
        var replacedBy = Flag(frame, "replacing") ? ReplacedBy(OptionalText(frame, "name"), OptionalText(frame, "blend")) : null;
        var step = replacedBy != null ? $"{model.Alias} replaced by {replacedBy}" : $"{model.Alias} written";
        bool changed;
        using (Apply())
        {
            using (LinkedDocuments.Step(document, step, session))
            {
                changed = document.ReplaceData(place.Data, data, LinkedDocuments.Describe(step));
            }

            // As TT Lab writes the data, what plays the project builds it again. Unsaved changes of TT Lab's stay unsaved
            if (changed && wasSaved)
            {
                document.Save();
                SaveMadeDrawnBy(model);
            }
        }

        if (replacedBy != null)
        {
            Log.WriteLine(changed ? $"{model.Alias}'s model is {replacedBy} now{(wasSaved ? string.Empty : ", unsaved like the changes before it")}, undoing " +
                                    $"\"{LinkedDocuments.Describe(step)}\" in its history brings the one before back"
                                  : $"{replacedBy} took {model.Alias}'s place, the same model");
            // The Blenders that had the model before stand for it no longer: their saves would take it back
            foreach (var other in session.Server.Sessions.Where(other => other != session && other.Assets.Models.Contains(model.URI)).ToList())
            {
                other.Assets.Release(model, replacedBy);
            }
        }

        foreach (var problem in problems)
        {
            Log.WriteLine($"{model.Alias} from Blender: {problem.Text}", Log.LogType.Warning);
        }

        _models.Add(model.URI);
        var written = RevisionOf(model);
        _told[model.URI] = written;
        return (new JsonObject { ["type"] = "ok", ["revision"] = written, ["problems"] = ProblemsJson(problems) }, null);
    }

    /// <summary>
    /// TT Lab's revision of the model, what Blender knows what it has of it by: the SHA-256 of its data file while no open document has
    /// unsaved changes of it, else of its data as TT Lab writes it, the file's once it's saved. Steps of a document that leave the model as
    /// it was (a chunk's scene moving instances) leave it, and so does saving what a Blender wrote
    /// </summary>
    internal static string RevisionOf(IAsset model)
    {
        return HasUnsavedChanges(model) ? Sha256(AssetDataBytes.Of(model.GetData<AbstractAssetData>(), model)) : FileRevision(model.FullDataPath);
    }

    // Whether what Blender has of the model (the revision it keeps) is what TT Lab has: the model's revision, the file it read without TT
    // Lab, what it wrote into the file without TT Lab (taken and written again by TT Lab since, TakenExports). A Blender that keeps none read
    // it before revisions were kept: TT Lab's saved model
    private static bool IsCurrent(IAsset model, string? blenders)
    {
        blenders ??= string.Empty;
        if (blenders.Length == 0)
        {
            return !HasUnsavedChanges(model);
        }

        var path = model.FullDataPath;
        if (blenders.StartsWith(FileRevisionPrefix, StringComparison.Ordinal))
        {
            return !HasUnsavedChanges(model) && FileRevision(path) == blenders[FileRevisionPrefix.Length..];
        }

        if (blenders.StartsWith(ExportedRevisionPrefix, StringComparison.Ordinal))
        {
            var written = blenders[ExportedRevisionPrefix.Length..];
            var file = FileRevision(path);
            return !HasUnsavedChanges(model) && (file == written || TakenExports.Took(path, written, file));
        }

        return blenders == RevisionOf(model);
    }

    // The model saved draws what Blender made for it: saved with it, a drop would take it from under the model's file
    private void SaveMadeDrawnBy(IAsset model)
    {
        var assetManager = AssetManager.Get();
        var drawn = AssetLinks.UrisInModelFile(model.FullDataPath).ToHashSet();
        foreach (var uri in drawn.ToList())
        {
            var link = new LabURI(uri);
            if (_made.Contains(link) && assetManager.DoesAssetExist(link) && assetManager.GetAsset(link) is Material material)
            {
                drawn.UnionWith(TexturesOf(material).Select(texture => texture.URI.ToString()));
            }
        }

        foreach (var uri in _made.Where(uri => drawn.Contains(uri.ToString()) && assetManager.DoesAssetExist(uri)).ToList())
        {
            SaveAsset(assetManager.GetAsset(uri));
            _made.Remove(uri);
        }
    }

    private static void SaveAsset(IAsset asset)
    {
        if (LinkedDocuments.Find(asset) is { } place)
        {
            place.Document.Save();
        }
        else
        {
            asset.Serialize(SerializationFlags.SaveData | SerializationFlags.PreserveData | SerializationFlags.FixReferences);
        }
    }

    private (JsonObject, byte[]?) Save()
    {
        var assetManager = AssetManager.Get();
        using (Apply())
        {
            foreach (var uri in _made.Concat(_changed).Distinct().Where(assetManager.DoesAssetExist).ToList())
            {
                SaveAsset(assetManager.GetAsset(uri));
            }
        }

        _made.Clear();
        _changed.Clear();
        return (Answer("ok"), null);
    }

    // Edit in Blender opens the blend file its models were saved in from then on, and no longer for the models whose model in it became
    // another's (their data files): this project's by their models, another project's (which TT Lab can't read) whatever of it the blend
    // file was remembered for
    private (JsonObject, byte[]?) BlendSaved(LinkProtocol.Frame frame)
    {
        var path = Text(frame, "path");
        session.BlendPath = path;
        if (Locator.Current.GetService<ProjectManager>()?.OpenedProject is Project.Project project)
        {
            // The models the blend file has as TT Lab has them, the add-on says (not one TT Lab changed since, or waiting to take another's
            // place): an older add-on says nothing, every model linked then
            var standing = frame.Header["models"] is JsonArray listed
                ? listed.Select(entry => entry is JsonValue value && value.TryGetValue<string>(out var text) ? new LabURI(text) : null).OfType<LabURI>().Where(_models.Contains).ToList()
                : _models.ToList();
            foreach (var model in standing)
            {
                BlendFiles.Remember(project.UUID, model, path);
            }

            // This project's models it had and has no more
            foreach (var released in (frame.Header["released"] as JsonArray ?? []).Select(entry => entry is JsonValue value && value.TryGetValue<string>(out var text) ? text : null))
            {
                if (!string.IsNullOrEmpty(released) && ModelAt(released) is { } model && !_models.Contains(model.URI))
                {
                    BlendFiles.Forget(project.UUID, model.URI, path);
                }
            }

            // Other projects it has no model of any more (which this TT Lab can't read): their models' blend file it isn't
            foreach (var other in (frame.Header["released_projects"] as JsonArray ?? []).Select(entry => entry is JsonValue value && value.TryGetValue<string>(out var text) ? text : null))
            {
                if (Guid.TryParse(other, out var uuid) && uuid != project.UUID)
                {
                    BlendFiles.ForgetProject(uuid, path);
                }
            }
        }

        return (Answer("ok"), null);
    }

    private (JsonObject, byte[]?) DropAnswer()
    {
        Drop();
        return (Answer("ok"), null);
    }

    /// <summary>
    /// The blend file closed without being saved: what the Blender made goes, what it changed gets back what it was
    /// </summary>
    internal void Drop()
    {
        var assetManager = AssetManager.Get();
        foreach (var uri in _changed.Where(uri => !_made.Contains(uri) && assetManager.DoesAssetExist(uri)).ToList())
        {
            LinkedDocuments.Drop(assetManager.GetAsset(uri), session);
        }

        // What it made goes with its documents first: a step of one of them (a picture swapped) doesn't keep a picture it made
        foreach (var uri in _made)
        {
            LinkedDocuments.Forget(uri);
        }

        // What a model TT Lab took draws (its file, a step bringing it back) stays the model's: saved, and what it draws with it. What only the
        // others it made draw goes with them
        var made = _made.Where(assetManager.DoesAssetExist).Select(assetManager.GetAsset).Where(asset => asset is SerializableAsset { IsUnsaved: true }).ToList();
        var used = BlenderAssets.UsersOf(made, null, made);
        var kept = made.Where(used).ToHashSet();
        foreach (var material in kept.OfType<Material>().ToList())
        {
            kept.UnionWith(TexturesOf(material).Where(made.Contains));
        }

        foreach (var asset in made)
        {
            if (kept.Contains(asset))
            {
                SaveAsset(asset);
                Log.WriteLine($"{asset.Alias} made in Blender stays, a model TT Lab took from it draws it");
                continue;
            }

            AssetDeletion.ForgetUnsaved((SerializableAsset)asset);
            Log.WriteLine($"{asset.Alias} made in Blender went, Blender's file wasn't saved");
        }

        _made.Clear();
        _changed.Clear();
        _models.Clear();
        _used.Clear();
        _told.Clear();
    }

    private (JsonObject, byte[]?) OpenAsset(LinkProtocol.Frame frame)
    {
        var asset = AssetOf<IAsset>(frame);
        Locator.Current.GetService<ILabManager>()?.OpenEditor(asset);
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } window })
        {
            window.Activate();
        }

        return (Answer("ok"), null);
    }

    // The kinds of model files a model made in Blender becomes an asset of
    private static readonly Dictionary<string, Type> NewModelTypes = new()
    {
        [OGIData.TlmAssetType] = typeof(OGI),
        [SkydomeData.TlmAssetType] = typeof(Skydome),
        [MeshData.TlmAssetType] = typeof(Mesh),
    };

    /// <summary>
    /// Where a model made in Blender can become an asset: the folders Create Asset makes models in (a package's, not its levels', a chunk's
    /// or the disc's), by their place in their package
    /// </summary>
    internal static IEnumerable<(Folder Folder, string Label)> NewModelFolders()
    {
        var assetManager = AssetManager.Get();
        const FolderMark NoModels = FolderMark.ChunksOnly | FolderMark.InChunk | FolderMark.DefaultOnly | FolderMark.IsChunk | FolderMark.Disc;
        foreach (var folder in assetManager.GetAllAssetsOf<Folder>())
        {
            if (folder.IsInternal || !folder.Mark.HasFlag(FolderMark.Normal) && !folder.Mark.HasFlag(FolderMark.IsPackage) || (folder.Mark & NoModels) != 0
                || folder.Parent == LabURI.Empty || folder.GetPathInLevels() != null || !assetManager.DoesAssetExist(folder.Package)
                || assetManager.GetAsset(folder.Package) is not Package { Enabled: true } package)
            {
                continue;
            }

            var names = new List<string>();
            for (var current = folder; current != null && current.Parent != LabURI.Empty && !current.Mark.HasFlag(FolderMark.IsPackage); current = assetManager.GetAsset(current.Parent) as Folder)
            {
                names.Insert(0, current.Alias);
            }

            yield return (folder, string.Join('/', new[] { package.Name }.Concat(names)));
        }
    }

    private (JsonObject, byte[]?) NewModelTargets()
    {
        var folders = new JsonArray(NewModelFolders().OrderBy(target => target.Label, StringComparer.OrdinalIgnoreCase)
            .Select(target => (JsonNode)new JsonObject { ["uri"] = target.Folder.URI.ToString(), ["label"] = target.Label }).ToArray());
        return (new JsonObject { ["type"] = "targets", ["folders"] = folders }, null);
    }

    // A model made in Blender (from a template) becomes an asset of the folder, its materials and pictures the model's own: written at
    // once, the model links by its file
    private (JsonObject, byte[]?) CreateModel(LinkProtocol.Frame frame)
    {
        var folder = AssetOf<Folder>(frame, "folder");
        if (NewModelFolders().All(target => target.Folder != folder))
        {
            throw new InvalidOperationException($"Models aren't made in {folder.Alias}");
        }

        var name = Text(frame, "name").Trim();
        if (!NameRules.IsAscii(name))
        {
            throw new InvalidOperationException($"{name} isn't a name: a name {NameRules.AsciiOnly}");
        }

        using var stream = new MemoryStream(frame.Payload);
        var assetType = TlmFile.Read(stream).AssetType;
        if (!NewModelTypes.TryGetValue(assetType, out var type))
        {
            throw new InvalidOperationException($"TT Lab doesn't make assets of {assetType} models made in Blender, only of {string.Join(", ", NewModelTypes.Keys)}");
        }

        string? failure = null;
        var made = AssetFactory.CreateAsset(type, folder, UniqueModelName(type, folder.Package, name), string.Empty, TwinIdGeneratorServiceProvider.GetGenerator(type), asset =>
        {
            try
            {
                asset.SetData(AssetDataBytes.Read(asset, DataTypeOf(asset), frame.Payload));
                return AssetCreationStatus.Success;
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                failure = exception.Message;
                return AssetCreationStatus.Failed;
            }
        });
        if (made == null)
        {
            throw new InvalidOperationException($"TT Lab can't read the model: {failure}");
        }

        Log.WriteLine($"Added {made.Alias} made in Blender to the project as {made.URI}");
        _models.Add(made.URI);
        return (new JsonObject { ["type"] = "created", ["uri"] = made.URI.ToString(), ["path"] = made.FullDataPath }, null);
    }

    private static string UniqueModelName(Type type, LabURI package, string name)
    {
        var taken = AssetManager.Get().GetAssets().Where(asset => asset.GetType() == type && asset.Package == package).Select(asset => asset.InvariantName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var candidate = name.Length == 0 ? "Model" : name;
        for (var number = 2; taken.Contains(candidate); number++)
        {
            candidate = $"{name} {number}";
        }

        return candidate;
    }

    // Another project's model (TT Lab has another one open): it opens, asking about the open one's changes first, and then the model
    private (JsonObject, byte[]?) OpenProject(LinkProtocol.Frame frame)
    {
        if (Locator.Current.GetService<ILabManager>() is not ViewModels.ShellViewModel shell)
        {
            throw new InvalidOperationException("TT Lab can't open projects now");
        }

        _ = shell.OpenProjectAndFile(Text(frame, "project"), OptionalText(frame, "path"));
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } window })
        {
            window.Activate();
        }

        return (Answer("ok"), null);
    }

    /// <summary>
    /// What TT Lab changed of an asset the Blender's models use (an edit, an undo, a save, its changes discarded), the Blender takes it
    /// </summary>
    internal void Tell(IAsset asset, bool saved)
    {
        if (_models.Contains(asset.URI))
        {
            // A model whose revision is the one it knows (its name changed, what it wrote saved) changed nothing it has
            var revision = RevisionOf(asset);
            if (_told.TryGetValue(asset.URI, out var told) && told == revision)
            {
                return;
            }

            _told[asset.URI] = revision;
            session.Push(new JsonObject { ["type"] = "model_changed", ["uri"] = asset.URI.ToString(), ["revision"] = revision });
            return;
        }

        if (!_used.TryGetValue(asset.URI, out var name))
        {
            return;
        }

        if (name != asset.Alias)
        {
            _used[asset.URI] = asset.Alias;
            session.Push(new JsonObject { ["type"] = "renamed", ["uri"] = asset.URI.ToString(), ["new_uri"] = asset.URI.ToString(), ["name"] = asset.Alias });
        }

        if (saved)
        {
            return;
        }

        switch (asset)
        {
            case Material material:
                session.Push(new JsonObject { ["type"] = "material_changed", ["uri"] = material.URI.ToString(), ["data"] = JsonNode.Parse(JsonConvert.SerializeObject(DataOf<MaterialData>(material))) });
                break;
            case Texture texture:
                session.Push(new JsonObject { ["type"] = "picture_changed", ["uri"] = texture.URI.ToString(), ["hash"] = PictureHash(texture) });
                break;
        }
    }

    /// <summary>
    /// Another Blender's model took the model's place (Use an Existing Blend File): this Blender's model of it stands for none from now on
    /// </summary>
    internal void Release(IAsset model, string by)
    {
        if (_models.Remove(model.URI))
        {
            session.Push(new JsonObject { ["type"] = "released", ["uri"] = model.URI.ToString(), ["name"] = model.Alias, ["by"] = by });
        }
    }

    /// <summary>
    /// The assets moved, the Blender keeps them by their new URIs
    /// </summary>
    internal void TellMoved(IReadOnlyDictionary<LabURI, LabURI> moved)
    {
        var assetManager = AssetManager.Get();
        foreach (var (from, to) in moved)
        {
            // Its model of the new place's file from now on, the blend file of it remembered for it there
            if (_models.Remove(from))
            {
                _models.Add(to);
                if (_told.Remove(from, out var revision))
                {
                    _told[to] = revision;
                }

                if (assetManager.DoesAssetExist(to))
                {
                    session.Push(new JsonObject { ["type"] = "moved", ["uri"] = from.ToString(), ["new_uri"] = to.ToString(), ["path"] = assetManager.GetAsset(to).FullDataPath });
                }
            }

            if (_made.Remove(from))
            {
                _made.Add(to);
            }

            if (_changed.Remove(from))
            {
                _changed.Add(to);
            }

            if (_used.Remove(from, out var name))
            {
                var alias = assetManager.DoesAssetExist(to) ? assetManager.GetAsset(to).Alias : name;
                _used[to] = alias;
                session.Push(new JsonObject { ["type"] = "renamed", ["uri"] = from.ToString(), ["new_uri"] = to.ToString(), ["name"] = alias });
            }
        }
    }

    /// <summary>
    /// What the Blender's models use that left the project
    /// </summary>
    internal void TellDeleted()
    {
        var assetManager = AssetManager.Get();
        foreach (var uri in _used.Keys.Concat(_models).Where(uri => !assetManager.DoesAssetExist(uri)).Distinct().ToList())
        {
            _used.Remove(uri);
            _models.Remove(uri);
            _told.Remove(uri);
            _made.Remove(uri);
            _changed.Remove(uri);
            session.Push(new JsonObject { ["type"] = "deleted", ["uri"] = uri.ToString() });
        }
    }

    private IDisposable Apply()
    {
        var previous = Applying;
        Applying = session;
        return Disposable.Create(() => Applying = previous);
    }

    private void Changed(IAsset asset)
    {
        if (!_made.Contains(asset.URI))
        {
            _changed.Add(asset.URI);
        }
    }

    // The texture made of Blender's picture is still what it was made of, by the hashes of both
    private static void Remember(Texture texture, byte[] png)
    {
        texture.Parameters[BlenderPngParameter] = Sha256(png);
        texture.Parameters[BlenderPictureParameter] = Sha256(DataOf<TextureData>(texture).GetPngBytes());
    }

    /// <summary>
    /// The SHA-256 of the texture's picture Blender knows it by: of Blender's PNG while the texture is what it was made of, else of the
    /// texture's own
    /// </summary>
    internal static string PictureHash(Texture texture)
    {
        var own = Sha256(DataOf<TextureData>(texture).GetPngBytes());
        return Parameter(texture, BlenderPictureParameter) == own && Parameter(texture, BlenderPngParameter) is { } blenders ? blenders : own;
    }

    // The assets' own GetData hides the interface's
    private static T DataOf<T>(IAsset asset) where T : AbstractAssetData => asset.GetData<T>();

    private static string? Parameter(IAsset asset, string parameter)
    {
        return asset.Parameters.TryGetValue(parameter, out var value) ? value?.ToString() : null;
    }

    // Of any open document: the model's own, its chunk's scene, one changing it through a link (a game object's model slot)
    private static bool HasUnsavedChanges(IAsset asset)
    {
        var uris = new HashSet<LabURI> { asset.URI };
        return LinkedDocuments.Documents().Any(document => document.IsDirty && document.HasChangesIn(uris));
    }

    // The settings with each picture the shaders draw (their "Image", an index into the images Blender made the project's) as its texture
    private static JsonObject WithPictures(JsonObject settings, JsonArray? images, LabURI package, string material)
    {
        var assetManager = AssetManager.Get();
        var copy = (JsonObject)settings.DeepClone();
        foreach (var shader in (copy["Shaders"] as JsonArray ?? []).OfType<JsonObject>())
        {
            string? linked;
            if (shader["Image"] is JsonValue picture && picture.TryGetValue<Int32>(out var index))
            {
                shader.Remove("Image");
                linked = images != null && index >= 0 && index < images.Count && images[index] is JsonObject image ? image.GetString("uri") : null;
            }
            else
            {
                linked = (shader["TextureId"] as JsonObject)?.GetString("_uri");
            }

            if (string.IsNullOrEmpty(linked) || new LabURI(linked) == LabURI.Empty)
            {
                shader["TextureId"] = new JsonObject { ["_uri"] = LabURI.Empty.ToString() };
                continue;
            }

            if (!assetManager.DoesAssetExist(new LabURI(linked)))
            {
                Log.WriteLine($"{material} from Blender draws {linked}, which the project doesn't have: it draws the placeholder texture", Log.LogType.Warning);
                linked = PlaceholderAssets.GetOrCreate(typeof(Texture), package).ToString();
            }

            shader["TextureId"] = new JsonObject { ["_uri"] = linked };
        }

        return copy;
    }

    private static JsonObject MaterialJson(Material material)
    {
        return new JsonObject
        {
            ["uri"] = material.URI.ToString(),
            ["name"] = material.Alias,
            ["data"] = JsonNode.Parse(JsonConvert.SerializeObject(DataOf<MaterialData>(material))),
            ["blender_id"] = Parameter(material, TlmMaterials.BlenderMaterialParameter)
        };
    }

    // The materials its file draws with, the ones its document has as well while TT Lab changed it
    private static List<Material> MaterialsOf(IAsset model)
    {
        var assetManager = AssetManager.Get();
        var uris = File.Exists(model.FullDataPath) ? AssetLinks.UrisInModelFile(model.FullDataPath).Select(uri => new LabURI(uri)).ToList() : [];
        if (model.IsLoaded && model.GetData<AbstractAssetData>() is OGIData ogi)
        {
            uris.AddRange(ogi.MaterialSlots.Select(slot => slot.Material));
        }

        return uris.Distinct().Where(uri => uri != LabURI.Empty && assetManager.DoesAssetExist(uri)).Select(assetManager.GetAsset).OfType<Material>().ToList();
    }

    private static IEnumerable<Texture> TexturesOf(Material material)
    {
        var assetManager = AssetManager.Get();
        return DataOf<MaterialData>(material).Shaders.Select(shader => shader.TextureId).Where(uri => uri != LabURI.Empty && assetManager.DoesAssetExist(uri))
            .Select(assetManager.GetAsset).OfType<Texture>();
    }

    /// <summary>
    /// The model of the data file, none when the project has none
    /// </summary>
    internal static IAsset? ModelAt(string path)
    {
        var wanted = AssetFileStamps.Normalize(path);
        return AssetManager.Get().GetAssets().FirstOrDefault(asset => !asset.IsInternal && asset is not Folder and not Package and not DiscFile && AssetLinks.IsModelFile(asset.Data)
                                                                       && AssetFileStamps.PathComparer.Equals(AssetFileStamps.Normalize(asset.FullDataPath), wanted));
    }

    // Read the way an editor reads it, nothing it brings taken: what keeps the game from taking the model, and what the objects drawing it
    // need of it
    private static List<ModelUsers.Problem> Check(IAsset model, byte[] file)
    {
        var type = DataTypeOf(model);
        using var scope = new AssetDataScope { IsDryRead = true };
        scope.ReadAgain(model);
        AbstractAssetData data;
        try
        {
            data = AssetDataBytes.Read(model, type, file);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return [new ModelUsers.Problem($"TT Lab can't read it: {exception.Message}", true, [])];
        }

        try
        {
            return data is OGIData ogi ? ModelUsers.Check(model.URI, ogi) : [];
        }
        finally
        {
            data.Dispose();
        }
    }

    // The data class of the asset's type is named after it (OGI's OGIData)
    // The asset's data class, found by its name without loading the data
    internal static Type DataTypeOf(IAsset asset)
    {
        if (asset.IsLoaded)
        {
            return asset.GetData<AbstractAssetData>().GetType();
        }

        return DataTypes.GetOrAdd(asset.GetType(), type => typeof(AbstractAssetData).Assembly.GetTypes()
                   .FirstOrDefault(data => data.Name == $"{type.Name}Data" && !data.IsAbstract && data.IsAssignableTo(typeof(AbstractAssetData))))
               ?? asset.GetData<AbstractAssetData>().GetType();
    }

    private static JsonArray ProblemsJson(IEnumerable<ModelUsers.Problem> problems)
    {
        return new JsonArray(problems.Select(problem => (JsonNode)new JsonObject
        {
            ["text"] = problem.Text,
            ["refused"] = problem.Refused,
            ["objects"] = new JsonArray(problem.Objects.Select(name => (JsonNode)JsonValue.Create(name)!).ToArray())
        }).ToArray());
    }

    internal static string FileRevision(string path) => File.Exists(path) ? Sha256(File.ReadAllBytes(path)) : string.Empty;

    internal static string Sha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static T AssetOf<T>(LinkProtocol.Frame frame, string key = "uri") where T : class, IAsset
    {
        var uri = new LabURI(Text(frame, key));
        var assetManager = AssetManager.Get();
        if (!assetManager.DoesAssetExist(uri) || assetManager.GetAsset(uri) is not T asset)
        {
            throw new InvalidOperationException($"The project has no {typeof(T).Name.ToLowerInvariant()} {uri}");
        }

        return asset;
    }

    private static string Name(LinkProtocol.Frame frame) => Text(frame, "name");

    private static string ReplacedBy(string? name, string? blend)
    {
        var model = string.IsNullOrEmpty(name) ? "a model" : name;
        return string.IsNullOrEmpty(blend) ? model : $"{model} of {Path.GetFileName(blend)}";
    }

    private static IEnumerable<LabURI> Uris(LinkProtocol.Frame frame, string key)
    {
        return (frame.Header[key] as JsonArray ?? []).Select(entry => entry is JsonValue value && value.TryGetValue<string>(out var text) ? text : null)
            .Where(text => !string.IsNullOrEmpty(text)).Select(text => new LabURI(text!)).Where(uri => uri != LabURI.Empty).Distinct();
    }

    private static T? Existing<T>(LabURI uri) where T : class, IAsset
    {
        var assetManager = AssetManager.Get();
        return assetManager.DoesAssetExist(uri) ? assetManager.GetAsset(uri) as T : null;
    }

    private static string Text(LinkProtocol.Frame frame, string key)
    {
        return OptionalText(frame, key) is { Length: > 0 } text ? text : throw new InvalidOperationException($"{frame.Type} without its {key}");
    }

    private static string? OptionalText(LinkProtocol.Frame frame, string key)
    {
        return frame.Header[key] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
    }

    private static bool Flag(LinkProtocol.Frame frame, string key) => frame.Header[key] is JsonValue value && value.TryGetValue<bool>(out var flag) && flag;

    private static JsonObject Object(LinkProtocol.Frame frame, string key)
    {
        return frame.Header[key] as JsonObject ?? throw new InvalidOperationException($"{frame.Type} without its {key}");
    }

    private static JsonObject Answer(string type) => new() { ["type"] = type };
}
