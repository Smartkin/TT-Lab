using GlmSharp;
using System;
using System.Drawing;
using org.ogre;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Extensions;
using TT_Lab.Util;
using TT_Lab.ViewModels.Editors.Instance;
using Color = Twinsanity.TwinsanityInterchange.Common.Color;

namespace TT_Lab.Rendering.Objects
{
    public class Trigger : EditableObject
    {
        private Billboard _billboard;
        private readonly TriggerData _triggerData;
        private readonly ColourValue _billboardColour;

        public Trigger(OgreWindow window, string name, SceneNode parentNode, SceneManager sceneManager, Billboard billboard, TriggerData data, vec3 size, KnownColor renderColor = KnownColor.DarkOrange) : base(window, parentNode, name, size)
        {
            _triggerData = data;
            _billboard = billboard;
            var color = System.Drawing.Color.FromKnownColor(renderColor);
            var cubeMesh = BufferGeneration.GetCubeBuffer($"DefaultCube_{color}", System.Drawing.Color.FromArgb((int)(new Color(color.R, color.G, color.B, 64).ToARGB())));
            var entity = sceneManager.createEntity(cubeMesh);
            entity.setMaterial(MaterialManager.GetMaterial("ColorOnlyTransparent"));
            SceneNode.attachObject(entity);
            
            _billboard.setPosition(-data.Position.X, data.Position.Y, data.Position.Z);
            _billboardColour = new ColourValue(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f);
            _billboard.setColour(_billboardColour);
        }

        public override void Select()
        {
            _billboard.setPosition(-1000, -1000, -1000);
            
            base.Select();
        }

        public override void Deselect()
        {
            _billboard.setPosition(Pos.x, Pos.y, Pos.z);
            
            base.Deselect();
        }

        protected override void InitSceneTransform()
        {
            Pos = new vec3(-_triggerData.Position.X, _triggerData.Position.Y, _triggerData.Position.Z);
            var rotEuler = _triggerData.Rotation.ToEulerAngles();
            Rot = new vec3(rotEuler.X, -rotEuler.Y, -rotEuler.Z);
            Size.x = -Size.x;
            Scl = Size;
        }

        protected override void UpdateSceneTransform()
        {
            base.UpdateSceneTransform();

            if (!Selected)
            {
                _billboard.setPosition(Pos.x, Pos.y, Pos.z);
            }
        }
    }
}
