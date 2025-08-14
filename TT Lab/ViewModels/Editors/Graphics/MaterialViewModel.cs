using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using org.ogre;
using TT_Lab.AssetData.Graphics;
using TT_Lab.AssetData.Graphics.Shaders;
using TT_Lab.Assets;
using TT_Lab.Attributes;
using TT_Lab.Command;
using TT_Lab.Controls;
using TT_Lab.Rendering;
using TT_Lab.Rendering.Objects;
using TT_Lab.Util;
using static Twinsanity.TwinsanityInterchange.Enumerations.Enums;

namespace TT_Lab.ViewModels.Editors.Graphics
{
    public class MaterialViewModel : ResourceEditorViewModel
    {
        private AppliedShaders _activatedShaders;
        private UInt32 _dmaChainIndex;
        private String _name;
        private readonly BindableCollection<ShaderViewModel> _shaders = new();
        private ShaderViewModel _selectedShader;

        public MaterialViewModel()
        {
            DirtyTracker.AddBindableCollection(Shaders);
            Scenes.Add(IoC.Get<SceneEditorViewModel>());
            MaterialViewer.SceneHeaderModel = "Material viewer";
            InitMaterialViewer();
        }

        protected override Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
        {
            foreach (var shaderViewModel in _shaders)
            {
                DeactivateItemAsync(shaderViewModel, close, cancellationToken);
            }
            
            return base.OnDeactivateAsync(close, cancellationToken);
        }

        public void Save(ref MaterialData data)
        {
            data.ActivatedShaders = ActivatedShaders;
            data.DmaChainIndex = DmaChainIndex;
            data.Name = Name;
            data.Shaders.Clear();
            foreach (var svm in Shaders)
            {
                var shader = new LabShader();
                svm.Save(shader);
                data.Shaders.Add(shader);
            }
        }

        public void Copy(ref MaterialData data)
        {
            data.ActivatedShaders = ActivatedShaders;
            data.DmaChainIndex = DmaChainIndex;
            data.Name = Name;
            data.Shaders.Clear();
            foreach (var svm in Shaders)
            {
                var shader = new LabShader();
                svm.Copy(shader);
                data.Shaders.Add(shader);
            }
        }

        protected override void Save()
        {
            var asset = AssetManager.Get().GetAsset(EditableResource);
            var data = asset.GetData<MaterialData>();
            Save(ref data);
            
            base.Save();
        }

        public override void LoadData()
        {
            var asset = AssetManager.Get().GetAsset(EditableResource);
            var matData = asset.GetData<MaterialData>();
            _activatedShaders = matData.ActivatedShaders;
            _dmaChainIndex = matData.DmaChainIndex;
            _name = new String(matData.Name.AsSpan()[..]);
            _shaders.Clear();
            foreach (var shaderViewModel in matData.Shaders.Select(shader => new ShaderViewModel(shader, this)))
            {
                _shaders.Add(shaderViewModel);
                Items.Add(shaderViewModel);
            }

            AddShaderCommand = new AddItemToListCommand<ShaderViewModel>(Shaders, () => new ShaderViewModel(new LabShader(), this), 5);
            DeleteShaderCommand = new DeleteItemFromListCommand(Shaders);
            CloneShaderCommand = new CloneItemIntoCollectionCommand<ShaderViewModel>(Shaders, 5);
            CurrentSelectedShader = _shaders[0];
            
            PropertyChanged += (sender, args) =>
            {
                MaterialViewer.ResetScene();
            };
        }

        private void InitMaterialViewer()
        {
            MaterialViewer.SceneCreator = glControl =>
            {
                // var sceneManager = glControl.GetSceneManager();
                // var pivot = sceneManager.getRootSceneNode().createChildSceneNode();
                // pivot.setPosition(0, 0, 0);
                // glControl.SetCameraTarget(pivot);
                // glControl.SetCameraStyle(CameraStyle.CS_ORBIT);
                //
                // var plane = sceneManager.getRootSceneNode().createChildSceneNode();
                // var entity = sceneManager.createEntity(BufferGeneration.GetPlaneBuffer());
                // var material = TwinMaterialGenerator.GenerateMaterialFromViewModel(this, default, true);
                // entity.setMaterial(material.Material);
                // entity.getSubEntity(0).setCustomParameter(0, new Vector4(1.0f, 1.0f, 1.0f, 1.0f));
                // plane.attachObject(entity);
                // plane.scale(0.05f, 0.05f, 1f);
            };
        }

        public void ChangeShaderSettings(SelectedItemChangedEventArgs args)
        {
            if (args.GetSelectedItem<ShaderViewModel>() == null)
            {
                return;
            }
            
            DeactivateItemAsync(CurrentSelectedShader, false);
            CurrentSelectedShader = args.GetSelectedItem<ShaderViewModel>()!;
            DeleteShaderCommand.Index = Shaders.IndexOf(CurrentSelectedShader);
            CloneShaderCommand.Item = CurrentSelectedShader;
        }

        public AddItemToListCommand<ShaderViewModel> AddShaderCommand { private set; get; }
        public DeleteItemFromListCommand DeleteShaderCommand { private set; get; }
        public CloneItemIntoCollectionCommand<ShaderViewModel> CloneShaderCommand { private set; get; }

        public SceneEditorViewModel MaterialViewer
        {
            get => Scenes[0];
        }

        public ShaderViewModel CurrentSelectedShader
        {
            get => _selectedShader;
            set
            {
                if (_selectedShader != value)
                {
                    _selectedShader = value;
                    
                    NotifyOfPropertyChange();
                }
            }
        }

        [MarkDirty]
        public AppliedShaders ActivatedShaders
        {
            get => _activatedShaders;
            private set
            {
                if (value != _activatedShaders)
                {
                    _activatedShaders = value;
                    
                    NotifyOfPropertyChange();
                }
            }
        }
        
        [MarkDirty]
        public UInt32 DmaChainIndex
        {
            get => _dmaChainIndex;
            set
            {
                if (value != _dmaChainIndex)
                {
                    _dmaChainIndex = value;
                    
                    NotifyOfPropertyChange();
                }
            }
        }
        
        [MarkDirty]
        public String Name
        {
            get => _name;
            set
            {
                if (value != _name)
                {
                    _name = value;
                    
                    NotifyOfPropertyChange();
                }
            }
        }

        public BindableCollection<ShaderViewModel> Shaders => _shaders;
    }
}
