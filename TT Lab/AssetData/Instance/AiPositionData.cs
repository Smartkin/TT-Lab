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
    
    // The game's nearest point search goes by the coordinates alone; the W of the position is only read by the
    // NearestPointEdgeDistanceSquared condition (FUN_00225be0), which takes it off the distance to the point: its radius
    [JsonProperty(Required = Required.Always)]
    [Editable(Hint = "How far the position reaches: scripts' NearestPointEdgeDistanceSquared measures to its edge, the nearest position itself is picked by its coordinates")]
    public float Radius { get; set; }
        
    [JsonProperty(Required = Required.Always)]
    [Editable]
    public UInt16 Flags { get; set; }

    protected override void Dispose(Boolean disposing)
    {
        return;
    }

    public override void Import(LabURI package, String? variant, Int32? layoutId)
    {
        var aiPosition = GetTwinItem<ITwinAIPosition>();
        Coords = new Vector3(aiPosition.Position.X, aiPosition.Position.Y, aiPosition.Position.Z);
        Radius = aiPosition.Position.W;
        Flags = aiPosition.Flags;
    }

    public override ITwinItem Export(ITwinItemFactory factory)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        Coords.Write(writer);
        writer.Write(Radius);
        writer.Write(Flags);

        writer.Flush();
        ms.Position = 0;
        return factory.GenerateAIPosition(ms);
    }

    public override List<ViewportObject> GetViewportObjects(ViewportContext viewportContext, PropertyNode property)
    {
        var visual = viewportContext.EditingContext.CreateAiPositionBillboard();
        var color = System.Drawing.Color.FromKnownColor(System.Drawing.KnownColor.Blue);
        visual.Diffuse = new vec4(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f,  color.A / 255.0f * 0.5f);
        
        // Same size as the billboard
        var size = vec3.Ones * 2.0f;
        var offset = -vec3.Ones;
        var editableObject = new EditableObject(viewportContext.RenderContext, visual, Owner.FullDataPath, offset, size);
        color = System.Drawing.Color.FromKnownColor(System.Drawing.KnownColor.LightBlue);
        editableObject.SelectedColor = new vec4(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f,  color.A / 255.0f * 0.25f);
        editableObject.UnselectedColor = visual.Diffuse;
        editableObject.SetPosition(Coords.ToGlm());

        var coords = property.Find($"[data].AssetData.{nameof(Coords)}");
        var radius = property.Find($"[data].AssetData.{nameof(Radius)}");
        var objects = new List<ViewportObject>
        {
            new(editableObject, property.Path, property)
            {
                Position = coords,
                Category = ViewportObjectCategory.AiPositions,
            }
        };
        if (coords == null || radius == null)
        {
            return objects;
        }

        // The radius is dragged by a handle on its ring, whose distance from the position it takes
        var converter = new AiPositionRadiusHandle(coords);
        var handle = new EditableObject(viewportContext.RenderContext, null, $"{Owner.FullDataPath}_RADIUS", -vec3.Ones * RadiusHandleSize, vec3.Ones * RadiusHandleSize * 2.0f);
        handle.SetPosition(converter.ToPosition(Radius));
        var ring = new AiPositionRadiusVisual(viewportContext.RenderContext, editableObject, handle, Radius);
        objects[0] = objects[0] with
        {
            RenderDependencies = [radius],
            Refresh = () =>
            {
                ring.Radius = radius.GetValue<float>();
                return true;
            },
        };
        objects.Add(new ViewportObject(handle, $"{property.Path}_RADIUS", property)
        {
            Position = radius,
            PositionConverter = converter,
            Category = ViewportObjectCategory.AiPositions,
            InspectorFocus = radius,
            RenderDependencies = [coords],
            Refresh = () =>
            {
                handle.SetPosition(converter.ToPosition(radius.GetValue()));
                return true;
            },
        });
        return objects;
    }

    // Half the size of the box the radius handle is picked by
    private const float RadiusHandleSize = 0.2f;
}

/// <summary>
/// The handle of an AI position's radius stands on the +X side of its ring, and the radius is how far from the position it's dragged
/// </summary>
internal sealed class AiPositionRadiusHandle(PropertyNode coords) : IPositionConverter
{
    private vec3 Center => coords.GetValue() is Vector3 center ? center.ToGlm() : vec3.Zero;

    public vec3 ToPosition(object? data) => AiPositionRadiusVisual.HandlePosition(Center, data is float radius ? radius : 0.0f);

    public object ToData(vec3 position) => (position - Center).Length;
}