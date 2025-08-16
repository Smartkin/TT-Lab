using System;
using GlmSharp;
using System.Collections.Generic;
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

namespace TT_Lab.Rendering.Objects
{
    public abstract class EditableObject : Renderable
    {
        protected vec3 Pos = new();
        protected vec3 Rot = new();
        protected vec3 Scl;
        protected vec3 Size;
        protected bool Selected;
        protected vec4 SelectedColor = new(0.3f, 0.3f, 0.3f, 1.0f);
        protected vec4 UnselectedColor = new(1.0f, 1.0f, 1.0f, 1.0f);

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
            SetInitialPosition(Pos);
            SetInitialRotation(new quat(Rot));
            SetInitialScale(Scl);
            UpdateSceneTransform();
        }

        protected abstract void InitSceneTransform();

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

        public override vec3 GetPosition()
        {
            return Pos;
        }

        public override vec3 GetRotation()
        {
            return Rot;
        }

        public override vec3 GetScale()
        {
            return Scl;
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
            ImGui.SetWindowPos(new Vector2(5, 5));
            ImGui.SetWindowSize(new Vector2(400, 100));
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
}
