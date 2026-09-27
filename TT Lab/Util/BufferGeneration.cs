using GlmSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using Caliburn.Micro;
using TT_Lab.AssetData.Graphics.SubModels;
using TT_Lab.Assets;
using TT_Lab.Rendering;
using TT_Lab.Rendering.Services;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.Util;

public static class BufferGeneration
{
    
    public static MeshInfo GetPlaneBuffer(RenderContext renderContext)
    {
        //
        // float[] vertices = new float[18] {
        //     -100, -100, 0,  // pos
        //     100, -100, 0,
        //     -100,  100, 0,
        //     -100,  100, 0 ,
        //     100,  -100, 0 ,
        //     100,  100, 0 ,
        // };
        //     
        // var vectors = new List<vec3>();
        // var faces = new List<IndexedFace>();
        // var uvs = new List<vec2>()
        // {
        //     new vec2(0, 0),
        //     new vec2(1, 0),
        //     new vec2(0, 1),
        //     new vec2(0, 1),
        //     new vec2(1, 0),
        //     new vec2(1, 1),
        // };
        // for (var i = 0; i < vertices.Length; i += 3)
        // {
        //     vectors.Add(new vec3(vertices[i], vertices[i + 1], vertices[i + 2]));
        // }
        // for (var i = 0; i < vectors.Count; i += 3)
        // {
        //     faces.Add(new IndexedFace { Indexes = new int[] { i + 2, i + 1, i } });
        // }

        return renderContext.MeshService.GetMesh(LabURI.Plane);
    }

    public static MeshInfo GetCubeBuffer(RenderContext renderContext)
    {
        return renderContext.MeshService.GetMesh(LabURI.Box);
    }

    // A volume's inside and outside each blend this much of its color in, together about half like a single face would
    public const float VolumeOpacity = 0.3f;
    public const float SelectedVolumeOpacity = 0.15f;

    /// <summary>
    /// A box from -1 to 1 that's seen through, drawn after the scene
    /// </summary>
    public static MeshInfo GetVolumeBuffer(RenderContext renderContext)
    {
        return renderContext.MeshService.GetMesh(LabURI.Volume);
    }

    public static MeshInfo GetCircleBuffer(RenderContext renderContext, float segmentPart = 1.0f, float thickness = 0.1f, int resolution = 16)
    {
        return renderContext.MeshService.GetMesh(LabURI.Circle);
    }
}