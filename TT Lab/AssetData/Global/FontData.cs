using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Global;
using TT_Lab.Attributes;
using TT_Lab.Util;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.Descs;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace TT_Lab.AssetData.Global;

[ReferencesAssets]
public class FontData : AbstractAssetData
{
    // The game's font has 3 slots for its pages' materials and textures (ReadFont)
    public const int MaxPages = 3;

    public FontData(IAsset asset) : base(asset)
    {
        FontPages = [];
        CharacterData = [];
    }

    public FontData(IAsset asset, ITwinPSF psf) : this(asset)
    {
        SetTwinItem(psf);
    }

    [JsonProperty(Required = Required.Always)]
    [Editable(Caption = "Font Pages", Hint = "The game's font keeps 3 pages at most")]
    [EditorParam(DocumentCollectionViewModel.MaxCount, MaxPages)]
    public List<LabURI> FontPages { get; set; }
    
    /// <summary>
    /// A box per character from the space on: its page (0 or 1 the first, 2 the second...), its left column and the row under its
    /// bottom one (the pages are upside down), its size. Edited on the pages with <see cref="FontEditorDesc"/>
    /// </summary>
    [JsonProperty(Required = Required.Always)]
    [Editable(Caption = "Characters", EditorDescType = typeof(FontEditorDesc), EditorOrientation = Avalonia.Controls.Dock.Top)]
    public List<VectorCharacterData> CharacterData { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable(Caption = "Space Identifier")]
    public Int32 SpaceIdentifier { get; set; }

    public override ITwinItem Export(ITwinItemFactory factory)
    {
        CheckCount("pages", FontPages.Count, MaxPages);
        var assetManager = AssetManager.Get();
        var pages = new List<ITwinPTC>();
        foreach (var page in FontPages)
        {
            pages.Add((ITwinPTC)assetManager.GetAssetData<PTCData>(page).Export(factory));
        }

        return factory.GenerateFont(pages, CharacterData, SpaceIdentifier);
    }

    public override void Import(LabURI package, String? variant, Int32? layoutId)
    {
        var psf = GetTwinItem<ITwinPSF>();
        var psfIndex = 0;
        var owner = (GlobalAsset)Owner;
        foreach (var ptc in psf.FontPages)
        {
            var asset = new PTC(package, true, $"{psf.GetName()}_{variant}_page_{psfIndex++}", $"{psf.GetName()}_page_{psfIndex++}", ptc)
            {
                GlobalPath = $"{owner.GlobalPath}/{psf.GetName()}/{variant!}"
            };
            asset.RegenerateLinks();
            AssetManager.Get().AddAssetToImport(asset);
            FontPages.Add(asset.URI);
        }

        CharacterData = CloneUtils.CloneList(psf.CharacterData);
        SpaceIdentifier = psf.SpaceIdentifier;
    }

    protected override void Dispose(Boolean disposing)
    {
        FontPages.Clear();
        CharacterData.Clear();
    }
}