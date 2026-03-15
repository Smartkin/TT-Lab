using System;
using GlmSharp;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using ImGuiNET;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Graphics;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Extensions;
using TT_Lab.Rendering.Buffers;

namespace TT_Lab.Rendering.Objects;

public class EditableObject : Renderable
{
    public virtual bool IsSelectable { get; init; } = true;
    
    protected vec3 Pos = new();
    protected vec3 Rot = new();
    protected vec3 Scl;
    protected vec3 Size;
    protected vec3 Offset;
    protected bool Selected;
    protected vec4 SelectedColor = new(0.3f, 0.3f, 0.3f, 1.0f);
    protected vec4 UnselectedColor = new(1.0f, 1.0f, 1.0f, 1.0f);

    public EditableObject(RenderContext context, Renderable visual, string name, vec3 offset = new(), vec3 size = new()) : base(context, name)
    {
        if (size == vec3.Zero)
        {
            size = vec3.Ones;
        }
        Selected = false;
        Size = size;
        Offset = offset;
        Scl = vec3.Ones;
        
        AddChild(visual);
    }

    public void Init()
    {
        InitSceneTransform();
        SetInitialPosition(Pos);
        SetInitialRotation(new quat(Rot));
        SetInitialScale(Scl);
        UpdateSceneTransform();
    }

    protected virtual void InitSceneTransform()
    {
    }

    protected virtual void UpdateSceneTransform()
    {
        ResetLocalTransform();
    }

    public virtual void Select()
    {
        Selected = true;
        Diffuse = SelectedColor;
    }

    public virtual void Deselect()
    {
        Selected = false;
        Diffuse = UnselectedColor;
    }

    public override void SetPosition(vec3 position)
    {
        Pos = position;
        SetInitialPosition(Pos);
        UpdateSceneTransform();
    }

    public void SetScale(vec3 scale)
    {
        Scl = scale;
        SetInitialScale(Scl);
        UpdateSceneTransform();
    }

    public void SetRotation(quat rotation)
    {
        Rot = (vec3)rotation.EulerAngles;
        SetInitialRotation(rotation);
        UpdateSceneTransform();
    }

    public vec3 GetSize()
    {
        return Size * Scl;
    }

    public vec3 GetOffset()
    {
        return Offset;
    }

    public vec3 GetEditorPosition()
    {
        return Pos;
    }

    public vec3 GetEditorRotation()
    {
        return Rot;
    }

    public vec3 GetEditorScale()
    {
        return Scl;
    }

    public override void Translate(vec3 translation, bool inLocalSpace = false)
    {
        if (inLocalSpace)
        {
            Pos += (new quat(Rot)) * translation;
        }
        else
        {
            Pos += translation;
        }

        base.Translate(translation, inLocalSpace);
    }

    public override void Rotate(quat rotation, bool inLocalSpace = false)
    {
        var eulerAngles = rotation.EulerAngles;
        Rot += new vec3((float)eulerAngles.x, (float)eulerAngles.y, (float)eulerAngles.z);
        Rot = vec3.Degrees(Rot);
        Rot = vec3.Radians(Rot);
        
        base.Rotate(rotation, inLocalSpace);
    }

    public override void Scale(vec3 scale, bool inLocalSpace = false)
    {
        Scl *= scale;
        
        base.Scale(scale, inLocalSpace);
    }

    public void RenderUpdate()
    {
        if (!Selected)
        {
            return;
        }
            
        DrawImGui();
    }

    private void DrawImGui()
    {
        ImGui.Begin(Name);
        ImGui.SetWindowPos(new Vector2(5, 5), ImGuiCond.FirstUseEver);
        ImGui.SetWindowSize(new Vector2(400, 100),  ImGuiCond.FirstUseEver);
        DrawImGuiInternal();
        ImGui.End();
    }

    protected virtual void DrawImGuiInternal()
    {
        var rotation = GetRotation();
        rotation.x = glm.Degrees(rotation.x);
        rotation.y = glm.Degrees(rotation.y);
        rotation.z = glm.Degrees(rotation.z);
        var position = GetPosition();
        ImGui.Text($"Position: {position}");
        ImGui.Text($"Rotation: {rotation}");
        ImGui.Text($"Scale: {GetScale()}");
        ImGui.Text($"Bounding Box Size: {Size}");
    }
}