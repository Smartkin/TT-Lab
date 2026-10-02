using System;
using static Twinsanity.TwinsanityInterchange.Enumerations.Enums;

namespace Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout
{
    /// <summary>
    /// A path between two AI positions of a layout (the game's AiPath): the path finder's routes go along them
    /// </summary>
    public interface ITwinAIPath : ITwinItem
    {
        /// <summary>
        /// The AI positions the path joins, by their index in the layout
        /// </summary>
        UInt16 PositionA { get; set; }
        UInt16 PositionB { get; set; }
        AiPathFlags Flags { get; set; }
        /// <summary>
        /// The tools' chunk indexes of the two positions, which the game replaces with its own chunk's when it links the navigation
        /// </summary>
        UInt16 ChunkA { get; set; }
        UInt16 ChunkB { get; set; }
    }
}
