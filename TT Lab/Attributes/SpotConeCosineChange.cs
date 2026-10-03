using TT_Lab.ViewModels.Editors.PropertyGraph;
using Twinsanity.TwinsanityInterchange.Common.Lights;

namespace TT_Lab.Attributes;

/// <summary>
/// A spot light's cosines, which the game lights with, made again from its cone and falloff angles like the tools made them
/// </summary>
public sealed class SpotConeCosineChange : IFieldChange
{
    public void DataChanged(PropertyNode listeningNode, PropertyNode changedNode)
    {
        if (listeningNode.Target is not SpotLight light)
        {
            return;
        }

        var (inner, outer) = SpotLight.ConeCosines(light.ConeAngle, light.FalloffAngle);
        listeningNode.SetValue(listeningNode.Name == nameof(SpotLight.InnerConeCosine) ? inner : outer);
    }
}
