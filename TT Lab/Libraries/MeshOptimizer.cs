using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Meshoptimizer;
using TT_Lab.AssetData.Graphics.SubModels;
using TT_Lab.Extensions;
using TT_Lab.MeshProcessor;
using Twinsanity.PS2Hardware;

namespace TT_Lab.Libraries;

public static unsafe partial class MeshOptimizer
{
    public static List<UInt32> Stripify(List<UInt32> indices, UInt32 vertexAmount)
    {
        var optimizedIndices = indices.ToArray();
        var destinationIndices = new UInt32[indices.Count];
        fixed (uint* destinationIndicesNative = destinationIndices)
        {
            fixed (uint* indicesNative = optimizedIndices)
            {
                Native.OptimizeVertexCacheStrip(ref *destinationIndicesNative, in *indicesNative, (nuint)optimizedIndices.Length, vertexAmount);
            }
        }
        
        var maxNeededLength = Meshopt.StripifyBound((nuint)destinationIndices.Length);
        var destination = new uint[maxNeededLength];
        var indicesArray = destinationIndices;

        var stripifiedIndices = 0U;
        fixed (uint* arrayStart = indicesArray)
        {
            fixed (uint* destinationStart = destination)
            {
                nuint indicesCountNative = (nuint)indicesArray.Length;
                stripifiedIndices = Meshopt.Stripify(ref *destinationStart, in *arrayStart, indicesCountNative, vertexAmount, 0).ToUInt32();
            }
        }

        var result = new List<UInt32>();
        for (var i = 0; i < stripifiedIndices; ++i)
        {
            result.Add(destination[i]);
        }

        return result;
    }
    
    // Additional imports that may not be provided in Meshoptimizer.NET https://github.com/BoyBaykiller/Meshoptimizer.NET
    private static partial class Native
    {
        private const string LIBRARY_NAME = "meshoptimizer";

        [LibraryImport(LIBRARY_NAME, EntryPoint = "meshopt_optimizeVertexCacheStrip")]
        public static partial void OptimizeVertexCacheStrip(ref uint destination, in uint indices, nuint indexCount,
            nuint vertexCount);
        
    }
}