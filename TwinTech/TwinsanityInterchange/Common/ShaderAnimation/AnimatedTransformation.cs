using System;

namespace Twinsanity.TwinsanityInterchange.Common.ShaderAnimation
{
    public class AnimatedTransformation : Animation.AnimatedTransformation
    {
        public AnimatedTransformation() : base(0) { }

        public AnimatedTransformation(UInt16 amount) : base(amount) { }
    }
}
