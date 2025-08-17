using System;
using System.Linq;
using Caliburn.Micro;
using System.Threading;
using System.Threading.Tasks;
using TT_Lab.Assets;
using TT_Lab.Rendering;
using TT_Lab.Rendering.Buffers;
using TT_Lab.Rendering.Objects;
using TT_Lab.Rendering.Services;
using TT_Lab.Util;
using Mesh = TT_Lab.Rendering.Objects.Mesh;

namespace TT_Lab.ViewModels.Editors.Graphics
{
    public class ModelViewModel : ResourceEditorViewModel
    {
        private readonly MeshService _meshService;
        private Mesh? _model;

        public ModelViewModel(MeshService meshService)
        {
            _meshService = meshService;
            // Scenes.Add(IoC.Get<SceneEditorViewModel>());
            // Scenes[0].SceneHeaderModel = "Model viewer";
            // InitSceneRenderer();
            SceneRenderer = IoC.Get<ViewportViewModel>();
            SceneRenderer.SceneInitializer = (renderer, scene) =>
            {
                var mesh = _meshService.GetMesh(EditableResource);
                if (mesh.Model != null)
                {
                    scene.AddChild(mesh.Model);
                }
            };
        }

        protected override async Task OnActivateAsync(CancellationToken cancellationToken)
        {
            await ActivateItemAsync(SceneRenderer, cancellationToken);
            
            await base.OnActivateAsync(cancellationToken);
        }

        protected override async Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
        {
            await DeactivateItemAsync(SceneRenderer, close, cancellationToken);
            
            await base.OnDeactivateAsync(close, cancellationToken);
        }

        // private void InitSceneRenderer()
        // {
        //     SceneRenderer.SceneCreator = glControl =>
        //     {
        //         var sceneManager = glControl.GetSceneManager();
        //         var pivot = sceneManager.getRootSceneNode().createChildSceneNode();
        //         pivot.setPosition(0, 0, 0);
        //         glControl.SetCameraTarget(pivot);
        //         glControl.EnableImgui(true);
        //
        //         var model = AssetManager.Get().GetAssetData<AssetData.Graphics.ModelData>(EditableResource);
        //         _model = new ModelBuffer(sceneManager, EditableResource, model);
        //
        //         glControl.OnRender += (sender, args) =>
        //         {
        //             ImGui.Begin("Model Data");
        //             ImGui.SetWindowPos(new ImVec2(5, 5));
        //             ImGui.SetWindowSize(new ImVec2(150, 90));
        //             ImGui.Text($"Vertexes {model.Vertexes.Sum(v => v.Count)}");
        //             ImGui.Text($"Faces {model.Faces.Sum(f => f.Count)}");
        //             ImGui.Text($"Meshes {model.Meshes.Count}");
        //             ImGui.End();
        //         };
        //     };
        // }

        public ViewportViewModel SceneRenderer { get; }

        public override void LoadData()
        {
            return;
        }

        protected override void Save()
        {
            return;
        }
    }
}
