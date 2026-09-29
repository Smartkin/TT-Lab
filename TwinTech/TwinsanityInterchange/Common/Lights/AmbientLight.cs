using System;

namespace Twinsanity.TwinsanityInterchange.Common.Lights
{
    /// <summary>
    /// Lights everything with its color times its intensity, wherever it is.
    /// </summary>
    public class AmbientLight : Light
    {
        public override LightType Type => LightType.Ambient;

        protected override Single BoundsExtent => Intensity * 100000.0f;
    }
}
