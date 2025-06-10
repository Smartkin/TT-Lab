using System.Drawing;
using GlmSharp;
using org.ogre;
using TT_Lab.AssetData.Instance;

namespace TT_Lab.Rendering.Objects;

public class Camera : EditableObject
{
    private readonly Trigger _cameraTrigger;
    
    public Camera(OgreWindow window, string name, SceneNode parentNode, SceneManager sceneManager, Billboard cameraBillboard, CameraData cameraData, vec3 size) : base(window, parentNode, name, size)
    {
        _cameraTrigger = new Trigger(window, $"CameraTrigger_{name}", SceneNode, sceneManager, cameraBillboard, cameraData.Trigger, size, KnownColor.Blue);
        _cameraTrigger.Init();
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
        _cameraTrigger.GetSceneNode().resetToInitialState();
    }

    protected override void UpdateSceneTransform()
    {
        base.UpdateSceneTransform();
        _cameraTrigger.SetPos(Pos);
        _cameraTrigger.SetRot(Rot);
        _cameraTrigger.SetScale(Scl);
        _cameraTrigger.GetSceneNode().resetToInitialState();
    }
}