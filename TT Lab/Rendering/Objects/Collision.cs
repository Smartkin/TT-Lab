using GlmSharp;
using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Linq;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets.Instance;
using TT_Lab.Rendering.Buffers;
using TT_Lab.Util;

namespace TT_Lab.Rendering.Objects;

public class Collision(RenderContext context, List<ModelBuffer> models) : Mesh(context, models)
{
    public override Mesh Clone()
    {
        var collision = new Collision(context, models);
        return collision;
    }
}