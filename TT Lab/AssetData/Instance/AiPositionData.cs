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

public class AiPositionData : AbstractAssetData
{
    public AiPositionData(IAsset asset) : base(asset)
    {
        Coords = new Vector3(0, 0, 0);
    }

    public AiPositionData(IAsset asset, ITwinAIPosition aiPosition) : this(asset)
    {
        SetTwinItem(aiPosition);
    }

    [JsonProperty(Required = Required.Always)]
    [Editable]
    public Vector3 Coords { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable]
    public float FloatArg { get; set; }
        
    [JsonProperty(Required = Required.Always)]
    [Editable]
    public UInt16 Arg { get; set; }

    protected override void Dispose(Boolean disposing)
    {
        return;
    }

    public override void Import(LabURI package, String? variant, Int32? layoutId)
    {
        var aiPosition = GetTwinItem<ITwinAIPosition>();
        Coords = new Vector3(aiPosition.Position.X, aiPosition.Position.Y, aiPosition.Position.Z);
        FloatArg = aiPosition.Position.W;
        Arg = aiPosition.UnkShort;
    }

    public override ITwinItem Export(ITwinItemFactory factory)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        Coords.Write(writer);
        writer.Write(FloatArg);
        writer.Write(Arg);

        writer.Flush();
        ms.Position = 0;
        return factory.GenerateAIPosition(ms);
    }

    public override List<ViewportObject> GetViewportObjects(ViewportContext viewportContext, PropertyNode property)
    {
        var visual = viewportContext.EditingContext.CreateAiPositionBillboard();
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