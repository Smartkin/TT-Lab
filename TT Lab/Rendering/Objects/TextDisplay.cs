using System;
using org.ogre;
using TT_Lab.Extensions;
using TT_Lab.Rendering.Objects.SceneInstances;

namespace TT_Lab.Rendering.Objects;

public class TextDisplay : IDisposable
{
    private static Overlay? _overlay = null;
    
    private readonly SceneInstance _sceneInstance;
    private readonly org.ogre.Camera _camera;
    private readonly OverlayContainer _overlayContainer;
    private readonly OverlayElement _overlayText;
    
    private bool _enabled = false;
    private string _text = "";
    
    public TextDisplay(SceneManager sceneManager, SceneInstance movableObject, org.ogre.Camera camera)
    {
        _sceneInstance = movableObject;
        _camera = camera;

        var overlayManager = OverlayManager.getSingleton();

        if (_overlay == null)
        {
            _overlay ??= overlayManager.create($"BaseOverlay");
            sceneManager.addRenderQueueListener(OverlaySystem.getSingleton());
            _overlay.setZOrder(200);
            _overlay.show();
            
            var font = FontManager.getSingleton().getByName("InterDisplay");
            font.load();
            // HACK: This allows to inject custom material to render text instead of OGRE's default generated one
            font._setMaterial(org.ogre.MaterialManager.getSingleton().getByName("TextOutlineMaterial"));
            font.getMaterial().getTechnique(0).getPass(0).getTextureUnitState(0).setTextureName("InterDisplayTexture");
        }

        _overlayContainer = overlayManager.createOverlayElement("Panel", $"container1_{GetHashCode()}").castOverlayContainer();
        _overlayContainer.castOverlayContainer().setWidth(1.0f);
        _overlayContainer.castOverlayContainer().setHeight(1.0f);
        _overlay.add2D(_overlayContainer);

        _overlayText = overlayManager.createOverlayElement("TextArea", $"textDisplayElement_{GetHashCode()}");
        _overlayText.setDimensions(1.0f, 1.0f);
        _overlayText.setPosition(0.0f, 0.0f);
        _overlayText.setParameter("font_name", "InterDisplay");
        _overlayText.setParameter("horz_align", "center");
        _overlayText.setColour(ColourValue.White);
        _overlayText.setCaption("IF YOU SEE THIS TEXT THEN SOMETHING WENT WRONG");
        
        _overlayContainer.addChild(_overlayText);
        _overlayContainer.setEnabled(false);
    }

    public void Enable(bool enabled)
    {
        _enabled = enabled;
        if (enabled)
        {
            _overlayContainer.show();
        }
        else
        {
            _overlayContainer.hide();
        }
    }

    public void SetFont(string fontName)
    {
        _overlayText.setParameter("font_name", fontName);
    }

    public void SetTextColour(ColourValue colour)
    {
        _overlayText.setColour(colour);
    }

    public void SetText(string text)
    {
        _text = text;
        _overlayText.setCaption(text);
    }

    public void Update()
    {
        if (!_enabled)
        {
            return;
        }

        var instancePosition = _sceneInstance.GetPosition();
        var topCenter = new Vector4(instancePosition.x, instancePosition.y + _sceneInstance.GetSize().y + 0.25f, instancePosition.z, 1.0f);
        var viewPoint = _camera.getViewMatrix().__mul__(topCenter);
        var isBehindCamera = viewPoint.z > 0.0;
        topCenter = _camera.getProjectionMatrix().__mul__(viewPoint);
        topCenter = new Vector4(topCenter.x / topCenter.w, topCenter.y / topCenter.w, topCenter.z / topCenter.w, 1.0f);
        
        if (isBehindCamera)
        {
            _overlayContainer.setPosition(-1000.0f, -1000.0f);
        }
        else
        {
            var distanceToCamera = System.Math.Max((OgreExtensions.FromOgre(_camera.getRealPosition()) - instancePosition).Length, 1.0f);
            var ratio = System.Math.Clamp(1.0f / distanceToCamera, 0.001f, 0.03f);
            _overlayContainer.setPosition(topCenter.x * 0.5f, (1.0f - topCenter.y) * 0.5f);
            _overlayContainer.setDimensions(1.0f, 1.0f);
            _overlayText.setDimensions(ratio, ratio);
            _overlayText.castTextAreaOverlayElement().setAlignment(TextAreaOverlayElement.Alignment.Center);
            _overlayText.castTextAreaOverlayElement().setCharHeight(ratio);
        }
        
    }

    public void Dispose()
    {
        if (OverlayElement.getCPtr(_overlayText).Handle == IntPtr.Zero)
        {
            return;
        }
        
        _overlayContainer.removeChild(_overlayText.getName());
        _overlay!.remove2D(_overlayContainer);
        _overlayContainer.Dispose();
        _overlayText.Dispose();
    }
}