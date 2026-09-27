using System;
using System.Collections.Generic;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;

namespace TT_Lab.AssetResolvers;

/// <summary>
/// Collects the animations game objects use, the OGIs they're played on keep them once those are known
/// </summary>
public class AnimationResolver
{
    private readonly Dictionary<UInt32, ITwinAnimation> _animations = new();

    public void Collect(ITwinSection animationSection, UInt32 id)
    {
        if (!_animations.ContainsKey(id) && animationSection.ContainsItem(id))
        {
            _animations[id] = animationSection.GetItem<ITwinAnimation>(id);
        }
    }

    public ITwinAnimation? Get(UInt32 id) => _animations.GetValueOrDefault(id);
}
