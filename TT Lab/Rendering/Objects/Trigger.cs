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
    private readonly vec4 _billboardColour;

    public Trigger(RenderContext context, string name, Renderable parentNode, Billboard billboard, TriggerData data, vec3 size, KnownColor renderColor = KnownColor.DarkOrange) : base(context, parentNode, name, size)
    {
        _triggerData = data;
        _billboard = billboard;
        var cube = BufferGeneration.GetCubeBuffer();
        if (cube.Model != null)
        {
            var color = System.Drawing.Color.FromKnownColor(renderColor);
            cube.Model.Diffuse = new vec4(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f,  color.A / 255.0f * 0.5f);
            SelectedColor = cube.Model.Diffuse * 0.5f;
            UnselectedColor = cube.Model.Diffuse;
            _billboardColour = cube.Model.Diffuse;
            AddChild(cube.Model);
        }
        // var color = System.Drawing.Color.FromKnownColor(renderColor);
        // var cubeMesh = BufferGeneration.GetCubeBuffer($"DefaultCube_{color}", System.Drawing.Color.FromArgb((int)(new Color(color.R, color.G, color.B, 64).ToARGB())));
        // var entity = sceneManager.createEntity(cubeMesh);
        // entity.setMaterial(MaterialManager.GetMaterial("ColorOnlyTransparent"));
        // SceneNode.attachObject(entity);

        // _billboard.setPosition(-data.Position.X, data.Position.Y, data.Position.Z);
        // _billboardColour = new ColourValue(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f);
        // _billboard.setColour(_billboardColour);
    }

    public override void Select()
    {
        _billboard.IsVisible = false;
        
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
        // LocalTransform = mat4.Identity; //mat4.Translate(Pos) * mat4.RotateZ(glm.Radians(Rot.z)) * mat4.RotateY(glm.Radians(Rot.y)) * mat4.RotateX(glm.Radians(Rot.x)) * mat4.Scale(Scl);
        // SetPosition(new vec3(-Scl.x / 2f, -Scl.y / 2f, -Scl.z / 2f));
        // var glmVec = _triggerData.Rotation.ToGlm();
        // var quat = new quat(glmVec.x, glmVec.y, glmVec.z, glmVec.w);
        // Rotate(quat);
        // Scale(Scl, true);
        // SetPosition(Pos);

        if (!Selected)
        {
            _billboard.SetPosition(Pos);
        }
        
        base.UpdateSceneTransform();
    }
}