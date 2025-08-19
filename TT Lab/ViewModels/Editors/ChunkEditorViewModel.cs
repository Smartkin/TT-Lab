using Caliburn.Micro;
using GlmSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using ImGuiNET;
using Silk.NET.Input;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Graphics;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Instance;
using TT_Lab.Command;
using TT_Lab.Controls;
using TT_Lab.Extensions;
using TT_Lab.Project.Messages;
using TT_Lab.Rendering;
using TT_Lab.Rendering.Buffers;
using TT_Lab.Rendering.Objects;
using TT_Lab.Rendering.Objects.SceneInstances;
using TT_Lab.Rendering.Scene;
using TT_Lab.Rendering.Services;
using TT_Lab.ServiceProviders;
using TT_Lab.Services;
using TT_Lab.Util;
using TT_Lab.ViewModels.Composite;
using TT_Lab.ViewModels.Editors.Instance;
using TT_Lab.ViewModels.Interfaces;
using TT_Lab.ViewModels.ResourceTree;
using Twinsanity.TwinsanityInterchange.Enumerations;
using AiPosition = TT_Lab.Rendering.Objects.AiPosition;
using Camera = TT_Lab.Rendering.Objects.Camera;
using Collision = TT_Lab.Rendering.Objects.Collision;
using DynamicScenery = TT_Lab.Rendering.Objects.DynamicScenery;
using ICommand = TT_Lab.Command.ICommand;
using Position = TT_Lab.Rendering.Objects.Position;
using Renderable = TT_Lab.Rendering.Renderable;
using Scenery = TT_Lab.Rendering.Objects.Scenery;
using Trigger = TT_Lab.Rendering.Objects.Trigger;

namespace TT_Lab.ViewModels.Editors
{
    public class ChunkEditorViewModel :
        Conductor<IScreen>.Collection.AllActive,
        IEditorViewModel,
        IInputListener,
        IDirtyMarker
    {
        private readonly BindableCollection<ResourceTreeElementViewModel> _chunkTree = new();
        private bool _isDefault;
        private bool _isChunkReady = false;
        private bool _usingConfirmClose = false;
        private IKeyboard? _keyboard;
        private Renderer? _renderer;
        private Scene? _scene;
        private readonly DirtyTracker _dirtyTracker;
        private readonly IActiveChunkService _activeChunkService;
        private readonly SceneInstanceFactory _sceneInstanceFactory;
        private readonly RenderContext _renderContext;
        private readonly MeshService _meshService;
        private readonly ICommand _unsavedChangesCommand;
        private readonly OpenDialogueCommand.DialogueResult _dialogueResult = new();
        private readonly List<ResourceTreeElementViewModel> _addedAssets = [];
        private readonly List<InstanceElementViewModel> _subscribedAssets = [];
        private readonly Dictionary<ResourceTreeElementViewModel, InstanceSectionResourceEditorViewModel> _trackedAssets = new();

        private string _tabDisplayName = string.Empty;
        private EditingContext _editingContext;
        private readonly List<SceneInstance> _sceneInstances = [];
        private CollisionData? _colData;
        private ViewportViewModel _sceneEditor = IoC.Get<ViewportViewModel>();
        private Collision _collisionRender;
        private Scenery _sceneryRender;
        private Skydome? _skydomeRender;
        private DynamicScenery _dynamicSceneryRender;
        private Node _instancesNode;
        private Node _triggersNode;
        private Node _camerasNode;
        private Node _linkedScenery;
        private DrawFilter _drawFilter = DrawFilter.Scenery | DrawFilter.Triggers | DrawFilter.Positions | DrawFilter.Instances | DrawFilter.Cameras | DrawFilter.Skybox | DrawFilter.LinkedScenery;

        [Flags]
        private enum DrawFilter
        {
            Disabled = 0,
            Scenery = 1 << 0,
            Collision = 1 << 1,
            Instances = 1 << 2,
            Positions = 1 << 3,
            Triggers = 1 << 4,
            Cameras = 1 << 5,
            Skybox = 1 << 6,
            Paths = 1 << 7,
            AiPositions = 1 << 8,
            AiPaths = 1 << 9,
            DynamicScenery = 1 << 10,
            Lighting = 1 << 11,
            LinkedScenery = 1 << 12,
        }

        public ChunkEditorViewModel(IEventAggregator eventAggregator, IActiveChunkService activeChunkService, SceneInstanceFactory sceneInstanceFactory, RenderContext renderContext, MeshService meshService)
        {
            _unsavedChangesCommand = new OpenDialogueCommand(() => new UnsavedChangesDialogue(_dialogueResult, AssetManager.Get().GetAsset(EditableResource).GetResourceTreeElement()));
            eventAggregator.SubscribeOnUIThread(this);
            _dirtyTracker = new DirtyTracker(this, EditorChangesHappened);
            _activeChunkService = activeChunkService;
            _sceneInstanceFactory = sceneInstanceFactory;
            _renderContext = renderContext;
            _meshService = meshService;
            InitScene();
        }

        public override Task<Boolean> CanCloseAsync(CancellationToken cancellationToken = new())
        {
            if (IsDirty)
            {
                _usingConfirmClose = true;
                _unsavedChangesCommand.Execute();
            }

            return Task.Factory.StartNew(() =>
            {
                if (!IsDirty)
                {
                    return true;
                }
            
                if (_dialogueResult.Result == null)
                {
                    return false;
                }
            
                var result = MiscUtils.ConvertEnum<UnsavedChangesDialogue.AnswerResult>(_dialogueResult.Result);
                switch (result)
                {
                    case UnsavedChangesDialogue.AnswerResult.YES:
                    case UnsavedChangesDialogue.AnswerResult.DISCARD:
                        return true;
                    case UnsavedChangesDialogue.AnswerResult.CANCEL:
                    default:
                        return false;
                }
            }, cancellationToken);
        }

        protected override void OnViewReady(object view)
        {
            base.OnViewReady(view);

            if (Parent is TabbedEditorViewModel tabbedEditorViewModel)
            {
                _tabDisplayName = tabbedEditorViewModel.DisplayName;
            }
            TwinIdGeneratorServiceProvider.RegisterGeneratorServiceForChunk(AssetManager.Get().GetAsset<ChunkFolder>(EditableResource));
            ResetDirty();
        }

        protected override Task OnActivateAsync(CancellationToken cancellationToken)
        {
            _activeChunkService.SetCurrentChunkEditor(this);
            ActivateItemAsync(SceneEditor, cancellationToken);
            
            return base.OnActivateAsync(cancellationToken);
        }

        protected override Task OnDeactivateAsync(Boolean close, CancellationToken cancellationToken)
        {
            if (close)
            {
                TwinIdGeneratorServiceProvider.DeregisterGeneratorServiceForChunk(AssetManager.Get().GetAsset<ChunkFolder>(EditableResource).Variation);
            }
            
            _activeChunkService.SetCurrentChunkEditor(null);
            
            foreach (var item in Items.Select(s => s).ToArray())
            {
                DeactivateItemAsync(item, close, cancellationToken);
            }

            if (IsDirty)
            {
                foreach (var addedAsset in _addedAssets)
                {
                    addedAsset.Asset.Delete(true);
                }
            }

            if (close)
            {
                _isChunkReady = false;
                UnsubscribeToDuplicatingItems();
                _chunkTree.Clear();
                // _collisionRender?.Dispose();
                // _sceneryRender?.Dispose();
                // _skydomeRender?.Dispose();
                // _instancesNode?.Dispose();
                foreach (var sceneInstance in _sceneInstances)
                {
                    sceneInstance.Dispose();
                }

                DeactivateItemAsync(SceneEditor, close, cancellationToken);
            }

            return base.OnDeactivateAsync(close, cancellationToken);
        }

        public SceneInstance NewSceneInstance(Type type, ResourceTreeElementViewModel basedOn)
        {
            var chunk = AssetManager.Get().GetAsset<ChunkFolder>(EditableResource);
            var newInstance = AssetFactory.CreateAsset(basedOn.Asset.Type, basedOn.Parent != null ? basedOn.Parent.GetAsset<Folder>() : chunk,
                $"New {basedOn.Asset.GetType().Name} {(uint)Guid.NewGuid().GetHashCode()}", chunk.Variation,
                TwinIdGeneratorServiceProvider.GetGeneratorForChunk(basedOn.Asset.Type, chunk.Variation, (Enums.Layouts)basedOn.Asset.LayoutID!),
                (asset) =>
                {
                    var instanceAsset = (SerializableInstance)asset;
                    instanceAsset.Chunk = chunk.Variation[..^3] + (instanceAsset.IsInScenery ? "sm2" : "rm2");
                    var assetData = basedOn.Asset.GetData<AbstractAssetData>();
                    asset.SetData((AbstractAssetData)CloneUtils.DeepClone(assetData, assetData.GetType()));
                    return AssetCreationStatus.Success;
                },
                (Enums.Layouts)basedOn.Asset.LayoutID)!;
            var sceneInstance = _sceneInstanceFactory.CreateSceneInstance(type, _editingContext, newInstance.GetData<AbstractAssetData>(), newInstance.GetResourceTreeElement());
            _sceneInstances.Add(sceneInstance);
            _addedAssets.Add(newInstance.GetResourceTreeElement());

            if (type == typeof(ObjectSceneInstance))
            {
                // _instancesNode.addChild(sceneInstance.GetEditableObject().getParentSceneNode());
            }
            
            // _chunkTree.Refresh();
            
            _dirtyTracker.MarkDirty();

            return sceneInstance;
        }

        public void InstanceEditorChanged(RoutedPropertyChangedEventArgs<Object> e)
        {
            if (e.NewValue == null)
            {
                if (CurrentInstanceEditor != null)
                {
                    DeactivateItemAsync(CurrentInstanceEditor, false);
                    CurrentInstanceEditor = null;
                    NotifyOfPropertyChange(nameof(CurrentInstanceEditor));
                }
                return;
            }
            var asset = (ResourceTreeElementViewModel)e.NewValue;
            if (asset.Asset.Type == typeof(Folder) || asset.Asset.Type == typeof(TT_Lab.Assets.Instance.Scenery) || asset.Asset.Type == typeof(TT_Lab.Assets.Instance.DynamicScenery))
            {
                return;
            }

            try
            {
                if (CurrentInstanceEditor != null)
                {
                    DeactivateItemAsync(CurrentInstanceEditor, false);
                }

                CurrentInstanceEditor = (InstanceSectionResourceEditorViewModel)IoC.GetInstance(asset.Asset.GetEditorType(), null);
                CurrentInstanceEditor.EditableResource = asset.Asset.URI;
                CurrentInstanceEditor.ParentEditor = this;
                ActivateItemAsync(CurrentInstanceEditor);
                NotifyOfPropertyChange(nameof(CurrentInstanceEditor));
                if (_trackedAssets.ContainsKey(asset))
                {
                    return;
                }
                
                _dirtyTracker.AddChild(CurrentInstanceEditor);
                _trackedAssets.Add(asset, CurrentInstanceEditor);
            }
            catch (Exception ex)
            {
                Log.WriteLine($"Failed to create editor: {ex.Message}");
            }
        }

        public void SelectDifferentInstance(RoutedPropertyChangedEventArgs<Object> e)
        {
            if (!_isChunkReady)
            {
                return;
            }
            
            _editingContext.Deselect();
            if (e.NewValue == null)
            {
                return;
            }
            
            foreach (var sceneInstance in _sceneInstances)
            {
                if (sceneInstance.GetViewModel() != e.NewValue)
                {
                    continue;
                }
                
                _editingContext.Select(sceneInstance);
                break;
            }
        }

        public BindableCollection<ResourceTreeElementViewModel> ChunkTree => _chunkTree;
        public LabURI EditableResource { get; set; } = LabURI.Empty;
        public ViewportViewModel SceneEditor { get => _sceneEditor; set => _sceneEditor = value; }
        public InstanceSectionResourceEditorViewModel? CurrentInstanceEditor { get; set; }

        public bool IsDirty => _dirtyTracker.IsDirty;
        
        public void ResetDirty()
        {
            _dirtyTracker.ResetDirty();
        }

        protected override Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            ActivateItemAsync(_sceneEditor, cancellationToken);

            return base.OnInitializeAsync(cancellationToken);
        }

        public void SaveChanges(bool force = false)
        {
            if (!IsDirty)
            {
                return;
            }
 
            ResetDirty();
            
            if (_usingConfirmClose && (_dialogueResult.Result == null || MiscUtils.ConvertEnum<UnsavedChangesDialogue.AnswerResult>(_dialogueResult.Result) == UnsavedChangesDialogue.AnswerResult.DISCARD))
            {
                return;
            }
            
            foreach (var instanceEditor in _trackedAssets)
            {
                instanceEditor.Value.SaveChanges(true);
            }
        }

        public void MouseMove(IMouse mouse, Vector2 mousePos)
        {
            if (!mouse.IsButtonPressed(MouseButton.Left) || !_editingContext.IsInstanceSelected())
            {
                return;
            }
            
            var pos = mousePos;
            _editingContext.UpdateTransform(pos.X, pos.Y);
            // var renderWindow = _sceneEditor.RenderControl?.GetRenderWindow();
            // renderWindow?.UpdateCamera();
        }

        public void MouseDown(IMouse mouse, MouseButton button)
        {
            // _sceneEditor.UnlockMouseMove();
            if (button != MouseButton.Left)
            {
                return;
            }
            
            var pos = mouse.Position;
            if ((_editingContext.TransformMode == TransformMode.SELECTION || _editingContext.TransformAxis == TransformAxis.NONE) && !_editingContext.IsInstanceSelected())
            {
                MouseSelect(pos.X, pos.Y);
            }
            else if (_editingContext.IsInstanceSelected())
            {
                if (_editingContext.StartTransform(pos.X, pos.Y))
                {
                    // _sceneEditor.LockMouseMove();
                }
            }
        }

        public void MouseUp(IMouse mouse, MouseButton button)
        {
            // _sceneEditor.UnlockMouseMove();
            if (!_editingContext.IsInstanceSelected())
            {
                return;
            }
            
            var pos = mouse.Position;
            _editingContext.EndTransform(pos.X, pos.Y);
        }

        public void KeyPressed(IKeyboard keyboard, Key key, int scancode)
        {
            if (key == Key.T)
            {
                _editingContext.ToggleTranslate();
            }
            else if (key == Key.R)
            {
                _editingContext.ToggleRotate();
            }
            else if (key == Key.S)
            {
                _editingContext.ToggleScale();
            }
            else if (key == Key.X)
            {
                _editingContext.SetTransformAxis(TransformAxis.X);
            }
            else if (key == Key.Y)
            {
                _editingContext.SetTransformAxis(TransformAxis.Y);
            }
            else if (key == Key.Z)
            {
                _editingContext.SetTransformAxis(TransformAxis.Z);
            }
            else if (key == Key.Left)
            {
                _editingContext.MoveCursorGrid(-vec3.UnitX);
            }
            else if (key == Key.Right)
            {
                _editingContext.MoveCursorGrid(vec3.UnitX);
            }
            else if (key == Key.Up)
            {
                _editingContext.MoveCursorGrid(vec3.UnitZ);
            }
            else if (key == Key.Down)
            {
                _editingContext.MoveCursorGrid(-vec3.UnitZ);
            }
            else if (key == Key.PageUp)
            {
                _editingContext.MoveCursorGrid(vec3.UnitY);
            }
            else if (key == Key.PageDown)
            {
                _editingContext.MoveCursorGrid(-vec3.UnitY);
            }
            else if (key == Key.K && _editingContext.SelectedInstance != null)
            {
                _editingContext.SetPalette(_editingContext.SelectedInstance);
            }
            else if (key == Key.P)
            {
                _editingContext.SpawnAtCursor();
            }
            else if (key == Key.G)
            {
                _editingContext.SetGrid();
            }
            else if (key == Key.U)
            {
                _editingContext.Deselect();
            }
        }

        private void MouseSelect(float x, float y)
        {
            if (_renderer == null || _scene == null || _keyboard == null)
            {
                return;
            }
            
            var rayOrigin = _scene.Camera.GetPosition();
            var rayDirection = _scene.Camera.GetRayFromViewport(x, y);
            
            _editingContext.Deselect();
            SceneInstance? result = null;
            if (!_keyboard.IsKeyPressed(Key.ControlLeft))
            {
                foreach (var instance in _sceneInstances)
                {
                    if (!instance.GetEditableObject().IsVisible)
                    {
                        continue;
                    }
                    
                    var hit = new vec3();
                    var distance = 0.0f;
                    var worldPosition = instance.GetWorldTransform().Column3.xyz;
                    if (!MathExtension.IntersectRayBox(rayOrigin, rayDirection, worldPosition.xyz, instance.GetOffset(),
                            instance.GetSize(), instance.GetTransform(), ref distance, ref hit))
                    {
                        continue;
                    }
                    
                    result = instance;
                    break;
                }
            
                if (result != null)
                {
                    _editingContext.Select(result);
                }
            }
            
            if (result == null && _colData != null)
            {
                var hit = new vec3();
                var distance = 0.0f;
                foreach (var triangle in _colData.Triangles)
                {
                    var p1 = _colData.Vectors[triangle.Face.Indexes![0]];
                    var p2 = _colData.Vectors[triangle.Face.Indexes[1]];
                    var p3 = _colData.Vectors[triangle.Face.Indexes[2]];
                    if (!MathExtension.IntersectRayTriangle(rayOrigin, rayDirection, new vec3(p1.X, p1.Y, p1.Z),
                            new vec3(p2.X, p2.Y, p2.Z), new vec3(p3.X, p3.Y, p3.Z), ref distance, ref hit))
                    {
                        continue;
                    }
                    
                    _editingContext.SetCursorCoordinates(hit);
                    if (_keyboard.IsKeyPressed(Key.ControlLeft))
                    {
                        _editingContext.SpawnAtCursor();
                    }
                    break;
                }
            }
        }

        private bool IsDrawFilterEnabled(DrawFilter filter)
        {
            return _drawFilter.HasFlag(filter);
        }

        private void EnableDrawFilter(DrawFilter filter)
        {
            _drawFilter |= filter;
        }

        private void DisableDrawFilter(DrawFilter filter)
        {
            _drawFilter &= ~filter;
        }

        private void SubscribeToElementEvents(BindableCollection<ResourceTreeElementViewModel>? folder)
        {
            if (folder == null)
            {
                return;
            }
            
            foreach (var item in folder)
            {
                switch (item)
                {
                    case FolderElementViewModel:
                        SubscribeToElementEvents(item.Children);
                        break;
                    case InstanceElementViewModel instanceElement:
                        instanceElement.OnDeleted += OnChunkTreeElementDeleted;
                        instanceElement.OnDuplicate += OnChunkTreeElementDuplicate;
                        _subscribedAssets.Add(instanceElement);
                        break;
                }
            }
        }

        private void OnChunkTreeElementDeleted(ResourceTreeElementViewModel deletedViewModel)
        {
            _trackedAssets.Remove(deletedViewModel);
        }

        private void UnsubscribeToDuplicatingItems()
        {
            foreach (var subscribedAssets in _subscribedAssets)
            {
                subscribedAssets.OnDuplicate -= OnChunkTreeElementDuplicate;
            }
        }

        private void InitScene()
        {
            _sceneEditor.SceneInitializer = (renderer, scene) =>
            {
                _scene = scene;
                _renderer = renderer;
                var assetManager = AssetManager.Get();
                var chunkAss = assetManager.GetAsset(EditableResource).GetResourceTreeElement();
                var chunk = chunkAss.GetAsset<ChunkFolder>();
                foreach (var resourceElement in chunk.GetData().To<FolderData>().Children.Select(item => assetManager.GetAsset(item).GetResourceTreeElement()))
                {
                    _chunkTree.Add(resourceElement);
                }
                SubscribeToElementEvents(_chunkTree);
                
                _isDefault = chunk.Name.Equals("default", StringComparison.InvariantCultureIgnoreCase);
                if (_isDefault)
                {
                    return;
                }

                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    var inputContext = _renderer.GetInputContext()!;
                    inputContext.Keyboards[0].KeyDown += KeyPressed;
                    _keyboard = inputContext.Keyboards[0];
                    inputContext.Mice[0].MouseDown += MouseDown;
                    inputContext.Mice[0].MouseUp += MouseUp;
                    inputContext.Mice[0].MouseMove += MouseMove;
                });

                _editingContext = new EditingContext(_renderContext, scene, this);
                
                // Currently all stuff is created as is from the data without linking it to view models
                // TODO: Link all that together so that changes in the editor reflect on the end data
                var collisionUri = _chunkTree.First(avm => avm.Asset.Type == typeof(Assets.Instance.Collision))!.Asset.URI;
                _colData = _chunkTree.First(avm => avm.Asset.Type == typeof(Assets.Instance.Collision))!.Asset.GetData<CollisionData>();
                _collisionRender = (Collision)_meshService.GetMesh(collisionUri).Model!;
                scene.AddChild(_collisionRender);
                
                var instances = _chunkTree.First(avm => avm.Alias == "Instances");
                _instancesNode = new Node(_renderContext, scene);
                foreach (var instance in instances!.Children)
                {
                    var instData = instance.Asset.GetData<ObjectInstanceData>();
                    var objSceneInstance = _sceneInstanceFactory.CreateSceneInstance<ObjectSceneInstance>(_editingContext, instData, instance);
                    _sceneInstances.Add(objSceneInstance);
                    _instancesNode.AddChild(objSceneInstance.GetEditableObject());
                }
                
                var dynamicScenery = _chunkTree.First(avm => avm.Asset.Section == Constants.SCENERY_DYNAMIC_SECENERY_ITEM).Asset.GetData<DynamicSceneryData>();
                _dynamicSceneryRender = new DynamicScenery(_renderContext, _meshService, dynamicScenery);
                scene.AddChild(_dynamicSceneryRender);
                
                var scenery = _chunkTree.First(avm => avm.Asset.Section == Constants.SCENERY_SECENERY_ITEM).Asset.GetData<SceneryData>();
                if (scenery.SkydomeID != LabURI.Empty)
                {
                    _skydomeRender = new Skydome(_renderContext, assetManager.GetAssetData<SkydomeData>(scenery.SkydomeID), _meshService);
                    scene.AddChild(_skydomeRender);
                }

                _sceneryRender = new Scenery(_renderContext, _meshService, scenery);
                scene.AddChild(_sceneryRender);

                var chunkLinks = _chunkTree.First(avm => avm.Asset.Section == Constants.SCENERY_LINK_ITEM).Asset.GetData<ChunkLinksData>();
                _linkedScenery = new Node(_renderContext, scene, "Linked Scenery");
                foreach (var link in chunkLinks.Links)
                {
                    // if (!link.IsRendered)
                    // {
                    //     continue;
                    // }
                    
                    var linkedChunk = assetManager.GetAsset<ChunkFolder>(link.Path);
                    var chunkData = linkedChunk.GetData().To<FolderData>();
                    var linkedSceneryUri = chunkData.Children.First(uri => assetManager.GetAsset(uri).Section == Constants.SCENERY_SECENERY_ITEM);
                    var linkedScenery = assetManager.GetAssetData<SceneryData>(linkedSceneryUri);
                    var linkedSceneryNode = new Node(_renderContext, _linkedScenery);
                    var linkedSceneryRender = new Scenery(_renderContext, _meshService, linkedScenery);
                    linkedSceneryNode.AddChild(linkedSceneryRender);
                    var chunkMatrix = link.ChunkMatrix.ToGlm();
                    linkedSceneryNode.LocalTransform = chunkMatrix;
                }
                var triggers = _chunkTree.First(avm => avm.Alias == "Triggers");
                _triggersNode = new Node(_renderContext, scene);
                _triggersNode.AddChild(_editingContext.GetTriggersBillboards());
                foreach (var trigger in triggers!.Children)
                {
                    var trg = _sceneInstanceFactory.CreateSceneInstance<TriggerSceneInstance>(_editingContext, trigger.Asset.GetData<AbstractAssetData>(), trigger, _triggersNode);
                    _sceneInstances.Add(trg);
                }
                
                
                var positions = _chunkTree.First(avm => avm.Alias == "Positions");
                var positionsNode = new Node(_renderContext, scene);
                positionsNode.AddChild(_editingContext.GetPositionBillboards());
                foreach (var position in positions!.Children)
                {
                    var billboard = _editingContext.CreatePositionBillboard();
                    var pos = new Position(_renderContext, position.Asset.URI, billboard, position.Asset.LayoutID!.Value, position.Asset.GetData<PositionData>());
                    positionsNode.AddChild(pos);
                }
                
                var aiPositions = _chunkTree.First(avm => avm.Alias == "AI Navigation Positions");
                var aiPositionsNode = new Node(_renderContext, scene);
                aiPositionsNode.AddChild(_editingContext.GetAiPositionsBillboards());
                foreach (var aiPosition in aiPositions!.Children)
                {
                    var billboard = _editingContext.CreateAiPositionBillboard();
                    var aiPos = new AiPosition(_renderContext, aiPosition.Asset.URI, billboard, aiPosition.Asset.LayoutID!.Value, aiPosition.Asset.GetData<AiPositionData>());
                    aiPositionsNode.AddChild(aiPos);
                }
                
                var cameras = _chunkTree.First(avm => avm.Alias == "Cameras");
                _camerasNode = new Node(_renderContext, scene);
                _camerasNode.AddChild(_editingContext.GetCamerasBillboards());
                foreach (var camera in cameras!.Children)
                {
                    var cam = _sceneInstanceFactory.CreateSceneInstance<CameraSceneInstance>(_editingContext, camera.Asset.GetData<AbstractAssetData>(), camera, _camerasNode);
                    _sceneInstances.Add(cam);
                }

                renderer.RenderImgui += () =>
                {
                    ImGui.Begin("Chunk Render Settings");
                    ImGui.SetWindowPos(new Vector2(renderer.GetFrameBufferSize().x - 300, 5));
                    ImGui.SetWindowSize(new Vector2(295, 200));
                    ImguiRenderFilterCheckbox("Render Collision", _collisionRender, DrawFilter.Collision);
                    ImguiRenderFilterCheckbox("Render Scenery", _sceneryRender, DrawFilter.Scenery);
                    if (_skydomeRender != null)
                    {
                        ImguiRenderFilterCheckbox("Render Skydome", _skydomeRender, DrawFilter.Skybox);
                    }

                    ImguiRenderFilterCheckbox("Render Positions", _editingContext.GetPositionBillboards(), DrawFilter.Positions);
                    ImguiRenderFilterCheckbox("Render Triggers", _triggersNode, DrawFilter.Triggers);
                    ImguiRenderFilterCheckbox("Render Cameras", _camerasNode, DrawFilter.Cameras);
                    ImguiRenderFilterCheckbox("Render AI Positions", _editingContext.GetAiPositionsBillboards(), DrawFilter.AiPositions);
                    ImguiRenderFilterCheckbox("Render Instances", _instancesNode, DrawFilter.Instances);
                    ImguiRenderFilterCheckbox("Render Linked Scenery", _linkedScenery, DrawFilter.LinkedScenery);
                    ImGui.End();

                    if (_editingContext.IsInstanceSelected())
                    {
                        ImguiRenderControls(renderer);
                        _editingContext.SelectedRenderable?.RenderUpdate();
                    }
                };

                _isChunkReady = true;

                // SceneEditor.SceneHeaderModel = "Chunk Viewer";
            };
        }

        private void OnChunkTreeElementDuplicate(InstanceElementViewModel sender, ResourceTreeElementViewModel duplicate)
        {
            // _chunkTree.Refresh();
            _addedAssets.Add(duplicate);
            _dirtyTracker.MarkDirty();
        }

        private void ImguiRenderControls(Renderer renderer)
        {
            ImGui.Begin("Editor Info");
            ImGui.SetWindowPos(new Vector2(5, renderer.GetFrameBufferSize().y - 400));
            ImGui.SetWindowSize(new Vector2(300, 395));
            ImGui.Text($"Editing mode: {_editingContext.TransformMode}");
            ImGui.Text("U - Unselect");
            ImGui.Text("T - Toggle translate");
            ImGui.Text("R - Toggle rotate");
            ImGui.Text("S - Toggle scale");
            ImGui.Text("X - Edit on X axis");
            ImGui.Text("Y - Edit on Y axis");
            ImGui.Text("Z - Edit on Z axis");
            ImGui.Text("G - Move edit cursor on a grid");
            ImGui.Text("P - Create duplicate at cursor's position");
            ImGui.Text("K - Add current selection to palette");
            ImGui.End();
        }

        private void ImguiRenderFilterCheckbox(string label, Renderable renderObject, DrawFilter filter, Action<bool>? toggleCallback = null)
        {
            var renderEnabled = IsDrawFilterEnabled(filter);
            if (ImGui.Checkbox(label, ref renderEnabled) && !renderObject.IsVisible)
            {
                renderObject.IsVisible = true;
                EnableDrawFilter(filter);
                toggleCallback?.Invoke(true);
            }
            else if (!renderEnabled && renderObject.IsVisible)
            {
                renderObject.IsVisible = false;
                DisableDrawFilter(filter);
                toggleCallback?.Invoke(false);
            }
        }
        
        private void EditorChangesHappened()
        {
            if (Parent is not TabbedEditorViewModel parent || !_isChunkReady)
            {
                return;
            }
            
            if (IsDirty)
            {
                parent.DisplayName = _tabDisplayName + "*";
            }
            else
            {
                parent.DisplayName = _tabDisplayName;
            }
        }
    }
}
