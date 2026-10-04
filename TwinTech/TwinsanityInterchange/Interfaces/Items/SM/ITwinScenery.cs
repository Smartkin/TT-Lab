using System;
using System.Collections.Generic;
using Twinsanity.TwinsanityInterchange.Common.Lights;
using Twinsanity.TwinsanityInterchange.Common.ScenerySubtypes;

namespace Twinsanity.TwinsanityInterchange.Interfaces.Items.SM
{
    public interface ITwinScenery : ITwinItem
    {
        /// <summary>
        /// Whether the scenery has lighting enabled
        /// </summary>
        Boolean HasLighting { get; set; }
        /// <summary>
        /// Name of the scenery
        /// </summary>
        String Name { get; set; }
        /// <summary>
        /// The fog the game tints the distance with while the player is in the chunk, one of the executable's 8 tables of colors
        /// (0x2f6ff8 in the PAL one), picked by the top byte of each pixel's depth: 0 violet, 1 black, 2 light blue, 3 green, 4 white,
        /// 5 light orange, 6 none, 7 blue to red. The game's chunks use 0 to 5
        /// </summary>
        UInt32 FogColor { get; set; }
        /// <summary>
        /// Unknown byte parameter
        /// </summary>
        /// <summary>
        /// Read into the chunk's data and never read again
        /// </summary>
        Byte UnusedByte { get; set; }
        /// <summary>
        /// Skydome's ID to render
        /// </summary>
        UInt32 SkydomeID { get; set; }
        /// <summary>
        /// Ambient lights present in the scenery
        /// </summary>
        List<AmbientLight> AmbientLights { get; set; }
        /// <summary>
        /// Directional lights present in the scenery
        /// </summary>
        List<DirectionalLight> DirectionalLights { get; set; }
        /// <summary>
        /// Point lights present in the scenery
        /// </summary>
        List<PointLight> PointLights { get; set; }
        /// <summary>
        /// Negative lights present in the scenery
        /// </summary>
        List<SpotLight> SpotLights { get; set; }
        /// <summary>
        /// Scenery tree
        /// </summary>
        List<TwinSceneryBaseType> Sceneries { get; set; }
        /// <summary>
        /// Index and kind (ambient 0, directional 1, point 2, spot 3) of every light in the order the scenery lists them, empty when
        /// they're listed kind by kind the way the PS2 version always does
        /// </summary>
        List<Int32> LightOrder { get; set; }

        enum SceneryType
        {
            None = 0x3,
            Node = 0x1600,
            Leaf = 0x1605,
            Root = 0x160A,
        }

        private static readonly Byte[] reservedBlob;
        static ITwinScenery()
        {
            reservedBlob = new Byte[0x400];
            for (Int32 i = 0; i < reservedBlob.Length; i++)
            {
                reservedBlob[i] = 0xCD;
            }
        }

        static Byte[] GetReservedBlob()
        {
            return reservedBlob;
        }
    }
}
