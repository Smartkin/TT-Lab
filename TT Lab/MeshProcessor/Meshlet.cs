using System;
using System.Collections.Generic;
using System.Linq;
using GlmSharp;
using TT_Lab.AssetData.Graphics.SubModels;
using TT_Lab.Extensions;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.MeshProcessor;

public class Meshlet
{
    public List<UInt32> Indices { get; set; } = new();
    public List<Vertex> Vertexes { get; set; } = new();
    public List<SubBlendFaceData>? BlendFaces { get; set; }
    public List<UInt32> Strip { get; set; } = new();
    public List<Vector4> Normals { get; private set; } = new();

    public void CalculateNormals()
    {
        Normals.AddRange(Vertexes.Select(_ => new Vector4()));
        var winding = 0U;
        for (var i = 0U; i < Strip.Count - 2; ++i)
        {
            UInt32 i1, i2, i3;
            if (i % 2 == winding)
            {
                i1 = i;
                i2 = i + 1;
                i3 = i + 2;
            }
            else
            {
                i1 = i + 1;
                i2 = i;
                i3 = i + 2;
            }
            
            var idx1 = (int)Strip[(int)i1];
            var idx2 = (int)Strip[(int)i2];
            var idx3 = (int)Strip[(int)i3];

            if (idx3 == 0xFFFF)
            {
                i += 2;
                winding = i % 2;
                continue;
            }

            if (idx1 == idx2 || idx1 == idx3 || idx2 == idx3)
            {
                continue;
            }

            var v0 = Vertexes[idx1].Position.ToGlm();
            var v1 = Vertexes[idx2].Position.ToGlm();
            var v2 = Vertexes[idx3].Position.ToGlm();
            var calculatedNormal = new vec4(glm.Cross((v1 - v0).xyz, (v2 - v0).xyz));
            
            Normals[idx1].Add(calculatedNormal.ToTwin());
            Normals[idx2].Add(calculatedNormal.ToTwin());
            Normals[idx3].Add(calculatedNormal.ToTwin());
        }

        foreach (var normal in Normals)
        {
            if (normal.Length() == 0)
            {
                normal.Y = 1.0f;
            }
            
            normal.Normalize();
        }
    }
}