using System.Collections.Generic;
using TT_Lab.Assets;
using Newtonsoft.Json;

namespace TT_Lab.Project.Build;

/// <summary>
/// What a build makes: the version of the game and the chunks left out of it. A chunk left out isn't written again, the file of the
/// last build it was in stays, and the links of the built chunks to it are dropped from their files without touching the assets
/// </summary>
public sealed class BuildProfile
{
    public const string DefaultName = "Default";

    public string Name { get; set; } = DefaultName;

    public Project.GamePlatform Platform { get; set; } = Project.GamePlatform.PS2;

    // Chunks are left out rather than listed, so chunks made after the profile get built
    public List<string> ExcludedChunks { get; set; } = [];

    [JsonIgnore]
    public string? FilePath { get; set; }

    public HashSet<LabURI> ExcludedChunkUris()
    {
        var uris = new HashSet<LabURI>();
        foreach (var chunk in ExcludedChunks)
        {
            uris.Add(new LabURI(chunk));
        }

        return uris;
    }

    public BuildProfile Copy(string name)
    {
        return new BuildProfile { Name = name, Platform = Platform, ExcludedChunks = [..ExcludedChunks] };
    }
}
