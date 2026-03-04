using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using TT_Lab.AssetData.Code;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Global;
using TT_Lab.Attributes;
using TT_Lab.Attributes.EditorParamWrappers;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;

namespace TT_Lab.AssetData.Global;

[ReferencesAssets]
public class UiSoundLibraryData : AbstractAssetData
{

    public UiSoundLibraryData(IAsset asset) : base(asset) { }

    public UiSoundLibraryData(IAsset asset, ITwinSection section) : this(asset)
    {
        SetTwinItem(section);
    }

    [JsonProperty(Required = Required.Always)]
    [Editable]
    public List<LabURI> UiSounds { get; set; } = new();

    public override ITwinItem Export(ITwinItemFactory factory)
    {
        var assetManager = AssetManager.Get();
        var sounds = new List<ITwinSound>();

        var frontend = factory.GenerateFrontend(sounds);
            
        foreach (var sound in UiSounds)
        {
            var soundAsset = assetManager.GetAsset(sound);
            soundAsset.ResolveChunkResources(factory, frontend);
        }

        return frontend;
    }

    public override void Import(LabURI package, String? variant, Int32? layoutId)
    {
        var frontend = GetTwinItem<ITwinSection>();
        var owner = (GlobalAsset)Owner;
        for (var i = 0; i < frontend.GetItemsAmount(); i++)
        {
            var sound = frontend.GetItem<ITwinSound>(frontend.GetItem(i).GetID());
            var soundImport = new SoundEffect(package, true, $"{sound.GetName()}_ui_sfx_{i}", sound.GetID(), $"{sound.GetName()}_ui_sfx_{i}", sound)
            {
                AdditionalPath = owner.GlobalPath
            };
            soundImport.RegenerateLinks();
            AssetManager.Get().AddAssetToImport(soundImport);
            UiSounds.Add(soundImport.URI);
        }
    }

    protected override void Dispose(Boolean disposing)
    {
        UiSounds.Clear();
    }
}