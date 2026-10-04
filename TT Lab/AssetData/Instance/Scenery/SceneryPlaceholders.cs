using System;
using System.Linq;
using System.Text.Json.Nodes;
using Splat;
using TT_Lab.AssetData.Graphics;
using TT_Lab.AssetData.Graphics.TlModel;
using TT_Lab.Assets;
using TT_Lab.Assets.Graphics;
using TT_Lab.Project;

namespace TT_Lab.AssetData.Instance.Scenery;

/// <summary>
/// The material placeholder shapes are drawn with: a checker of two greys a unit a square like a new chunk's ground, one of each version
/// of the game, made the first time a placeholder is
/// </summary>
public static class SceneryPlaceholders
{
    /// <summary>
    /// The checker material's ID among materials made out of a model file's (<see cref="TlmMaterials.BlenderMaterialParameter"/>), with its
    /// version of the game after it
    /// </summary>
    public const String CheckerMaterialId = "TTLab_PlaceholderChecker";

    public const String CheckerMaterialName = "Placeholder checker";

    // Two squares across the texture, which repeats every two units
    private const Int32 CheckerTextureSize = 64;
    private const Single UvScale = 0.5f;
    private const UInt32 LightSquare = 0xFFA8A8A8;
    private const UInt32 DarkSquare = 0xFF8C8C8C;

    /// <summary>
    /// The material of the file the placeholder's part is drawn with: the package's checker material, or one made with it while there's none
    /// </summary>
    public static Int32 UseCheckerMaterial(TlmFile file, LabURI package)
    {
        var id = CheckerMaterialIdOf(package);
        var existing = AssetManager.Get().GetRelatedAssetsOf<Material>(package)
            .FirstOrDefault(material => !material.IsInternal && material.Parameters.TryGetValue(TlmMaterials.BlenderMaterialParameter, out var value) && value?.ToString() == id);
        if (existing != null)
        {
            file.Materials.Add(new JsonObject { ["uri"] = existing.URI.ToString(), ["name"] = existing.Alias });
            return file.Materials.Count - 1;
        }

        file.Materials.Add(new JsonObject
        {
            ["name"] = CheckerMaterialName,
            ["blender_id"] = id,
            ["image"] = new JsonObject { ["png"] = file.Write(TextureData.EncodePng(CheckerPixels(), CheckerTextureSize, CheckerTextureSize).AsSpan()), ["name"] = "PlaceholderChecker.png" }
        });
        return file.Materials.Count - 1;
    }

    internal static UInt32[] CheckerPixels()
    {
        const Int32 half = CheckerTextureSize / 2;
        var pixels = new UInt32[CheckerTextureSize * CheckerTextureSize];
        for (var y = 0; y < CheckerTextureSize; y++)
        {
            for (var x = 0; x < CheckerTextureSize; x++)
            {
                pixels[y * CheckerTextureSize + x] = (x < half) ^ (y < half) ? LightSquare : DarkSquare;
            }
        }

        return pixels;
    }

    private static String CheckerMaterialIdOf(LabURI package)
    {
        var project = Locator.Current.GetService<ProjectManager>()?.OpenedProject as Project.Project;
        return project == null ? CheckerMaterialId : $"{CheckerMaterialId}_{project.GetPlatform(package)}";
    }

    /// <summary>
    /// The shape as a part of a model file, a square of the checker the size given (a unit), its vertexes' colors leaving the texture as it is
    /// </summary>
    public static JsonObject WritePart(TlmFile file, PlaceholderMesh mesh, Int32 material, Single squareSize = 1.0f)
    {
        var uvScale = UvScale / squareSize;
        return new JsonObject
        {
            ["material"] = material,
            ["vertices"] = mesh.Positions.Count,
            ["faces"] = file.Write(mesh.Faces.SelectMany(face => new[] { (UInt32)face.A, (UInt32)face.B, (UInt32)face.C }).ToArray().AsSpan()),
            ["position"] = file.Write(mesh.Positions.SelectMany(position => new[] { position.X, position.Y, position.Z }).ToList()),
            ["normal"] = file.Write(mesh.Normals.SelectMany(normal => new[] { normal.X, normal.Y, normal.Z }).ToList()),
            ["uv"] = file.Write(mesh.Uvs.SelectMany(uv => new[] { uv.X * uvScale, uv.Y * uvScale }).ToList()),
            ["color"] = file.Write(Enumerable.Repeat((Byte)0x80, mesh.Positions.Count * 4).ToList())
        };
    }
}
