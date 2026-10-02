using System;
using System.Collections.Generic;
using System.IO;

namespace Twinsanity.TwinsanityInterchange.Common
{
    /// <summary>
    /// What a path keeps next to its points (the game's LayoutPath, a camera path's and a camera spline's): every segment's arc length
    /// from the start, then 1 over the steps the segment takes, one array after the other
    /// </summary>
    public static class TwinPathParameters
    {
        public static void Read(BinaryReader reader, Int32 segments, List<Single> arcLengths, List<Single> inverseSteps)
        {
            arcLengths.Clear();
            inverseSteps.Clear();
            for (var i = 0; i < segments; ++i)
            {
                arcLengths.Add(reader.ReadSingle());
            }

            for (var i = 0; i < segments; ++i)
            {
                inverseSteps.Add(reader.ReadSingle());
            }
        }

        public static void Write(BinaryWriter writer, List<Single> arcLengths, List<Single> inverseSteps)
        {
            if (arcLengths.Count != inverseSteps.Count)
            {
                throw new InvalidOperationException($"A path has {arcLengths.Count} arc lengths and {inverseSteps.Count} steps, the game reads as many of each");
            }

            foreach (var length in arcLengths)
            {
                writer.Write(length);
            }

            foreach (var steps in inverseSteps)
            {
                writer.Write(steps);
            }
        }
    }
}
