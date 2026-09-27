using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Instance;
using TT_Lab.Attributes;
using TT_Lab.Rendering.Objects;
using AiPosition = TT_Lab.Assets.Instance.AiPosition;
using TT_Lab.ViewModels;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using TT_Lab.ViewModels.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;

namespace TT_Lab.AssetData.Instance;

[ReferencesAssets]
public class AiPathData : AbstractAssetData
{
    public AiPathData(IAsset asset) : base(asset)
    {
        PathBegin = LabURI.Empty;
        PathEnd = LabURI.Empty;
        Args = new UInt16[3];
    }

    public AiPathData(IAsset asset, ITwinAIPath aiPath) : this(asset)
    {
        SetTwinItem(aiPath);
    }

    [JsonProperty(Required = Required.Always)]
    [Editable]
    public LabURI PathBegin { get; set; }
        
    [JsonProperty(Required = Required.Always)]
    [Editable]
    public LabURI PathEnd { get; set; }
        
    [JsonProperty(Required = Required.Always)]
    [Editable]
    [EditorParam(DocumentCollectionViewModel.IsCollectionEditable, false)]
    public UInt16[] Args { get; set; }

    protected override void Dispose(Boolean disposing)
    {
        return;
    }

    public override void Import(LabURI package, String? variant, Int32? layoutId)
    {
        var aiPath = GetTwinItem<ITwinAIPath>();
        PathBegin = AssetManager.Get().GetUriByTwinId<AiPosition>(Owner, aiPath.Args[0], layoutId);
        PathEnd = AssetManager.Get().GetUriByTwinId<AiPosition>(Owner, aiPath.Args[1], layoutId);
        Args = [aiPath.Args[2], aiPath.Args[3], aiPath.Args[4]];
    }

    public override ITwinItem Export(ITwinItemFactory factory)
    {
        var assetManager = AssetManager.Get();
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write((UInt16)assetManager.GetAsset(PathBegin).ExportTwinID);
        writer.Write((UInt16)assetManager.GetAsset(PathEnd).ExportTwinID);
        foreach (var arg in Args)
        {
            writer.Write(arg);
        }

        writer.Flush();
        ms.Position = 0;
        return factory.GenerateAIPath(ms);
    }

    public override List<ViewportObject> GetViewportObjects(ViewportContext viewportContext, PropertyNode property)
    {
        var visual = new AiPathVisual(viewportContext.RenderContext, $"{Owner.FullDataPath}_AI_PATH");
        UpdateEnds(visual);
        var dependencies = new[] { property.Find($"[data].AssetData.{nameof(PathBegin)}"), property.Find($"[data].AssetData.{nameof(PathEnd)}") }
            .OfType<PropertyNode>().ToList();
        return [new ViewportObject(visual, $"AI_PATH_{property.Path}", property)
        {
            Category = ViewportObjectCategory.AiPaths,
            RenderDependencies = dependencies,
            Refresh = () =>
            {
                UpdateEnds(visual);
                return true;
            }
        }];
    }

    private void UpdateEnds(AiPathVisual visual)
    {
        visual.SetEnds(GetPosition(PathBegin), GetPosition(PathEnd));
    }

    private static AiPositionData? GetPosition(LabURI uri)
    {
        var assetManager = AssetManager.Get();
        if (uri == LabURI.Empty || !assetManager.DoesAssetExist(uri))
        {
            return null;
        }

        return assetManager.GetAssetData<AiPositionData>(uri);
    }
}