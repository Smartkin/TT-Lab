using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using GlmSharp;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Attributes;
using TT_Lab.Extensions;
using TT_Lab.Rendering.Objects;
using TT_Lab.Util;
using TT_Lab.ViewModels;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using TT_Lab.ViewModels.Interfaces;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;

namespace TT_Lab.AssetData.Instance;

public class PositionData : AbstractAssetData
{
    public PositionData(IAsset asset) : base(asset)
    {
        Coords = new Vector3(0, 0, 0);
    }

    public PositionData(IAsset asset, ITwinPosition position) : base(asset)
    {
        SetTwinItem(position);
    }

    [JsonProperty(Required = Required.Always)]
    [Editable]
    public Vector3 Coords { get; set; }

    protected override void Dispose(Boolean disposing)
    {
        return;
    }

    public override void Import(LabURI package, String? variant, Int32? layoutId)
    {
        var position = GetTwinItem<ITwinPosition>();
        Coords = new Vector3(position.Position.X, position.Position.Y, position.Position.Z);
    }

    public override ITwinItem Export(ITwinItemFactory factory)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        var coordsVec = new Vector4(Coords.X, Coords.Y, Coords.Z, 1.0f);
        coordsVec.Write(writer);

        writer.Flush();
        ms.Position = 0;
        return factory.GeneratePosition(ms);
    }

    public override List<ViewportObject> GetViewportObjects(ViewportContext viewportContext, PropertyNode property)
    {
        var visual = viewportContext.EditingContext.CreatePositionBillboard();
        var color = System.Drawing.Color.FromKnownColor(System.Drawing.KnownColor.Blue);
        visual.Diffuse = new vec4(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f,  color.A / 255.0f * 0.5f);
        
        var size = vec3.Ones;
        var offset = -vec3.Ones * 0.5f;
        var editableObject = new EditableObject(viewportContext.RenderContext, visual, Owner.FullDataPath, offset, size);
        color = System.Drawing.Color.FromKnownColor(System.Drawing.KnownColor.LightBlue);
        editableObject.SelectedColor = new vec4(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f,  color.A / 255.0f * 0.25f);
        editableObject.UnselectedColor = visual.Diffuse;
        editableObject.SetPosition(Coords.ToGlm());
        
        return [new ViewportObject(editableObject, property.Path, property)
        {
            Position = property.Find($"[data].AssetData.{nameof(Coords)}"),
        }];
    }
}