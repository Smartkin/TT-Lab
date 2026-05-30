using System;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Caliburn.Micro;
using DynamicData;
using GlmSharp;
using ImGuiNET;
using ReactiveUI;
using Splat;
using TT_Lab.AssetData.Code;
using TT_Lab.Assets;
using TT_Lab.Extensions;
using TT_Lab.Rendering;
using TT_Lab.Rendering.Objects;
using TT_Lab.Rendering.Scene;
using TT_Lab.Rendering.Services;
using TT_Lab.Util;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using TT_Lab.ViewModels.Interfaces;
using Twinsanity.TwinsanityInterchange.Common.Animation;
using Math = System.Math;

namespace TT_Lab.ViewModels.Editors.Code;

public class AnimationViewModel : DocumentDataViewModel<AnimationData>
{
    private RenderContext _context;
    private short _playbackFps;
    private ushort _totalFrames;
    private ushort _currentAnimationFrame;
    private float _frameFraction;
    private float _frameDisplacement;
    private bool _isPlaying;
    private bool _imguiInitialized = false;
    private bool _isLooping = true;
    private int _runCounter = 0;
    private bool _doRunningCounter = false;
    private Scene _scene;
    private Rendering.Objects.OGI? _ogiRender;
    private TwinAnimation _twinAnimation;
    private TwinMorphAnimation _twinMorphAnimation;
    private Stopwatch _renderWatch = new Stopwatch();
    private LabURI _selectedOgi = LabURI.Empty;
    private bool _tryingToClose = false;

    public AnimationViewModel(DocumentViewModel document, PropertyNode animationData, params DocumentNodeViewModel[] dependencies) : base(document, animationData, dependencies)
    {
        LoadData();
    }

    private void LoadData()
    {
        var data = CurrentValue!;
        _playbackFps = data.DefaultFPS;
        _totalFrames = data.TotalFrames;
        _twinAnimation = data.MainAnimation;
        _twinMorphAnimation = data.FacialAnimation;
        _currentAnimationFrame = 0;

        _doRunningCounter = Preferences.GetPreference<bool>(Preferences.SillinessEnabled) && data.GetOwner().ID == 2;
        
        SuitableModels.Clear();
        var allOgis = AssetManager.Get().GetAllAssetsOf<Assets.Code.OGI>().Where(ogi =>
            ogi.GetData().To<OGIData>().Joints.Count >= data.MainAnimation.JointSettings.Count).ToList();
        var allOgiUris = allOgis.Select(ogi => ogi.URI).ToList();
        var bestFitIndex = allOgis.FindIndex(ogi => ogi.GetData().To<OGIData>().Joints.Count == data.MainAnimation.JointSettings.Count);
        SuitableModels.AddRange(allOgiUris);
        _selectedOgi = SuitableModels[bestFitIndex == -1 ? 0 : bestFitIndex];
    }
    
    public void ChangeTrackPosition(RangeBaseValueChangedEventArgs e)
    {
        if (_isPlaying)
        {
            return;
        }
        
        var frame = (int)e.NewValue;
        CurrentAnimationFrame = (ushort)frame;
        UpdateAnimationPlayback();
    }
    
    public void PlayAnimation()
    {
        _isPlaying = true;
        
        _renderWatch.Start();
    }
    
    public void PauseAnimation()
    {
        _runCounter = 0;
        _isPlaying = false;
        _renderWatch.Reset();
    }

    public void ExportAnimation()
    {
        // using var sfd = new SaveFileDialog();
        // sfd.Title = "Export Animation";
        // sfd.Filter = "GLB file (*.glb)|*.glb";
        // var result = sfd.ShowDialog();
        // if (result == DialogResult.OK)
        // {
        //     var path = sfd.FileName;
        //     var ogiData = AssetManager.Get().GetAssetData<OGIData>(_selectedOgi);
        //     ogiData.ExportGltf(path, AssetManager.Get().GetAssetData<AnimationData>(EditableResource));
        // }
    }

    public void UpdateAnimationPlayback()
    {
        _ogiRender ??= Document.Viewport?.GetViewportObjects()[0].Render.Children[0] as Rendering.Objects.OGI;
        if (_ogiRender == null)
        {
            return;
        }
        
        Document.Viewport?.GetRenderContext()?.QueueRenderAction(UpdateAnimationPlaybackRender);
    }

    private void UpdateAnimationPlaybackRender()
    {
        var nextFrame = (ushort)Math.Min(_currentAnimationFrame + 1, TotalFrames);
        if (_currentAnimationFrame == TotalFrames)
        {
            if (_isLooping && _isPlaying)
            {
                nextFrame = 0;
            }
            else
            {
                PauseAnimation();
            }
        }
        
        if (_isPlaying)
        {
            var interpolationLength = 1.0f / PlaybackFps;
            var dt = _renderWatch.ElapsedMilliseconds / 1000f;
            _renderWatch.Restart();
            _frameFraction += dt;
            _frameDisplacement = _frameFraction / interpolationLength;
            if (_frameFraction >= interpolationLength)
            {
                _frameDisplacement = 0;
                _frameFraction = 0;
                if (_isLooping && nextFrame == 0)
                {
                    CurrentAnimationFrame = 0;
                    nextFrame = 1;
                }
                else
                {
                    CurrentAnimationFrame = (ushort)Math.Min(CurrentAnimationFrame + 1, TotalFrames);
                    nextFrame = (ushort)Math.Min(CurrentAnimationFrame + 1, TotalFrames);
                }
                
                if (_doRunningCounter && CurrentAnimationFrame is 4 or 11)
                {
                    _runCounter++;
                }
            }
        }

        var jointIndex = 0;
        foreach (var jointSetting in _twinAnimation.JointSettings)
        {
            PerformAnimationForJoint(nextFrame, jointIndex, jointSetting);
        
            jointIndex++;
        }

        if (_twinMorphAnimation.JointSettings.Count > 0)
        {
            PerformAnimationForShapes(nextFrame, _twinMorphAnimation.JointSettings[0]);
        }
    }

    private void PerformAnimationForShapes(int nextFrame, MorphJointSettings morphSettings)
    {
        var shapesAmount = morphSettings.FacialShapesAmount;
        var weights = new float[shapesAmount];
        var transformIndex = morphSettings.TransformationIndex;
        var currentFrameTransformIndex = morphSettings.AnimationTransformationIndex;
        var nextFrameTransformIndex = morphSettings.AnimationTransformationIndex;

        for (var i = 0; i < shapesAmount; i++)
        {
            if (morphSettings.AnimationMorph[i] == Enums.TransformType.Animated)
            {
                var f1 = _twinMorphAnimation.AnimatedTransformations[CurrentAnimationFrame].Transforms[currentFrameTransformIndex++].Value;
                var f2 = _twinMorphAnimation.AnimatedTransformations[nextFrame].Transforms[nextFrameTransformIndex++].Value;
                weights[i] = MathExtension.Lerp(f1, f2, _frameDisplacement);
            }
            else
            {
                weights[i] = _twinMorphAnimation.StaticTransformations[transformIndex++].Value;
            }
        }
        
        _ogiRender!.ApplyWeightsToBlendSkin(weights);
    }

    private void PerformAnimationForJoint(int nextFrame, int jointIndex, JointSettings jointSettings)
    {
        // TODO: Use animation's sampler method to reduce code duplication
        var useAddRot = jointSettings.UseAdditionalRotation;
        var independentScaling = jointSettings.IndependentScaling;
        var transformIndex = jointSettings.TransformationIndex;
        var currentFrameTransformIndex = jointSettings.AnimationTransformationIndex;
        var nextFrameTransformIndex = jointSettings.AnimationTransformationIndex;
        var currentTranslation = new vec3();
        var nextTranslation = new vec3();
        var currentRotation = new vec3();
        var nextRotation = new vec3();
        var scale = new vec3();
        var translateXChoice = jointSettings.TranslateX;
        var translateYChoice = jointSettings.TranslateY;
        var translateZChoice = jointSettings.TranslateZ;
        var rotXChoice = jointSettings.RotateX;
        var rotYChoice = jointSettings.RotateY;
        var rotZChoice = jointSettings.RotateZ;
        var scaleXChoice = jointSettings.ScaleX;
        var scaleYChoice = jointSettings.ScaleY;
        var scaleZChoice = jointSettings.ScaleZ;

        if (translateXChoice == Enums.TransformType.Animated)
        {
            currentTranslation.x = _twinAnimation.AnimatedTransformations[CurrentAnimationFrame]
                .Transforms[currentFrameTransformIndex++].Value;
            nextTranslation.x = _twinAnimation.AnimatedTransformations[nextFrame]
                .Transforms[nextFrameTransformIndex++].Value;
        }
        else
        {
            currentTranslation.x = _twinAnimation.StaticTransformations[transformIndex].Value;
            nextTranslation.x = _twinAnimation.StaticTransformations[transformIndex++].Value;
        }
        
        if (translateYChoice == Enums.TransformType.Animated)
        {
            currentTranslation.y = _twinAnimation.AnimatedTransformations[CurrentAnimationFrame]
                .Transforms[currentFrameTransformIndex++].Value;
            nextTranslation.y = _twinAnimation.AnimatedTransformations[nextFrame]
                .Transforms[nextFrameTransformIndex++].Value;
        }
        else
        {
            currentTranslation.y = _twinAnimation.StaticTransformations[transformIndex].Value;
            nextTranslation.y = _twinAnimation.StaticTransformations[transformIndex++].Value;
        }
        
        if (translateZChoice == Enums.TransformType.Animated)
        {
            currentTranslation.z = _twinAnimation.AnimatedTransformations[CurrentAnimationFrame]
                .Transforms[currentFrameTransformIndex++].Value;
            nextTranslation.z = _twinAnimation.AnimatedTransformations[nextFrame]
                .Transforms[nextFrameTransformIndex++].Value;
        }
        else
        {
            currentTranslation.z = _twinAnimation.StaticTransformations[transformIndex].Value;
            nextTranslation.z = _twinAnimation.StaticTransformations[transformIndex++].Value;
        }

        if (rotXChoice == Enums.TransformType.Animated)
        {
            var rot1 = _twinAnimation.AnimatedTransformations[CurrentAnimationFrame].Transforms[currentFrameTransformIndex++].PureValue * 16;
            var rot2 = _twinAnimation.AnimatedTransformations[nextFrame].Transforms[nextFrameTransformIndex++].PureValue * 16;
            var rotations = GetRotationChanges(rot1, rot2);
            currentRotation.x = rotations.Item1;
            nextRotation.x = rotations.Item2;
        }
        else
        {
            var rot = _twinAnimation.StaticTransformations[transformIndex++].RotationValue;
            currentRotation.x = rot;
            nextRotation.x = rot;
        }
        
        if (rotYChoice == Enums.TransformType.Animated)
        {
            var rot1 = _twinAnimation.AnimatedTransformations[CurrentAnimationFrame].Transforms[currentFrameTransformIndex++].PureValue * 16;
            var rot2 = _twinAnimation.AnimatedTransformations[nextFrame].Transforms[nextFrameTransformIndex++].PureValue * 16;
            var rotations = GetRotationChanges(rot1, rot2);
            currentRotation.y = rotations.Item1;
            nextRotation.y = rotations.Item2;
        }
        else
        {
            var rot = _twinAnimation.StaticTransformations[transformIndex++].RotationValue;
            currentRotation.y = rot;
            nextRotation.y = rot;
        }
        
        if (rotZChoice == Enums.TransformType.Animated)
        {
            var rot1 = _twinAnimation.AnimatedTransformations[CurrentAnimationFrame].Transforms[currentFrameTransformIndex++].PureValue * 16;
            var rot2 = _twinAnimation.AnimatedTransformations[nextFrame].Transforms[nextFrameTransformIndex++].PureValue * 16;
            var rotations = GetRotationChanges(rot1, rot2);
            currentRotation.z = rotations.Item1;
            nextRotation.z = rotations.Item2;
        }
        else
        {
            var rot = _twinAnimation.StaticTransformations[transformIndex++].RotationValue;
            currentRotation.z = rot;
            nextRotation.z = rot;
        }

        if (scaleXChoice == Enums.TransformType.Animated)
        {
            var val1 = _twinAnimation.AnimatedTransformations[CurrentAnimationFrame].Transforms[currentFrameTransformIndex++].Value;
            var val2 = _twinAnimation.AnimatedTransformations[nextFrame].Transforms[nextFrameTransformIndex++].Value;
            scale.x = MathExtension.Lerp(val1, val2, _frameDisplacement);
        }
        else
        {
            scale.x = _twinAnimation.StaticTransformations[transformIndex++].Value;
        }
        
        if (scaleYChoice == Enums.TransformType.Animated)
        {
            var val1 = _twinAnimation.AnimatedTransformations[CurrentAnimationFrame].Transforms[currentFrameTransformIndex++].Value;
            var val2 = _twinAnimation.AnimatedTransformations[nextFrame].Transforms[nextFrameTransformIndex++].Value;
            scale.y = MathExtension.Lerp(val1, val2, _frameDisplacement);
        }
        else
        {
            scale.y = _twinAnimation.StaticTransformations[transformIndex++].Value;
        }
        
        if (scaleZChoice == Enums.TransformType.Animated)
        {
            var val1 = _twinAnimation.AnimatedTransformations[CurrentAnimationFrame].Transforms[currentFrameTransformIndex].Value;
            var val2 = _twinAnimation.AnimatedTransformations[nextFrame].Transforms[nextFrameTransformIndex].Value;
            scale.z = MathExtension.Lerp(val1, val2, _frameDisplacement);
        }
        else
        {
            scale.z = _twinAnimation.StaticTransformations[transformIndex].Value;
        }
        
        var resultTranslation = MathExtension.Lerp(currentTranslation, nextTranslation, _frameDisplacement);
        var quat1 = new quat(currentRotation);
        var quat2 = new quat(nextRotation);
        var lerpedQuat = GlmExtensions.SLerpSafe(quat1, quat2, _frameDisplacement);

        var resRotationQuat = lerpedQuat;
        if (useAddRot)
        {
            var additionalRotation = AssetManager.Get().GetAssetData<OGIData>(_selectedOgi).Joints[jointIndex].AdditionalAnimationRotation;
            var addRotQuat = new quat(additionalRotation.X, additionalRotation.Y, additionalRotation.Z, additionalRotation.W);
            resRotationQuat = addRotQuat * lerpedQuat;
        }
        
        _ogiRender?.SetInheritScaleForJoint(jointIndex, !independentScaling);
        _ogiRender?.ApplyTransformToJoint(jointIndex, resultTranslation, scale, resRotationQuat);
    }

    private (float, float) GetRotationChanges(int rot1, int rot2)
    {
        var diff = rot1 - rot2;
        if (diff < -0x8000)
        {
            rot1 += 0x10000;
        }
        if (diff > 0x8000)
        {
            rot1 -= 0x10000;
        }

        var rot1Rad = rot1 / (float)(ushort.MaxValue + 1) * (float)(Math.PI * 2);
        var rot2Rad = rot2 / (float)(ushort.MaxValue + 1) * (float)(Math.PI * 2);
        return (rot1Rad, rot2Rad);
    }

    private void InitAnimationScene()
    {
        // AnimationScene.SceneInitializer = (renderer, scene) =>
        // {
        //     _context = renderer.GetRenderContext();
        //     _ogiRender = new Rendering.Objects.OGI(_context, _context.SkeletonManager, _context.MeshService, AssetManager.Get().GetAssetData<OGIData>(_selectedOgi));
        //     _scene = scene;
        //     _scene.AddChild(_ogiRender);
        //     InitImgui(renderer);
        //     
        //     UpdateAnimationPlayback();
        // };
    }

    private void InitImgui(Renderer renderer)
    {
        if (_imguiInitialized)
        {
            return;
        }
        
        renderer.RenderImgui += () =>
        {
            ImGui.Begin("Animation Info");
            ImGui.SetWindowPos(new Vector2(5, 5), ImGuiCond.FirstUseEver);
            ImGui.Text($"Total Frames: {TotalFrames + 1}");
            if (_doRunningCounter)
            {
                ImGui.Text($"Steps: {_runCounter}");
                ImGui.Text($"Estimated distance ran: {(_runCounter * 0.3f):F2} meters");
            }
            ImGui.End();
        };

        _imguiInitialized = true;
    }

    public bool Loop
    {
        get => _isLooping;
        set
        {
            if (_isLooping != value)
            {
                _isLooping = value;
                this.RaisePropertyChanged();
            }
        }
    }

    public short PlaybackFps
    {
        get => _playbackFps;
        set
        {
            if (value != _playbackFps)
            {
                _playbackFps = value;
                this.RaisePropertyChanged();
            }
        }
    }

    public ushort CurrentAnimationFrame
    {
        get => _currentAnimationFrame;
        set
        {
            if (value != _currentAnimationFrame)
            {
                _currentAnimationFrame = value;
                Dispatcher.UIThread.Invoke(() => this.RaisePropertyChanged());
            }
        }
    }
    
    public BindableCollection<LabURI> SuitableModels { get; } = new();

    public LabURI SelectedOgi
    {
        get => _selectedOgi;
        set
        {
            if (value != _selectedOgi)
            {
                _selectedOgi = value;
                // AnimationScene.ResetScene();
                Document.Viewport?.GetRenderContext()?.QueueRenderAction(() =>
                {
                    var context = Document.Viewport?.GetRenderContext();
                    if (context == null)
                    {
                        return;
                    }
                    var scene = Document.Viewport!.GetViewportObjects()[0].Render;
                    if (_ogiRender != null)
                    {
                        scene.RemoveChild(_ogiRender);
                    }
                    _ogiRender = new Rendering.Objects.OGI(context, context.SkeletonManager, context.MeshService, AssetManager.Get().GetAssetData<OGIData>(_selectedOgi));
                    scene.AddChild(_ogiRender);
                });
                this.RaisePropertyChanged();
            }
        }
    }
    
    public int TotalFrames => _totalFrames - 1;
}