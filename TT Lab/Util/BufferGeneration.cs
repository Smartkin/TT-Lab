using GlmSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using Caliburn.Micro;
using TT_Lab.AssetData.Graphics.SubModels;
using TT_Lab.Assets;
using TT_Lab.Rendering.Services;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.Util;

public static class BufferGeneration
{
    private static MeshService? _meshService;

    private static void GetMeshService()
    {
        if (_meshService != null)
        {
            return;
        }

        _meshService = IoC.Get<MeshService>();
    }
    
    public static MeshInfo GetPlaneBuffer()
    {
        GetMeshService();
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

        return _meshService!.GetMesh(LabURI.Plane);
    }

    public static MeshInfo GetCubeBuffer()
    {
        GetMeshService();
        //
        // color ??= Color.LightGray;
        // List<Color> colors = new List<Color> { color.Value };
        // float[] cubeVertecies = {
        //     -1.0f,-1.0f,-1.0f,
        //     -1.0f,-1.0f, 1.0f,
        //     -1.0f, 1.0f, 1.0f,
        //     1.0f, 1.0f,-1.0f,
        //     -1.0f,-1.0f,-1.0f,
        //     -1.0f, 1.0f,-1.0f,
        //     1.0f,-1.0f, 1.0f,
        //     -1.0f,-1.0f,-1.0f,
        //     1.0f,-1.0f,-1.0f,
        //     1.0f, 1.0f,-1.0f,
        //     1.0f,-1.0f,-1.0f,
        //     -1.0f,-1.0f,-1.0f,
        //     -1.0f,-1.0f,-1.0f,
        //     -1.0f, 1.0f, 1.0f,
        //     -1.0f, 1.0f,-1.0f,
        //     1.0f,-1.0f, 1.0f,
        //     -1.0f,-1.0f, 1.0f,
        //     -1.0f,-1.0f,-1.0f,
        //     -1.0f, 1.0f, 1.0f,
        //     -1.0f,-1.0f, 1.0f,
        //     1.0f,-1.0f, 1.0f,
        //     1.0f, 1.0f, 1.0f,
        //     1.0f,-1.0f,-1.0f,
        //     1.0f, 1.0f,-1.0f,
        //     1.0f,-1.0f,-1.0f,
        //     1.0f, 1.0f, 1.0f,
        //     1.0f,-1.0f, 1.0f,
        //     1.0f, 1.0f, 1.0f,
        //     1.0f, 1.0f,-1.0f,
        //     -1.0f, 1.0f,-1.0f,
        //     1.0f, 1.0f, 1.0f,
        //     -1.0f, 1.0f,-1.0f,
        //     -1.0f, 1.0f, 1.0f,
        //     1.0f, 1.0f, 1.0f,
        //     -1.0f, 1.0f, 1.0f,
        //     1.0f,-1.0f, 1.0f
        // };
        //     
        // var vectors = new List<vec3>();
        // var faces = new List<IndexedFace>();
        // for (var i = 0; i < cubeVertecies.Length; i += 3)
        // {
        //     vectors.Add(new vec3(cubeVertecies[i], cubeVertecies[i + 1], cubeVertecies[i + 2]));
        // }
        // for (var i = 0; i < vectors.Count; i += 3)
        // {
        //     faces.Add(new IndexedFace { Indexes = new int[] { i + 2, i + 1, i } });
        // }
        
        return _meshService!.GetMesh(LabURI.Box);
    }

    public static MeshInfo GetCircleBuffer(float segmentPart = 1.0f, float thickness = 0.1f, int resolution = 16)
    {
        GetMeshService();
        //
        // var segment = 2 * System.Math.PI * segmentPart;
        // List<vec3> vectors = new List<vec3>();
        // var step = (2 * System.Math.PI) / resolution;
        // var k = 1.0f - thickness;
        // for (var i = 0; i <= resolution; ++i)
        // {
        //     var step1 = i * step;
        //     if (step1 > segment)
        //     {
        //         break;
        //     }
        //     var step2 = System.Math.Min((i + 1) * step, segment);
        //     vectors.Add(new vec3((float)System.Math.Cos(step1), 0, (float)System.Math.Sin(step1)));
        //     vectors.Add(new vec3((float)System.Math.Cos(step1) * k, 0, (float)System.Math.Sin(step1) * k));
        //     vectors.Add(new vec3((float)System.Math.Cos(step2) * k, 0, (float)System.Math.Sin(step2)));
        //     vectors.Add(new vec3((float)System.Math.Cos(step1), 0, (float)System.Math.Sin(step1)));
        //     vectors.Add(new vec3((float)System.Math.Cos(step2) * k, 0, (float)System.Math.Sin(step2) * k));
        //     vectors.Add(new vec3((float)System.Math.Cos(step2), 0, (float)System.Math.Sin(step2)));
        // }
        // var faces = new List<IndexedFace>();
        // for (var i = 0; i < vectors.Count; i += 3)
        // {
        //     faces.Add(new IndexedFace { Indexes = new int[] { i + 2, i + 1, i } });
        // }
            
        return _meshService!.GetMesh(LabURI.Circle);
    }
}