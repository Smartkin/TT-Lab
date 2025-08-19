using GlmSharp;
using System;
using System.Diagnostics;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Rendering.Scene;
using TT_Lab.ViewModels.Editors.Instance;

namespace TT_Lab.Rendering.Objects;

public class Position : EditableObject
{
    private int _layId;
    private PositionData _data;
    private Billboard _billboard;

    public Position(RenderContext context, string name, Billboard billboard, int layId, PositionData data) : base(context, name)
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