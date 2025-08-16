using System;
using System.ComponentModel;
using System.Linq;
using GlmSharp;
using org.ogre;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.ViewModels.Composite;
using TT_Lab.ViewModels.Editors.Instance;
using TT_Lab.ViewModels.ResourceTree;
using TT_Lab.Views.Composite;

namespace TT_Lab.Rendering.Objects.SceneInstances
{
    [Flags]
    public enum SupportedTransforms
    {
        None = 0,
        Translate = 1 << TransformMode.TRANSLATE,
        Rotate = 1 << TransformMode.ROTATE,
        Scale = 1 << TransformMode.SCALE,
    }
    
    public abstract class SceneInstance : IDisposable
    {
        protected EditableObject AttachedEditableObject;
        protected readonly EditingContext EditingContext;
        protected readonly ResourceTreeElementViewModel AttachedViewModel;
        protected TextDisplay? TextDisplay;
        protected readonly AbstractAssetData AssetData;
        protected SupportedTransforms SupportedTransforms = SupportedTransforms.Translate | SupportedTransforms.Rotate;
        
        protected vec3 Position;
        protected vec3 Rotation;
        protected vec3 Offset;
        protected vec3 Size;
        protected Boolean IsSelected;

        private bool _editedDirectly = false;

        protected SceneInstance(EditingContext editingContext, AbstractAssetData data, ResourceTreeElementViewModel attachedViewModel)
        {
            AssetData = data;
            AttachedViewModel = attachedViewModel;
            EditingContext = editingContext;
        }

        public void Init(Renderable? parentNode)
        {
            CreateEditableObject(parentNode);
            AttachedEditableObject.Init();
        }

        protected abstract void CreateEditableObject(Renderable? parentNode = null);

        public ResourceTreeElementViewModel GetViewModel()
        {
            return AttachedViewModel;
        }

        public bool IsTransformSupported(TransformMode mode)
        {
            var supportCast = (SupportedTransforms)(1 << (int)mode);
            return SupportedTransforms.HasFlag(supportCast);
        }

        public void EnableTextDisplay(bool enabled)
        {
            TextDisplay?.Enable(enabled);
        }

        public void Translate(vec3 translation)
        {
            AttachedEditableObject.Translate(translation);
            Position += translation;
            SetPositionRotationScale(Position, Rotation, GetScale());
        }

        public void Rotate(vec3 rotation)
        {
            var rotQuat = new quat(rotation);
            AttachedEditableObject.Rotate(rotQuat, true);
            var currentRot = new quat(Rotation);
            var resultRot = currentRot.ToMat4 * rotQuat.ToMat4;
            Rotation = (vec3)quat.FromMat4(resultRot).EulerAngles;
            SetPositionRotationScale(Position, Rotation, GetScale());
        }

        public void Scale(vec3 scale)
        {
            AttachedEditableObject.Scale(scale);
            SetPositionRotationScale(Position, Rotation, GetScale());
        }

        public virtual void SetPositionRotationScale(vec3 position, vec3 rotation, vec3 scale = default)
        {
            if (!IsSelected)
            {
                return;
            }

            _editedDirectly = true;
            var editor = (ViewportEditableInstanceViewModel)EditingContext.GetCurrentEditor()!;
            editor.Position.X = position.x;
            editor.Position.Y = position.y;
            editor.Position.Z = position.z;
            editor.Rotation.X = glm.Degrees(rotation.x);
            editor.Rotation.Y = glm.Degrees(rotation.y);
            editor.Rotation.Z = glm.Degrees(rotation.z);
            if (scale != vec3.Zero)
            {
                editor.Scale.X = scale.x;
                editor.Scale.Y = scale.y;
                editor.Scale.Z = scale.z;
                AttachedEditableObject.SetScale(scale);
            }
            _editedDirectly = false;
            AttachedEditableObject.SetRotation(new quat(rotation));
            AttachedEditableObject.SetPosition(position);
        }

        public virtual void LinkChangesToViewModel(ViewportEditableInstanceViewModel viewModel)
        {
            viewModel.Position.PropertyChanged += PositionOnPropertyChanged;
            viewModel.Rotation.PropertyChanged += RotationOnPropertyChanged;
            viewModel.Scale.PropertyChanged += ScaleOnPropertyChanged;
        }

        private void ScaleOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "IsDirty" || _editedDirectly)
            {
                return;
            }

            var viewModel = (Vector3ViewModel)sender!;
            AttachedEditableObject.SetScale(new vec3(viewModel.X, viewModel.Y, viewModel.Z));
        }

        private void RotationOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "IsDirty" || _editedDirectly)
            {
                return;
            }
            
            var viewModel = (Vector3ViewModel)sender!;
            Rotation = new vec3(glm.Radians(viewModel.X), glm.Radians(viewModel.Y), glm.Radians(viewModel.Z));
            AttachedEditableObject.SetRotation(new quat(Rotation));
        }

        private void PositionOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "IsDirty" || _editedDirectly)
            {
                return;
            }

            var viewModel = (Vector4ViewModel)sender!;
            Position = new vec3(viewModel.X, viewModel.Y, viewModel.Z);
            AttachedEditableObject.SetPosition(Position);
        }

        public virtual void UnlinkChangesToViewModel(ViewportEditableInstanceViewModel viewModel)
        {
            viewModel.Position.PropertyChanged -= PositionOnPropertyChanged;
            viewModel.Rotation.PropertyChanged -= RotationOnPropertyChanged;
            viewModel.Scale.PropertyChanged -= ScaleOnPropertyChanged;
        }

        public void Select()
        {
            AttachedEditableObject.Select();
            IsSelected = true;
        }

        public void Deselect()
        {
            IsSelected = false;
            AttachedEditableObject.Deselect();
        }

        public EditableObject GetEditableObject()
        {
            return AttachedEditableObject;
        }

        public vec3 GetOffset()
        {
            return Offset;
        }
        
        public vec3 GetSize()
        {
            return Size;
        }
        
        public vec3 GetPosition()
        {
            return AttachedEditableObject.GetPosition();
        }
        
        public vec3 GetRotation()
        {
            return AttachedEditableObject.GetRotation();
        }

        public vec3 GetScale()
        {
            return AttachedEditableObject.GetScale();
        }

        public mat4 GetTransform()
        {
            return mat4.Translate(Position) * (new quat(Rotation)).ToMat4;
        }

        public mat4 GetWorldTransform()
        {
            return AttachedEditableObject.WorldTransform;
        }

        public void Dispose()
        {
            TextDisplay?.Dispose();
        }
    }
}
