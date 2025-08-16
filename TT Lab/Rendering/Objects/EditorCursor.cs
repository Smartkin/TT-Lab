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
        _cursorNode = BufferGeneration.GetCubeBuffer().Model!;
        var purple = Color.Purple;
        _cursorNode.Diffuse = new vec4(purple.R / 255.0f,  purple.G / 255.0f, purple.B / 255.0f, 1.0f);
        _cursorNode.IsVisible = false;
        _cursorNode.Scale(new vec3(0.25f, 0.25f, 0.25f), true);
        editingContext.GetEditorNode().AddChild(_cursorNode);
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
        _cursorNode.SetPosition(newPos);
        _cursorNode.IsVisible = true;
    }

    public vec3 GetPosition()
    {
        return _pos ?? vec3.Zero;
    }

}