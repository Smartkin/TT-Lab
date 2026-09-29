using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TT_Lab.AssetData;
using TT_Lab.ViewModels.ResourceTree;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace TT_Lab.Assets;

public enum DiscFileTool
{
    // twinmusic reads the MH/MB music archives
    Music,
    // twinpss reads the PSS videos
    Video,
}

/// <summary>
/// A file of the disc the project tree shows for the twinstudio tools to open: a music archive (its MH header and MB body as one
/// node) or a PSS video. It's never written as an asset, the tree makes it from the file
/// </summary>
public class DiscFile : SerializableAsset
{
    private const string UriRoot = "res://__DISC__";

    public override UInt32 Section => uint.MaxValue;

    public DiscFileTool Tool { get; }

    /// <summary>
    /// The file the tool opens, a music archive's header
    /// </summary>
    public string MainPath { get; }

    /// <summary>
    /// Every file of the node, a music archive's header and body
    /// </summary>
    public IReadOnlyList<string> Paths { get; }

    public override string IconPath => Tool == DiscFileTool.Music ? "UI_Sound_Library.png" : "Camera.png";

    public DiscFile(string projectPath, DiscFileTool tool, string mainPath, IReadOnlyList<string> paths)
    {
        Tool = tool;
        MainPath = mainPath;
        Paths = paths;
        InvariantName = Path.GetFileNameWithoutExtension(mainPath);
        Alias = paths.Count > 1
            ? $"{Path.GetFileName(mainPath)}/{string.Join("/", paths.Skip(1).Select(path => Path.GetExtension(path).TrimStart('.')))}"
            : Path.GetFileName(mainPath);
        SkipExport = true;
        URI = UriFor(projectPath, mainPath);
    }

    public static LabURI UriFor(string projectPath, string mainPath)
    {
        var relative = Path.GetRelativePath(projectPath, mainPath).Replace('\\', '/');
        return new LabURI($"{UriRoot}/{Path.ChangeExtension(relative, null)}");
    }

    /// <summary>
    /// The files of a disc folder the tools open: a music archive's MH and MB together, a PSS on its own
    /// </summary>
    public static List<DiscFile> ListIn(string projectPath, DirectoryInfo directory)
    {
        var files = new List<DiscFile>();
        var bodies = directory.GetFiles("*.mb").ToDictionary(file => Path.GetFileNameWithoutExtension(file.Name), file => file.FullName, StringComparer.OrdinalIgnoreCase);
        foreach (var header in directory.GetFiles("*.mh"))
        {
            var name = Path.GetFileNameWithoutExtension(header.Name);
            var paths = bodies.Remove(name, out var body) ? new[] { header.FullName, body } : new[] { header.FullName };
            files.Add(new DiscFile(projectPath, DiscFileTool.Music, header.FullName, paths));
        }

        // A body without its header is still something the tool can be pointed at
        files.AddRange(bodies.Values.Select(body => new DiscFile(projectPath, DiscFileTool.Music, body, [body])));
        files.AddRange(directory.GetFiles("*.pss").Select(video => new DiscFile(projectPath, DiscFileTool.Video, video.FullName, [video.FullName])));
        return files;
    }

    public override void RegenerateUri()
    {
    }

    public override void Serialize(SerializationFlags serializationFlags = SerializationFlags.None)
    {
    }

    public override void Delete()
    {
        AssetManager.Get().RemoveAsset(this);
    }

    public override Type GetEditorType()
    {
        throw new NotSupportedException("Disc files are opened with the twinstudio tools");
    }

    public override AbstractAssetData GetData()
    {
        return new DummyData(this);
    }

    public override void ResolveChunkResources(Factory.ITwinItemFactory factory, ITwinSection section)
    {
    }

    protected override ResourceTreeElementViewModel CreateResourceTreeElement(ResourceTreeElementViewModel? parent = null)
    {
        return new DiscFileElementViewModel(URI, parent);
    }
}
