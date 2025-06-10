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
    public abstract class EditableObject : ManualObject
    {
        protected vec3 AmbientColor = new();
        protected vec3 Pos = new();
        protected vec3 Rot = new();
        protected vec3 Scl = new();
        protected vec3 Size;
        protected bool Selected;
        protected SceneNode SceneNode;
        protected TextDisplay? TextDisplay;

        private OgreWindow _window;

        protected EditableObject(OgreWindow window, string name, vec3 size = new(), TextDisplay? display = null) : base(name)
        {
            if (size == vec3.Zero)
            {
                size = vec3.Ones;
            }
            var sceneManager = window.GetSceneManager();
            SceneNode = sceneManager.createSceneNode();
            SceneNode.attachObject(this);
            Selected = false;
            Size = size;
            Scl = vec3.Ones;
            TextDisplay = display;
            _window = window;
        }

        protected EditableObject(OgreWindow window, SceneNode parentNode, string name, vec3 size = new(), TextDisplay? display = null) : base(name)
        {
            if (size == vec3.Zero)
            {
                size = vec3.Ones;
            }
            SceneNode = parentNode.createChildSceneNode();
            SceneNode.attachObject(this);
            Selected = false;
            Scl = vec3.Ones;
            Size = size;
            TextDisplay = display;
            _window = window;
        }

        public void Init()
        {
            InitSceneTransform();
            UpdateSceneTransform();
        }

        protected abstract void InitSceneTransform();

        public SceneNode GetSceneNode()
        {
            return SceneNode;
        }
        
        public mat4 GetTransform()
        {
            var transform = mat4.Translate(Pos);
            transform *= mat4.RotateX(Rot.x);
            transform *= mat4.RotateY(Rot.y);
            transform *= mat4.RotateZ(Rot.z);
            transform *= mat4.Scale(Scl);

            return transform;
        }

        public void Translate(vec3 offset)
        {
            Pos += offset;
            UpdateSceneTransform();
        }

        public void Rotate(vec3 rotOffset)
        {
            Rot += rotOffset;
            UpdateSceneTransform();
        }

        public void Scale(vec3 scale)
        {
            Scl += scale;
            UpdateSceneTransform();
        }

        public void SetPos(vec3 pos)
        {
            Pos = pos;
            UpdateSceneTransform();
        }

        public void SetRot(vec3 rotation)
        {
            Rot = rotation;
            UpdateSceneTransform();
        }

        public void SetScale(vec3 scale)
        {
            Scl = scale;
            UpdateSceneTransform();
        }

        public vec3 GetPosition()
        {
            return new vec3(SceneNode.getPosition().x, SceneNode.getPosition().y, SceneNode.getPosition().z);
        }

        public vec3 GetScale()
        {
            return OgreExtensions.FromOgre(SceneNode.getScale());
        }

        public vec3 GetRotation()
        {
            var renderQuat = SceneNode.getOrientation();
            var rotationMatrix = new Matrix3();
            renderQuat.ToRotationMatrix(rotationMatrix);
            var rotX = new Radian();
            var rotY = new Radian();
            var rotZ = new Radian();
            rotationMatrix.ToEulerAnglesXYZ(rotX, rotY, rotZ);
            return new vec3(rotX.valueDegrees(), rotY.valueDegrees(), rotZ.valueDegrees());
        }

        protected virtual void UpdateSceneTransform()
        {
            SceneNode.resetToInitialState();
            SceneNode.setPosition(OgreExtensions.FromGlm(Pos));
            SceneNode.pitch(new Radian(new Degree(Rot.x)));
            SceneNode.yaw(new Radian(new Degree(Rot.y)));
            SceneNode.roll(new Radian(new Degree(Rot.z)));
            SceneNode.setScale(OgreExtensions.FromGlm(Scl));
        }

        public virtual void Select()
        {
            Selected = true;
        }

        public virtual void Deselect()
        {
            Selected = false;
        }

        public void RenderUpdate()
        {
            TextDisplay?.Update();
            
            if (!Selected)
            {
                return;
            }
            
            DrawImGui();
        }

        private void DrawImGui()
        {
            ImGui.Begin(getName());
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
            ImGui.Text($"Position: {GetPosition()}");
            ImGui.Text($"Rotation: {rotation}");
            ImGui.Text($"Scale: {GetScale()}");
            ImGui.Text($"Bounding Box Size: {Size}");
        }
    }
}
