namespace TT_Lab.Rendering.Lighting;

/// <summary>
/// Where the game gathers the lights for everything drawn under it: an object instance's position (<c>FUN_001fe290</c> hands the light
/// controller its instance's matrix), the same lights for every part of the object
/// </summary>
public interface ILightAnchor;

public static class LightAnchor
{
    /// <summary>
    /// The renderable's object, or its topmost parent when it's under none (a model on its own)
    /// </summary>
    public static Renderable Of(Renderable renderable)
    {
        var current = renderable;
        while (current is not ILightAnchor && current.Parent != null)
        {
            current = current.Parent;
        }

        return current;
    }
}
