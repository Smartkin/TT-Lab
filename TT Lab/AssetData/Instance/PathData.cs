using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
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

public class PathData : AbstractAssetData
{
    private readonly object _parametersLock = new();
    private vec3[] _parametersPoints = [];
    private float? _stepLength;

    public PathData(IAsset asset) : base(asset)
    {
        Points = [];
        ArcLengths = [];
        InverseSteps = [];
    }

    public PathData(IAsset asset, ITwinPath path) : this(asset)
    {
        SetTwinItem(path);
    }

    [JsonProperty(Required = Required.Always)]
    [Editable]
    public List<Vector3> Points { get; set; }
    
    /// <summary>
    /// Every segment's arc length from the start, made from the points (<see cref="PathParameters"/>), the game's are kept until the
    /// points change
    /// </summary>
    [JsonProperty(Required = Required.Always)]
    public List<Single> ArcLengths { get; set; }

    /// <summary>
    /// 1 over the steps every segment takes, made with the arc lengths
    /// </summary>
    [JsonProperty(Required = Required.Always)]
    public List<Single> InverseSteps { get; set; }

    // Points get edited in place by the inspector and the viewport, so the parameters catch up whenever they're saved or built
    [OnSerializing]
    private void OnSerializing(StreamingContext context) => UpdateParameters();

    [OnDeserialized]
    private void OnDeserialized(StreamingContext context) => KeepParameters();

    internal void UpdateParameters()
    {
        lock (_parametersLock)
        {
            var points = Points.Select(point => point.ToGlm()).ToArray();
            if (points.AsSpan().SequenceEqual(_parametersPoints))
            {
                return;
            }

            _stepLength ??= PathParameters.FindStepLength(ArcLengths, InverseSteps, _parametersPoints.Length - 3) ?? PathParameters.DefaultStepLength;
            (ArcLengths, InverseSteps) = PathParameters.Create(points, _stepLength.Value);
            _parametersPoints = points;
        }
    }

    private void KeepParameters()
    {
        lock (_parametersLock)
        {
            _parametersPoints = Points.Select(point => point.ToGlm()).ToArray();
            _stepLength = null;
        }
    }

    protected override void Dispose(Boolean disposing)
    {
        Points.Clear();
        ArcLengths.Clear();
        InverseSteps.Clear();
    }

    public override void Import(LabURI package, String? variant, Int32? layoutId)
    {
        var path = GetTwinItem<ITwinPath>();
        Points = [];
        foreach (var point in path.PointList)
        {
            Points.Add(new Vector3(point.X, point.Y, point.Z));
        }
        ArcLengths = [..path.ArcLengths];
        InverseSteps = [..path.InverseSteps];
        KeepParameters();
    }

    public override ITwinItem Export(ITwinItemFactory factory)
    {
        UpdateParameters();
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write(Points.Count);
        foreach (var pos in Points.Select(point => new Vector4(point.X, point.Y, point.Z, 1.0f)))
        {
            pos.Write(writer);
        }

        writer.Write(ArcLengths.Count);
        TwinPathParameters.Write(writer, ArcLengths, InverseSteps);

        writer.Flush();
        ms.Position = 0;
        return factory.GeneratePath(ms);
    }

    public override List<ViewportObject> GetViewportObjects(ViewportContext viewportContext, PropertyNode property)
    {
        var viewportObjects = new List<ViewportObject>();
        var pointIdx = 0;
        foreach (var point in Points)
        {
            var visual = viewportContext.EditingContext.CreatePathBillboard();
            var color = System.Drawing.Color.FromKnownColor(System.Drawing.KnownColor.Blue);
            visual.Diffuse = new vec4(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f,  color.A / 255.0f * 0.5f);
        
            // Same size as the billboard
            var size = vec3.Ones * 2.0f;
            var offset = -vec3.Ones;
            var editableObject = new EditableObject(viewportContext.RenderContext, visual, $"{Owner.FullDataPath}{pointIdx}", offset, size);
            color = System.Drawing.Color.FromKnownColor(System.Drawing.KnownColor.LightBlue);
            editableObject.SelectedColor = new vec4(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f,  color.A / 255.0f * 0.25f);
            editableObject.UnselectedColor = visual.Diffuse;
            editableObject.SetPosition(point.ToGlm());

            var pointProperty = property.Find($"[data].AssetData.{nameof(Points)}[{pointIdx++}]")!;
            viewportObjects.Add(new ViewportObject(editableObject, pointProperty.Path, property)
            {
                Position = pointProperty,
                Category = ViewportObjectCategory.Paths,
                InspectorFocus = pointProperty,
                DuplicatedElement = pointProperty,
            });
        }

        var pointsProperty = property.Find($"[data].AssetData.{nameof(Points)}");
        if (pointsProperty == null)
        {
            return viewportObjects;
        }

        var spline = new PolylineVisual(viewportContext.RenderContext, $"{Owner.FullDataPath}_SPLINE");
        spline.SetGeometry(PathGeometry.CreatePath(Points));
        var pointCount = Points.Count;
        viewportObjects.Add(new ViewportObject(spline, $"PATH_{property.Path}", property)
        {
            Category = ViewportObjectCategory.Paths,
            RenderDependencies = [pointsProperty],
            Refresh = () =>
            {
                // Every point is an object of its own, they have to be made again when points get added or removed
                if (Points.Count != pointCount)
                {
                    return false;
                }

                spline.SetGeometry(PathGeometry.CreatePath(Points));
                return true;
            }
        });
        
        return viewportObjects;
    }
}