using GlmSharp;

namespace TT_Lab.Rendering.Objects;

/// <summary>
/// Where the scene's cursor is, once it got placed. <see cref="EditingContext"/> draws it
/// </summary>
internal class EditorCursor
{
    private vec3? _pos;

    public bool IsShown => _pos != null;

    public void SetPosition(vec3 newPos)
    {
        _pos = newPos;
    }

    public vec3 GetPosition()
    {
        return _pos ?? vec3.Zero;
    }
}
