using System;
using System.Drawing;
using GlmSharp;
using TT_Lab.AssetData.Instance;
using TT_Lab.Extensions;
using TT_Lab.Rendering.Scene;
using Twinsanity.TwinsanityInterchange.Common.CameraSubtypes;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;

namespace TT_Lab.Rendering.Objects;

public class Camera : EditableObject
{
    private readonly CameraData _cameraData;
    private readonly Trigger _cameraTrigger;
    
    public Camera(RenderContext context, string name, Renderable parentNode, Billboard cameraBillboard, CameraData cameraData, vec3 size) : base(context, parentNode, name, size)
    {
        _cameraData = cameraData;
        _cameraTrigger = new Trigger(context, $"CameraTrigger_{name}", this, cameraBillboard, cameraData.Trigger, size, KnownColor.Blue);
        _cameraTrigger.Init();
    }

    public override (String, Int32)[] GetPriorityPasses()
    {
        return [("ColorOnly", 10)];
    }

    protected override void RenderSelf(float delta)
    {
        base.RenderSelf(delta);

        var primitiveRender = Context.GetPrimitiveRenderer();
        primitiveRender.DrawSphere(Pos, 1.0f, new vec4(0.0f, 0.0f, 1.0f, 0.3f));
        if (_cameraData.MainCamera1 != null)
        {
            RenderMainCamera(_cameraData.MainCamera1);
        }

        if (_cameraData.MainCamera2 != null)
        {
            RenderMainCamera(_cameraData.MainCamera2);
        }
    }

    private void RenderMainCamera(CameraSubBase mainCamera)
    {
        var primitiveRender = Context.GetPrimitiveRenderer();
        switch (mainCamera.GetCameraType())
        {
            case ITwinCamera.CameraType.Null:
                break;
            case ITwinCamera.CameraType.BossCamera:
                break;
            case ITwinCamera.CameraType.CameraPoint:
                {
                    var camera = (CameraPoint)mainCamera;
                    primitiveRender.DrawSphere(camera.Point.ToGlm().xyz, 0.5f, new vec4(0, 0, 1.0f, 0.8f));
                }
                break;
            case ITwinCamera.CameraType.CameraLine:
                {
                    var camera = (CameraLine)mainCamera;
                    primitiveRender.DrawSphere(camera.LineStart.ToGlm().xyz, 0.5f, new vec4(0, 0, 1.0f, 0.8f));
                    primitiveRender.DrawSphere(camera.LineEnd.ToGlm().xyz, 0.5f, new vec4(0, 0, 1.0f, 0.8f));
                }
                break;
            case ITwinCamera.CameraType.CameraPath:
                {
                    var camera = (CameraPath)mainCamera;
                    foreach (var cameraPath in camera.PathPoints)
                    {
                        primitiveRender.DrawSphere(cameraPath.ToGlm().xyz, 0.5f, new vec4(0, 0, 1.0f, 0.8f));
                    }
                }
                break;
            case ITwinCamera.CameraType.CameraSpline:
                {
                    var camera = (CameraSpline)mainCamera;
                    foreach (var cameraPath in camera.PathPoints)
                    {
                        primitiveRender.DrawSphere(cameraPath.ToGlm().xyz, 0.5f, new vec4(0, 0, 1.0f, 0.8f));
                    }
                }
                break;
            case ITwinCamera.CameraType.CameraSub1C09:
                break;
            case ITwinCamera.CameraType.CameraPoint2:
                {
                    var camera = (CameraPoint2)mainCamera;
                    primitiveRender.DrawSphere(camera.Point.ToGlm().xyz, 0.5f, new vec4(0, 0, 1.0f, 0.8f));
                }
                break;
            case ITwinCamera.CameraType.CameraSub1C0C:
                break;
            case ITwinCamera.CameraType.CameraLine2:
                {
                    var camera = (CameraLine2)mainCamera;
                    primitiveRender.DrawSphere(camera.LineStart.ToGlm().xyz, 0.5f, new vec4(0, 0, 1.0f, 0.8f));
                    primitiveRender.DrawSphere(camera.LineEnd.ToGlm().xyz, 0.5f, new vec4(0, 0, 1.0f, 0.8f));
                }
                break;
            case ITwinCamera.CameraType.CameraZone:
                break;
        }
    }

    public override void Select()
    {
        _cameraTrigger.Select();
        
        base.Select();
    }

    public override void Deselect()
    {
        _cameraTrigger.Deselect();
        
        base.Deselect();
    }

    protected override void InitSceneTransform()
    {
        Pos = _cameraTrigger.GetPosition();
        Rot = _cameraTrigger.GetRotation();
        Scl = _cameraTrigger.GetScale();
    }

    protected override void UpdateSceneTransform()
    {
        base.UpdateSceneTransform();
        _cameraTrigger.SetPosition(Pos);
        _cameraTrigger.Rotate(Rot);
        _cameraTrigger.Scale(Scl);
    }
}