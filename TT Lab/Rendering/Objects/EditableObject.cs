using System;
using GlmSharp;
using System.Collections.Generic;
using System.Linq;
using org.ogre;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Graphics;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Extensions;
using TT_Lab.Rendering.Buffers;

namespace TT_Lab.Rendering.Objects
{
    public abstract class EditableObject : Renderable
    {
        protected vec3 Pos = new();
        protected vec3 Rot = new();
        protected vec3 Scl = new();
        protected vec3 Size;
        protected bool Selected;

        protected EditableObject(RenderContext context, string name, vec3 size = new()) : base(context, name)
        {
            if (size == vec3.Zero)
            {
                size = vec3.Ones;
            }
            Selected = false;
            Size = size;
            Scl = vec3.Ones;
        }

        protected EditableObject(RenderContext context, Renderable parentNode, string name, vec3 size = new()) : base(context, name)
        {
            if (size == vec3.Zero)
            {
                size = vec3.Ones;
            }
            parentNode.AddChild(this);
            Selected = false;
            Scl = vec3.Ones;
            Size = size;
        }

        public void Init()
        {
            InitSceneTransform();
            UpdateSceneTransform();
        }

        protected abstract void InitSceneTransform();

        protected virtual void UpdateSceneTransform()
        {
            LocalTransform = mat4.Identity; //mat4.Translate(Pos) * mat4.RotateZ(glm.Radians(Rot.z)) * mat4.RotateY(glm.Radians(Rot.y)) * mat4.RotateX(glm.Radians(Rot.x)) * mat4.Scale(Scl);
            Scale(Scl);
            Rotate(new vec3(glm.Radians(Rot.x), glm.Radians(Rot.y), glm.Radians(Rot.z)));
            SetPosition(Pos);
        }

        public virtual void Select()
        {
            Selected = true;
            Diffuse = new vec4(0.3f, 0.3f, 0.3f, 1.0f);
        }

        public virtual void Deselect()
        {
            Selected = false;
            Diffuse = new vec4(1.0f, 1.0f, 1.0f, 1.0f);
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
            ImGui.SetWindowPos(new ImVec2(5, 5));
            ImGui.SetWindowSize(new ImVec2(400, 100));
            DrawImGuiInternal();
            ImGui.End();
        }

        protected virtual void DrawImGuiInternal()
        {
            var rotation = GetRotation();
            rotation.y = -rotation.y;
            rotation.z = -rotation.z;
            var position = GetPosition();
            position.x = -position.x;
            ImGui.Text($"Position: {position}");
            ImGui.Text($"Rotation: {rotation}");
            ImGui.Text($"Scale: {GetScale()}");
            ImGui.Text($"Bounding Box Size: {Size}");
        }
    }
}
