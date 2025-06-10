using Caliburn.Micro;
using GlmSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using org.ogre;
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
using ICommand = TT_Lab.Command.ICommand;
using Position = TT_Lab.Rendering.Objects.Position;
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
        private readonly DirtyTracker _dirtyTracker;
        private readonly IActiveChunkService _activeChunkService;
        private readonly ICommand _unsavedChangesCommand;
        private readonly OpenDialogueCommand.DialogueResult _dialogueResult = new();
        private readonly List<ResourceTreeElementViewModel> _addedAssets = new();
        private readonly List<InstanceElementViewModel> _subscribedAssets = new();
        private readonly Dictionary<ResourceTreeElementViewModel, InstanceSectionResourceEditorViewModel> _trackedAssets = new();

        private string _tabDisplayName = string.Empty;
        private EditingContext _editingContext;
        private readonly List<SceneInstance> _sceneInstances = new();
        private CollisionData? _colData;
        private SceneEditorViewModel _sceneEditor = IoC.Get<SceneEditorViewModel>();
        private readonly InputController _inputController;
        private Collision _collisionRender;
        private Scenery _sceneryRender;
        private Skydome _skydomeRender;
        private SceneNode _instancesNode;
        private DrawFilter _drawFilter = DrawFilter.Scenery | DrawFilter.Triggers | DrawFilter.Positions | DrawFilter.Instances | DrawFilter.Cameras | DrawFilter.Skybox;

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
        }

        public ChunkEditorViewModel(IEventAggregator eventAggregator, IActiveChunkService activeChunkService)
        {
            _unsavedChangesCommand = new OpenDialogueCommand(() => new UnsavedChangesDialogue(_dialogueResult, AssetManager.Get().GetAsset(EditableResource).GetResourceTreeElement()));
            _sceneEditor.SceneHeaderModel = "Chunk Viewer (Loading! The UI may hang for a bit)";
            _inputController = new InputController(_sceneEditor);
            eventAggregator.SubscribeOnUIThread(this);
            _dirtyTracker = new DirtyTracker(this, EditorChangesHappened);
            _activeChunkService = activeChunkService;
            InitScene();
        }

        public override Task<Boolean> CanCloseAsync(CancellationToken cancellationToken = new CancellationToken())
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
            _sceneEditor.AddInputListener(this);
            _activeChunkService.SetCurrentChunkEditor(this);
            
            return base.OnActivateAsync(cancellationToken);
        }

        protected override Task OnDeactivateAsync(Boolean close, CancellationToken cancellationToken)
        {
            if (close)
            {
                TwinIdGeneratorServiceProvider.DeregisterGeneratorServiceForChunk(AssetManager.Get().GetAsset<ChunkFolder>(EditableResource).Variation);
            }
            
            _sceneEditor.RemoveInputListener(this);
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
                _collisionRender?.Dispose();
                _sceneryRender?.Dispose();
                _skydomeRender?.Dispose();
                _instancesNode?.Dispose();
                foreach (var sceneInstance in _sceneInstances)
                {
                    sceneInstance.Dispose();
                }
            }

            return base.OnDeactivateAsync(close, cancellationToken);
        }

        public SceneInstance NewSceneInstance(Type type, ResourceTreeElementViewModel basedOn)
        {
            Debug.Assert(_sceneEditor.RenderControl?.GetRenderWindow() != null, "Invalid editor state!");

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
            var sceneInstance = SceneInstanceFactory.CreateSceneInstance(type, _editingContext, newInstance.GetData<AbstractAssetData>(), newInstance.GetResourceTreeElement());
            _sceneInstances.Add(sceneInstance);
            _addedAssets.Add(newInstance.GetResourceTreeElement());

            if (type == typeof(ObjectSceneInstance))
            {
                _instancesNode.addChild(sceneInstance.GetEditableObject().getParentSceneNode());
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
            if (asset.Asset.Type == typeof(Folder) || asset.Asset.Type == typeof(TT_Lab.Assets.Instance.Scenery) || asset.Asset.Type == typeof(DynamicScenery))
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
        public SceneEditorViewModel SceneEditor { get => _sceneEditor; set => _sceneEditor = value; }
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

        public bool MouseMove(Object? sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || !_editingContext.IsInstanceSelected())
            {
                return false;
            }
            
            var pos = e.GetPosition(_sceneEditor.RenderControl);
            _editingContext.UpdateTransform((float)pos.X, (float)pos.Y);
            var renderWindow = _sceneEditor.RenderControl?.GetRenderWindow();
            renderWindow?.UpdateCamera();
            return true;
        }

        public bool MouseDown(Object? sender, MouseButtonEventArgs e)
        {
            _sceneEditor.UnlockMouseMove();
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                return false;
            }
            
            var pos = e.GetPosition(_sceneEditor.RenderControl);
            if ((_editingContext.TransformMode == TransformMode.SELECTION || _editingContext.TransformAxis == TransformAxis.NONE) && !_editingContext.IsInstanceSelected())
            {
                MouseSelect((float)pos.X, (float)pos.Y);
            }
            else if (_editingContext.IsInstanceSelected())
            {
                if (_editingContext.StartTransform((float)pos.X, (float)pos.Y))
                {
                    _sceneEditor.LockMouseMove();
                }
            }
            else
            {
                return false;
            }

            return true;
        }

        public bool MouseUp(Object? sender, MouseButtonEventArgs e)
        {
            _sceneEditor.UnlockMouseMove();
            if (!_editingContext.IsInstanceSelected())
            {
                return false;
            }
            
            var pos = e.GetPosition(_sceneEditor.RenderControl);
            _editingContext.EndTransform((float)pos.X, (float)pos.Y);
            return true;
        }

        public bool KeyPressed(Object sender, KeyEventArgs arg)
        {
            if (arg.IsRepeat || arg.IsUp || !_editingContext.IsInstanceSelected())
            {
                return false;
            }
            
            var key = arg.Key;
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

            return true;
        }

        private void MouseSelect(float x, float y)
        {
            if (_sceneEditor.RenderControl?.GetRenderWindow() == null)
            {
                return;
            }

            var ray = _sceneEditor.RenderControl.GetRenderWindow().GetRayFromViewport(x, y);
            var rayOrigin = ray.getOrigin();
            var rayDirection = ray.getDirection();

            _editingContext.Deselect();
            SceneInstance? result = null;
            if (!_inputController.Ctrl)
            {
                foreach (var instance in _sceneInstances)
                {
                    if (!instance.GetEditableObject().isVisible())
                    {
                        continue;
                    }
                    
                    var hit = new vec3();
                    var distance = 0.0f;
                    var worldPosition = instance.GetTransform() * new vec4(0, 0, 0, 1);
                    if (!MathExtension.IntersectRayBox(OgreExtensions.FromOgre(rayOrigin),
                            OgreExtensions.FromOgre(rayDirection), worldPosition.xyz, instance.GetOffset(),
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
                    var p1 = _colData.Vectors[triangle.Face.Indexes[0]];
                    var p2 = _colData.Vectors[triangle.Face.Indexes[1]];
                    var p3 = _colData.Vectors[triangle.Face.Indexes[2]];
                    if (!MathExtension.IntersectRayTriangle(OgreExtensions.FromOgre(rayOrigin),
                            OgreExtensions.FromOgre(rayDirection), new vec3(-p1.X, p1.Y, p1.Z),
                            new vec3(-p2.X, p2.Y, p2.Z), new vec3(-p3.X, p3.Y, p3.Z), ref distance, ref hit))
                    {
                        continue;
                    }
                    
                    _editingContext.SetCursorCoordinates(hit);
                    if (_inputController.Ctrl)
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
            _sceneEditor.SceneCreator = glControl =>
            {
                var assetManager = AssetManager.Get();
                var chunkAss = assetManager.GetAsset(EditableResource).GetResourceTreeElement();
                var chunk = chunkAss.GetAsset<ChunkFolder>();
                foreach (var item in chunk.GetData().To<FolderData>().Children)
                {
                    var resourceElement = assetManager.GetAsset(item).GetResourceTreeElement();
                    _chunkTree.Add(resourceElement);
                }
                SubscribeToElementEvents(_chunkTree);
                _isDefault = chunk.Name.ToLower() == "default";
                if (_isDefault)
                {
                    return;
                }

                var sceneManager = glControl.GetSceneManager();
                glControl.EnableImgui(true);
                glControl.SetCameraStyle(CameraStyle.CS_FREELOOK);
                glControl.SetCameraSpeed(50.0f);
                glControl.SetCameraPosition(vec3.Zero);

                _editingContext = new EditingContext(glControl, sceneManager, this);
                
                // Currently all stuff is created as is from the data without linking it to view models
                // TODO: Link all that together so that changes in the editor reflect on the end data
                _colData = _chunkTree.First(avm => avm.Asset.Type == typeof(Assets.Instance.Collision))!.Asset.GetData<CollisionData>();

                _collisionRender = new Collision("CollisionData", sceneManager, _colData);
                
                var instances = _chunkTree.First(avm => avm.Alias == "Instances");
                _instancesNode = sceneManager.getRootSceneNode().createChildSceneNode();
                var instanceRenderObject = sceneManager.createManualObject();
                _instancesNode.attachObject(instanceRenderObject);
                foreach (var instance in instances!.Children)
                {
                    var instData = instance.Asset.GetData<ObjectInstanceData>();
                    var objSceneInstance = SceneInstanceFactory.CreateSceneInstance<ObjectSceneInstance>(_editingContext, instData, instance);
                    _sceneInstances.Add(objSceneInstance);
                    _instancesNode.addChild(objSceneInstance.GetEditableObject().getParentSceneNode());
                }
                
                var scenery = _chunkTree.First(avm => avm.Asset.Section == Constants.SCENERY_SECENERY_ITEM).Asset.GetData<SceneryData>();
                if (scenery.SkydomeID != LabURI.Empty)
                {
                    _skydomeRender = new Skydome("SkydomeRender", sceneManager, assetManager.GetAssetData<SkydomeData>(scenery.SkydomeID));
                }

                _sceneryRender = new Scenery("SceneryRender", sceneManager, scenery);
                
                var triggers = _chunkTree.First(avm => avm.Alias == "Triggers");
                var triggersNode = sceneManager.getRootSceneNode().createChildSceneNode();
                triggersNode.attachObject(_editingContext.GetTriggersBillboards());
                foreach (var trigger in triggers!.Children)
                {
                    var trg = SceneInstanceFactory.CreateSceneInstance<TriggerSceneInstance>(_editingContext, trigger.Asset.GetData<AbstractAssetData>(), trigger, triggersNode);
                    _sceneInstances.Add(trg);
                }
                
                var positions = _chunkTree.First(avm => avm.Alias == "Positions");
                var positionsNode = sceneManager.getRootSceneNode().createChildSceneNode();
                positionsNode.attachObject(_editingContext.GetPositionBillboards());
                foreach (var position in positions!.Children)
                {
                    var billboard = _editingContext.CreatePositionBillboard();
                    var pos = new Position(position.Asset.URI, sceneManager, billboard, position.Asset.LayoutID!.Value, position.Asset.GetData<PositionData>());
                    positionsNode.attachObject(pos);
                }
                
                var aiPositions = _chunkTree.First(avm => avm.Alias == "AI Navigation Positions");
                var aiPositionsNode = sceneManager.getRootSceneNode().createChildSceneNode();
                aiPositionsNode.attachObject(_editingContext.GetAiPositionsBillboards());
                foreach (var aiPosition in aiPositions!.Children)
                {
                    var billboard = _editingContext.CreateAiPositionBillboard();
                    var aiPos = new AiPosition(aiPosition.Asset.URI, sceneManager, billboard, aiPosition.Asset.LayoutID!.Value, aiPosition.Asset.GetData<AiPositionData>());
                    aiPositionsNode.attachObject(aiPos);
                }
                
                var cameras = _chunkTree.First(avm => avm.Alias == "Cameras");
                var camerasNode = sceneManager.getRootSceneNode().createChildSceneNode();
                camerasNode.attachObject(_editingContext.GetCamerasBillboards());
                foreach (var camera in cameras!.Children)
                {
                    var cam = SceneInstanceFactory.CreateSceneInstance<CameraSceneInstance>(_editingContext, camera.Asset.GetData<AbstractAssetData>(), camera, camerasNode);
                    _sceneInstances.Add(cam);
                }

                glControl.OnRender += (sender, args) =>
                {
                    foreach (var sceneInstance in _sceneInstances)
                    {
                        sceneInstance.GetEditableObject().RenderUpdate();
                    }
                    
                    ImGui.Begin("Chunk Render Settings");
                    ImGui.SetWindowPos(new ImVec2(glControl.GetViewportWidth() - 300, 5));
                    ImGui.SetWindowSize(new ImVec2(295, 200));
                    ImguiRenderFilterCheckbox("Render Collision", _collisionRender, DrawFilter.Collision);
                    ImguiRenderFilterCheckbox("Render Scenery", _sceneryRender, DrawFilter.Scenery);
                    ImguiRenderFilterCheckbox("Render Skydome", _skydomeRender, DrawFilter.Skybox);
                    ImguiRenderFilterCheckbox("Render Positions", _editingContext.GetPositionBillboards(), DrawFilter.Positions);
                    ImguiRenderFilterCheckbox("Render Triggers", _editingContext.GetTriggersBillboards(), DrawFilter.Triggers);
                    ImguiRenderFilterCheckbox("Render Cameras", _editingContext.GetCamerasBillboards(), DrawFilter.Cameras);
                    ImguiRenderFilterCheckbox("Render AI Positions", _editingContext.GetAiPositionsBillboards(), DrawFilter.AiPositions);
                    ImguiRenderFilterCheckbox("Render Instances", instanceRenderObject, DrawFilter.Instances,
                        (enable) =>
                        {
                            foreach (var sceneInstance in _sceneInstances)
                            {
                                sceneInstance.EnableTextDisplay(enable);
                            }
                        });
                    ImGui.End();

                    if (_editingContext.IsInstanceSelected())
                    {
                        ImguiRenderControls(glControl);
                    }
                };

                _isChunkReady = true;

                SceneEditor.SceneHeaderModel = "Chunk Viewer";
            };
        }

        private void OnChunkTreeElementDuplicate(InstanceElementViewModel sender, ResourceTreeElementViewModel duplicate)
        {
            // _chunkTree.Refresh();
            _addedAssets.Add(duplicate);
            _dirtyTracker.MarkDirty();
        }

        private void ImguiRenderControls(OgreWindow glControl)
        {
            ImGui.Begin("Editor Info");
            ImGui.SetWindowPos(new ImVec2(5, glControl.GetViewportHeight() - 400));
            ImGui.SetWindowSize(new ImVec2(300, 395));
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

        private void ImguiRenderFilterCheckbox(string label, MovableObject renderObject, DrawFilter filter, Action<bool>? toggleCallback = null)
        {
            var renderEnabled = IsDrawFilterEnabled(filter);
            if (ImGui.Checkbox(label, ref renderEnabled) && !renderObject.isVisible())
            {
                renderObject.getParentSceneNode().setVisible(true, true);
                EnableDrawFilter(filter);
                toggleCallback?.Invoke(true);
            }
            else if (!renderEnabled && renderObject.isVisible())
            {
                renderObject.getParentSceneNode().setVisible(false, true);
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
