using System;
using System.Collections.Generic;
using Twinsanity.TwinsanityInterchange.Common;

namespace Twinsanity.TwinsanityInterchange.Interfaces
{
    public interface ITwinPSF : ITwinItem
    {
        /// <summary>
        /// Font pages
        /// </summary>
        public List<ITwinPTC> FontPages { get; set; }
        /// <summary>
        /// ASCII table ordered data for where characters are on the texture
        /// </summary>
        public List<VectorCharacterData> CharacterData { get; set; }
        /// <summary>
        /// Index of space character in ASCII table (generally should be 32 or 0x20)
        /// </summary>
        public Int32 SpaceIdentifier { get; set; }
    }
}
