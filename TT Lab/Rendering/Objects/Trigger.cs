using GlmSharp;
using System;
using System.Drawing;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Extensions;
using TT_Lab.Rendering.Materials;
using TT_Lab.Rendering.Scene;
using TT_Lab.Util;
using TT_Lab.ViewModels.Editors.Instance;
using Color = Twinsanity.TwinsanityInterchange.Common.Color;

namespace TT_Lab.Rendering.Objects;

public class Trigger : EditableObject
{
    private Billboard _billboard;
    private readonly TriggerData _triggerData;

    public Trigger(RenderContext context, string name, Renderable parentNode, Billboard billboard, TriggerData data, vec3 size, KnownColor renderColor = KnownColor.DarkOrange) : base(context, parentNode, name, size)
    {
        _triggerData = data;
        _billboard = billboard;
        var cube = BufferGeneration.GetCubeBuffer();
        if (cube.Model == null)
        {
            return;
        }
        
        var color = System.Drawing.Color.FromKnownColor(renderColor);
        cube.Model.Diffuse = new vec4(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f,  color.A / 255.0f * 0.5f);
        SelectedColor = cube.Model.Diffuse * 0.75f;
        UnselectedColor = cube.Model.Diffuse;
        AddChild(cube.Model);
    }

    public override void Select()
    {
        _billboard.SetPosition(new vec3(-10000, -10000, -10000));
        
        base.Select();
    }

    public override void Deselect()
    {
        _billboard.SetPosition(Pos);
            
        base.Deselect();
    }

    protected override void InitSceneTransform()
    {
        Pos = new vec3(_triggerData.Position.X, _triggerData.Position.Y, _triggerData.Position.Z);
        var rotEuler = _triggerData.Rotation.ToEulerAngles();
        Rot = new vec3(rotEuler.X, rotEuler.Y, rotEuler.Z);
        Scl = Size;
    }

    protected override void UpdateSceneTransform()
    {
        if (!Selected)
        {
            _billboard.SetPosition(Pos);
        }
        
        base.UpdateSceneTransform();
    }
}