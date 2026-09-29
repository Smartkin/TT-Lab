using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json.Nodes;

namespace TT_Lab.AssetData.Graphics.TlModel;

/// <summary>
/// Nodes of TT Lab model files and their transforms
/// </summary>
public static class TlmNodes
{
    public const string KindKey = "kind";
    public const string NameKey = "name";
    public const string DataKey = "data";
    public const string ChildrenKey = "children";
    public const string JointKey = "joint";
    public const string MeshKey = "mesh";
    /// <summary>
    /// The game's matrix of a node placed by a transform, kept while the transform still is the one written for it
    /// </summary>
    public const string MatrixKey = "matrix";

    public static JsonObject Create(string kind, string name, JsonObject? data = null)
    {
        var node = new JsonObject { [KindKey] = kind, [NameKey] = name };
        if (data != null)
        {
            node[DataKey] = data;
        }

        return node;
    }

    public static JsonObject AddChild(this JsonObject node, JsonObject child)
    {
        if (node[ChildrenKey] is not JsonArray children)
        {
            children = new JsonArray();
            node[ChildrenKey] = children;
        }

        children.Add(child);
        return child;
    }

    public static string? GetKind(this JsonObject node)
    {
        return node.GetString(KindKey);
    }

    public static JsonObject GetData(this JsonObject node)
    {
        return node[DataKey] as JsonObject ?? new JsonObject();
    }

    public static IEnumerable<JsonObject> GetChildren(this JsonObject? node)
    {
        return node?[ChildrenKey] is JsonArray children ? children.OfType<JsonObject>() : [];
    }

    public static IEnumerable<JsonObject> GetChildren(this JsonObject? node, string kind)
    {
        return node.GetChildren().Where(child => child.GetKind() == kind);
    }

    public static JsonObject? FindChild(this JsonObject? node, string kind)
    {
        return node.GetChildren(kind).FirstOrDefault();
    }

    /// <summary>
    /// The node and every node under it, parents before their children
    /// </summary>
    public static IEnumerable<JsonObject> Traverse(this JsonObject? node)
    {
        if (node == null)
        {
            yield break;
        }

        yield return node;
        foreach (var descendant in node.GetChildren().SelectMany(Traverse))
        {
            yield return descendant;
        }
    }

    /// <summary>
    /// Sets the node's transform relative to its parent as translation, rotation and scale. Blender can't hold the slightly sheared
    /// matrices the game has, they come as close as they can
    /// </summary>
    public static void SetTransform(this JsonObject node, Matrix4x4 matrix)
    {
        if (!TryDecompose(matrix, out var scale, out var rotation, out var translation))
        {
            translation = matrix.Translation;
            rotation = Quaternion.Identity;
            scale = new Vector3(new Vector3(matrix.M11, matrix.M12, matrix.M13).Length(), new Vector3(matrix.M21, matrix.M22, matrix.M23).Length(),
                new Vector3(matrix.M31, matrix.M32, matrix.M33).Length());
        }

        if (translation != Vector3.Zero)
        {
            node["translation"] = TlmJson.ToJson(new[] { translation.X, translation.Y, translation.Z });
        }

        if (rotation != Quaternion.Identity)
        {
            node["rotation"] = TlmJson.ToJson(new[] { rotation.X, rotation.Y, rotation.Z, rotation.W });
        }

        if (scale != Vector3.One)
        {
            node["scale"] = TlmJson.ToJson(new[] { scale.X, scale.Y, scale.Z });
        }
    }

    /// <summary>
    /// The node's transform relative to its parent, identity when it has none
    /// </summary>
    public static Matrix4x4 GetTransform(this JsonObject node)
    {
        var translation = node.GetFloats("translation");
        var rotation = node.GetFloats("rotation");
        var scale = node.GetFloats("scale");
        return Matrix4x4.CreateScale(scale.Length == 3 ? new Vector3(scale[0], scale[1], scale[2]) : Vector3.One) *
               Matrix4x4.CreateFromQuaternion(rotation.Length == 4 ? Quaternion.Normalize(new Quaternion(rotation[0], rotation[1], rotation[2], rotation[3])) : Quaternion.Identity) *
               Matrix4x4.CreateTranslation(translation.Length == 3 ? new Vector3(translation[0], translation[1], translation[2]) : Vector3.Zero);
    }

    /// <summary>
    /// The stored matrix, with the game's exact values, while the node's transform is still the one <see cref="SetTransform"/> wrote
    /// for it. The node's matrix otherwise
    /// </summary>
    public static Matrix4x4 KeepStored(Single[] stored, Matrix4x4 matrix, out Boolean kept)
    {
        kept = false;
        if (stored.Length != 16)
        {
            return matrix;
        }

        var storedMatrix = ToMatrix(stored);
        var written = TryDecompose(storedMatrix, out var scale, out var rotation, out var translation)
            ? Matrix4x4.CreateScale(scale) * Matrix4x4.CreateFromQuaternion(rotation) * Matrix4x4.CreateTranslation(translation)
            : storedMatrix;
        kept = IsClose(written, matrix);
        return kept ? storedMatrix : matrix;
    }

    /// <summary>
    /// The matrix's 16 values the way the game and System.Numerics keep them, row after row with the translation in the last one
    /// </summary>
    public static Single[] ToArray(Matrix4x4 matrix)
    {
        return [matrix.M11, matrix.M12, matrix.M13, matrix.M14, matrix.M21, matrix.M22, matrix.M23, matrix.M24,
            matrix.M31, matrix.M32, matrix.M33, matrix.M34, matrix.M41, matrix.M42, matrix.M43, matrix.M44];
    }

    public static Matrix4x4 ToMatrix(Single[] values)
    {
        return new Matrix4x4(values[0], values[1], values[2], values[3], values[4], values[5], values[6], values[7],
            values[8], values[9], values[10], values[11], values[12], values[13], values[14], values[15]);
    }

    /// <summary>
    /// The matrix the way Blender keeps them, transforming column vectors and written row after row, which puts the translation
    /// in the last column
    /// </summary>
    public static JsonArray ToColumnVectorJson(Matrix4x4 matrix)
    {
        return TlmJson.ToJson(ToArray(Matrix4x4.Transpose(matrix)));
    }

    public static Matrix4x4? FromColumnVectorJson(Single[] values)
    {
        return values.Length == 16 ? Matrix4x4.Transpose(ToMatrix(values)) : null;
    }

    public static Boolean TryDecompose(Matrix4x4 matrix, out Vector3 scale, out Quaternion rotation, out Vector3 translation)
    {
        if (!Matrix4x4.Decompose(matrix, out scale, out rotation, out translation) || scale.X == 0 || scale.Y == 0 || scale.Z == 0)
        {
            return false;
        }

        rotation = Quaternion.Normalize(rotation);
        return true;
    }

    // Blender moves what it reads by rounding errors
    private static Boolean IsClose(Matrix4x4 a, Matrix4x4 b)
    {
        return ToArray(a).Zip(ToArray(b)).All(pair => Math.Abs(pair.First - pair.Second) < 1e-3f);
    }

    public static Boolean IsIdentity(Matrix4x4 matrix, Single tolerance = 1e-6f)
    {
        var identity = Matrix4x4.Identity;
        for (var row = 0; row < 4; row++)
        {
            for (var column = 0; column < 4; column++)
            {
                if (Math.Abs(matrix[row, column] - identity[row, column]) > tolerance)
                {
                    return false;
                }
            }
        }

        return true;
    }
}
