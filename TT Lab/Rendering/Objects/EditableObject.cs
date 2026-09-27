using System;
using GlmSharp;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Graphics;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Extensions;
using TT_Lab.Rendering.Buffers;
using TT_Lab.Rendering.Objects.Gizmo;
using TT_Lab.Rendering.Scene;

namespace TT_Lab.Rendering.Objects;

public class EditableObject : Renderable
{
    public virtual bool IsSelectable { get; init; } = true;
    
    protected vec3 Pos = new();
    protected vec3 Rot = new();
    protected vec3 Scl;
    public vec3 Size;
    public vec3 Offset;
    protected bool Selected;
    private quat _orientation = quat.Identity;
    public bool IsSelected => Selected;
    public vec4 SelectedColor { get; set; } = new(0.3f, 0.3f, 0.3f, 1.0f);
    public vec4 UnselectedColor { get; set; } = new(1.0f, 1.0f, 1.0f, 1.0f);

    public EditableObject(RenderContext context, Renderable? visual, string name, vec3 offset = new(), vec3 size = new()) : base(context, name)
    {
        if (size == vec3.Zero)
        {
            size = vec3.Ones;
        }
        Selected = false;
        Size = size;
        Offset = offset;
        Scl = vec3.Ones;

        if (visual != null)
        {
            AddChild(visual);
        }
    }

    public void Init()
    {
        InitSceneTransform();
        _orientation = new quat(Rot);
        SetInitialPosition(Pos);
        SetInitialRotation(_orientation);
        SetInitialScale(Scl);
        UpdateSceneTransform();
    }

    public quat Orientation => _orientation;

    /// <summary>
    /// Where the object's box used for picking and outlining its selection is, maps the cube from -1 to 1 onto it
    /// </summary>
    public mat4 GetBoundsTransform()
    {
        return WorldTransform * mat4.Translate(Offset + Size * 0.5f) * mat4.Scale(Size * 0.5f);
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
        HighlightBillboards(true);
    }

    public virtual void Deselect()
    {
        Selected = false;
        Diffuse = UnselectedColor;
        HighlightBillboards(false);
    }

    // Billboards are drawn by their set in its own color, the tint doesn't reach them
    private void HighlightBillboards(bool highlight)
    {
        foreach (var child in Children)
        {
            if (child is Billboard billboard)
            {
                billboard.IsHighlighted = highlight;
            }
        }
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
        _orientation = rotation;
        Rot = (vec3)rotation.EulerAngles;
        SetInitialRotation(rotation);
        UpdateSceneTransform();
    }

    public vec3 GetSize()
    {
        return Size;
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
        _orientation = inLocalSpace ? _orientation * rotation : rotation * _orientation;
        var eulerAngles = rotation.EulerAngles;
        Rot += new vec3((float)eulerAngles.x, (float)eulerAngles.y, (float)eulerAngles.z);
        Rot = (vec3.Degrees(Rot) % 360 + 360) % 360;
        Rot = vec3.Radians(Rot);
        
        base.Rotate(rotation, inLocalSpace);
    }

    public override void Scale(vec3 scale, bool inLocalSpace = false)
    {
        Scl *= scale;
        
        base.Scale(scale, inLocalSpace);
    }

    /// <summary>
    /// What the viewport shows about the selection in its corner
    /// </summary>
    public string Describe()
    {
        // GetRotation doesn't take out the scale first, which skews the angles of anything scaled unevenly
        var rotation = vec3.Degrees(GetRotationQuat().ToEulerAngles());
        var position = GetPosition();
        return $"{Name}\nPosition: {position}\nRotation: {rotation}\nScale: {GetScale()}\nBounding box size: {Size}";
    }
}