using GlmSharp;
using TT_Lab.AssetData.Instance;
using TT_Lab.Rendering.Scene;

namespace TT_Lab.Rendering.Objects;

public class AiPosition : EditableObject
{
    private int _layId;
    private AiPositionData _data;
    private Billboard _billboard;

    public AiPosition(RenderContext context, string name, Billboard billboard, int layId, AiPositionData data) : base(context, name)
    {
        _layId = layId;
        _billboard = billboard;
        var position = new vec3(data.Coords.X, data.Coords.Y, data.Coords.Z);
        _data = data;
        
        _billboard.SetPosition(position);
    }

    protected override void InitSceneTransform()
    {
        
    }
}