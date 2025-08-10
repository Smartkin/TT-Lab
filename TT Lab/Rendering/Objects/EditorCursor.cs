using System.Drawing;
using GlmSharp;
using TT_Lab.Extensions;
using TT_Lab.Util;

namespace TT_Lab.Rendering.Objects;

internal class EditorCursor
{
    private vec3? _pos = null;
    private Renderable _cursorNode;

    public EditorCursor(EditingContext editingContext)
    {
        // _cursorNode = editingContext.GetEditorNode().createChildSceneNode("EditorCursorNode");
        // var cube = BufferGeneration.GetCubeBuffer("EditorCursorCube", Color.Purple);
        // var entity = editingContext.CreateEntity(cube);
        // entity.setMaterial(MaterialManager.GetMaterial("Gizmo"));
        // _cursorNode.attachObject(entity);
        // _cursorNode.setScale(0.2f, 0.2f, 0.2f);
        // _cursorNode.setVisible(false);
    }

    public Renderable GetCursorNode()
    {
        return _cursorNode;
    }

    public void SetPosition(vec3 newPos)
    {
        _pos = newPos;
        // _cursorNode.setPosition(OgreExtensions.FromGlm(newPos));
        // _cursorNode.setVisible(true);
    }

    public vec3 GetPosition()
    {
        return _pos ?? vec3.Zero;
    }

}