using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Meshoptimizer;
using TT_Lab.AssetData.Graphics.SubModels;
using TT_Lab.Extensions;
using TT_Lab.MeshProcessor;
using Twinsanity.PS2Hardware;
using Twinsanity.TwinsanityInterchange.Common;

[assembly: DisableRuntimeMarshalling]

namespace TT_Lab.Libraries;

public static unsafe partial class MeshOptimizer
{
    public static Meshlet Simplify(List<UInt32> indices, List<Vertex> vertices, List<SubBlendFaceData>? blendFaceData = null)
    {
        var originalIndices = indices.ToArray();
        var destinationIndices = new UInt32[indices.Count];
        var positions = vertices.Select(v => v.Position.ToGlm()).ToList();
        var positionsArray = new float[positions.Count * 3];
        var index = 0;
        foreach (var position in positions)
        {
            positionsArray[index++] = position.x;
            positionsArray[index++] = position.y;
            positionsArray[index++] = position.z;
        }
    
        nuint totalIndexCount;
        var resultError = 0.0f;
        fixed (UInt32* destinationNative = destinationIndices)
        {
            fixed (UInt32* originalIndNative = originalIndices)
            {
                fixed (float* positionsNative = positionsArray)
                {
                    var resultErrorNative = &resultError;
                    totalIndexCount = Native.Simplify(ref *destinationNative, 
                        in *originalIndNative,
                        (nuint)indices.Count,
                        in *positionsNative,
                        (nuint)vertices.Count, 
                        12, 
                        3,
                        0.9f,
                        0, 
                        ref *resultErrorNative);
                }
            }
        }
    
        destinationIndices = totalIndexCount != UIntPtr.Zero ? destinationIndices[..(int)totalIndexCount] : originalIndices;
    
        var resultingMeshlet = new Meshlet();
        if (blendFaceData != null)
        {
            resultingMeshlet.BlendFaces = [];
            foreach (var blendFace in blendFaceData)
            {
                resultingMeshlet.BlendFaces.Add(blendFace);
            }
        }
    
        resultingMeshlet.Indices = new List<UInt32>(destinationIndices.Length);
        resultingMeshlet.Vertexes = new List<Vertex>(destinationIndices.Length);
        foreach (var idx in destinationIndices)
        {
            resultingMeshlet.Indices.Add(idx);
        }
        resultingMeshlet.Vertexes.AddRange(vertices);
        
        Debug.Assert(resultingMeshlet.Indices.Count > 0);
        Debug.Assert(resultingMeshlet.Indices.Count % 3 == 0);
        Debug.Assert(resultingMeshlet.Indices.All(idx => idx < resultingMeshlet.Vertexes.Count));
    
        return resultingMeshlet;
    }
    
    public static List<UInt32> Stripify(List<UInt32> indices, UInt32 vertexAmount, bool useOptimalStrips = true)
    {
        Debug.Assert(indices.All(i => i < vertexAmount));
        
        var maxNeededLength = Meshopt.StripifyBound((nuint)indices.Count);
        var destination = new uint[maxNeededLength];
        var indicesArray = new uint[indices.Count];
        var indIdx = 0;
        foreach (var idx in indices)
        {
            indicesArray[indIdx++] = idx;
        }
        
        var stripifiedIndices = 0U;
#if _WINDOWS
        fixed (uint* arrayStart = indicesArray)
        {
            fixed (uint* destinationStart = destination)
            {
                nuint indicesCountNative = (nuint)indicesArray.Length;
                Debug.Assert(arrayStart != destinationStart);
                stripifiedIndices = Meshopt
                    .Stripify(ref *destinationStart, in *arrayStart, indicesCountNative, vertexAmount, 0xFFFF)
                    .ToUInt32();
            }
        }
#else
        fixed (uint* arrayStart = indicesArray)
        {
            fixed (uint* destinationStart = destination)
            {
                Debug.Assert(arrayStart != destinationStart);
                var config = new RmTristripper.Config
                {
                    UseTunneling = false,
                    PreserveOrientation = false,
                    PreprocAlgorithm = RmTristripper.PreprocAlgorithms.RM_TRISTRIPPER_PREPROC_ALGORITHM_STRIPIFY
                };
                var indicesCountNative = (nuint)indicesArray.Length;
                stripifiedIndices = RmTristripper
                    .CreateStrips(ref *destinationStart, in *arrayStart, indicesCountNative, ref config).ToUInt32();
            }
        }
#endif
        
        // Create a full list
        var result = new List<UInt32>();
        for (var i = 0; i < stripifiedIndices; ++i)
        {
            result.Add(destination[i]);
        }
        
        // Reorder strips by their length
        var strips = new List<List<UInt32>>();
        strips.Add([0xFFFF]);
        foreach (var idx in result)
        {
            if (idx == 0xFFFF)
            {
                strips.Add([0xFFFF]);
                continue;
            }
            
            strips[^1].Add(idx);
        }
        strips.Sort((l1, l2) => l2.Count - l1.Count);
        
        // Stitch them back together
        result = strips.SelectMany(l => l).ToList();
        result.RemoveAt(0);
        
        return result;
    }
    
    // Additional imports that may not be provided in Meshoptimizer.NET https://github.com/BoyBaykiller/Meshoptimizer.NET
    private static partial class Native
    {
        private const string LIBRARY_NAME = "meshoptimizer";

        [LibraryImport(LIBRARY_NAME, EntryPoint = "meshopt_simplify")]
        public static partial nuint Simplify(ref uint destination, in uint indices, nuint indexCount,
            in float vertexPositions, nuint vertexCount, nuint vertexPositionsStride, nuint targetIndexCount,
            float targetError, uint options, ref float resultError);
    }

    private static partial class RmTristripper
    {
        private const string LIBRARY_NAME = "RmTristripper";

        public enum PreprocAlgorithms
        {
            //Treat each triangle as an isolated strip:
            RM_TRISTRIPPER_PREPROC_ALGORITHM_ISOLATED,

            //Try to pair up to two triangles together.
            //Prefer isolated triangles.
            RM_TRISTRIPPER_PREPROC_ALGORITHM_PAIRS,

            //Use the stripify algorithm (identical to the "non-tunneled" version).
            RM_TRISTRIPPER_PREPROC_ALGORITHM_STRIPIFY
        }
        
        [StructLayout(LayoutKind.Sequential)]
        public struct Config
        {
            public bool UseTunneling;
            public bool PreserveOrientation;
            public PreprocAlgorithms PreprocAlgorithm;
            public uint MaxCount;
            public bool Incremental;
            public uint LoopLimit;
            public bool BacktrackAfterLoopLimit;
            public uint DestCount;
        }

        [LibraryImport(LIBRARY_NAME, EntryPoint = "rm_tristripper_create_stiched_strips")]
        public static partial nuint CreateStrips(ref uint destination, in uint indices, nuint indexCount, ref Config config);
    }
}