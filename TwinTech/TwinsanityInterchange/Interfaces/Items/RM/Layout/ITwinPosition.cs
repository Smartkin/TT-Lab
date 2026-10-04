using Twinsanity.TwinsanityInterchange.Common;

namespace Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout
{
    public interface ITwinPosition : ITwinItem
    {
        /// <summary>
        /// Coordinates in the chunk, a key of the waypoints of the instances listing it (the game reads X, Y and Z, every reader
        /// takes 1 as W)
        /// </summary>
        Vector4 Position { get; set; }
    }
}
