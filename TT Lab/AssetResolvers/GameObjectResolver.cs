using System;
using System.Collections.Generic;
using System.Linq;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;

namespace TT_Lab.AssetResolvers;

public class GameObjectResolver : AssetResolver<ITwinObject>
{
    private readonly Dictionary<String, TwinBehaviourStarter> _starterMap;
    private readonly AnimationResolver _animationResolver = new();
    private readonly OgiResolver _ogiResolver;
    private readonly SoundResolver _soundResolver = new();
    private readonly Dictionary<ushort, List<ushort>> _ogiToAnimations = new();

    public GameObjectResolver(Dictionary<string, TwinBehaviourStarter> starterMap)
    {
        _starterMap = starterMap;
        _ogiResolver = new OgiResolver(_animationResolver);
    }

    public override void CreateAssetsFromChunk(ITwinSection chunk, Package package)
    {
        var code = chunk.GetItem<ITwinSection>(Constants.LEVEL_CODE_SECTION);
        var gameObjectSection = code.GetItem<ITwinSection>(Constants.CODE_GAME_OBJECTS_SECTION);
        var ogiSection = code.GetItem<ITwinSection>(Constants.CODE_OGIS_SECTION);
        var animSection = code.GetItem<ITwinSection>(Constants.CODE_ANIMATIONS_SECTION);
        ITwinSection[] sfxSections = [
            code.GetItem<ITwinSection>(Constants.CODE_SOUND_EFFECTS_SECTION),
            code.GetItem<ITwinSection>(Constants.CODE_LANG_ENG_SECTION),
            code.GetItem<ITwinSection>(Constants.CODE_LANG_FRE_SECTION),
            code.GetItem<ITwinSection>(Constants.CODE_LANG_GER_SECTION),
            code.GetItem<ITwinSection>(Constants.CODE_LANG_ITA_SECTION),
            code.GetItem<ITwinSection>(Constants.CODE_LANG_SPA_SECTION),
            code.GetItem<ITwinSection>(Constants.CODE_LANG_JPN_SECTION),
        ];
        for (var i = 0; i < gameObjectSection.GetItemsAmount(); ++i)
        {
            var itemId = gameObjectSection.GetItem(i).GetID();
            var asset = CreateAssetFromId(chunk, gameObjectSection, package, itemId);
            if (asset == null)
            {
                continue;
            }
            
            var twinGameObject = gameObjectSection.GetItem<ITwinObject>(itemId);

            twinGameObject.RefAnimations.ForEach(animRef => _animationResolver.Collect(animSection, animRef));
            twinGameObject.AnimationSlots.ForEach(animRef => _animationResolver.Collect(animSection, animRef));
            twinGameObject.RefOGIs.ForEach(ogiRef => _ogiResolver.CreateAssetFromId(chunk, ogiSection, package, ogiRef));

            foreach (var soundRef in twinGameObject.RefSounds)
            {
                _soundResolver.CreateAssetFromId<SoundEffect>(sfxSections[0], package, soundRef);
                _soundResolver.CreateAssetFromId<SoundEffectEN>(sfxSections[1], package, soundRef);
                _soundResolver.CreateAssetFromId<SoundEffectFR>(sfxSections[2], package, soundRef);
                _soundResolver.CreateAssetFromId<SoundEffectGR>(sfxSections[3], package, soundRef);
                _soundResolver.CreateAssetFromId<SoundEffectIT>(sfxSections[4], package, soundRef);
                _soundResolver.CreateAssetFromId<SoundEffectSP>(sfxSections[5], package, soundRef);
                _soundResolver.CreateAssetFromId<SoundEffectJP>(sfxSections[6], package, soundRef);
            }
            
            for (var j = 0; j < twinGameObject.OGISlots.Count; ++j)
            {
                var ogiReference = twinGameObject.OGISlots[j];
                if (ogiReference == ushort.MaxValue)
                {
                    continue;
                }

                var animReference = twinGameObject.AnimationSlots[j];
                if (animReference == ushort.MaxValue)
                {
                    continue;
                }

                if (!_ogiToAnimations.TryGetValue(ogiReference, out var value))
                {
                    value = [];
                    _ogiToAnimations.Add(ogiReference, value);
                }

                if (value.Contains(animReference))
                {
                    continue;
                }
                
                value.Add(animReference);
            }
        }
    }

    // Objects and sounds that differ between chunks have a version for each, references to them get the one of the chunk being built
    public List<LabURI> GetChunkVersions(ITwinSection chunk)
    {
        var code = chunk.GetItem<ITwinSection>(Constants.LEVEL_CODE_SECTION);
        var objects = code.GetItem<ITwinSection>(Constants.CODE_GAME_OBJECTS_SECTION);
        var versions = Enumerable.Range(0, objects.GetItemsAmount()).Select(i => GetResolvedUri(objects.GetItem<ITwinObject>(objects.GetItem(i).GetID()))).ToList();
        foreach (var (sectionId, resolve) in SoundSections())
        {
            if (!code.ContainsItem((UInt32)sectionId))
            {
                continue;
            }

            var section = code.GetItem<ITwinSection>((UInt32)sectionId);
            versions.AddRange(Enumerable.Range(0, section.GetItemsAmount()).Select(i => resolve(section, section.GetItem(i).GetID())));
        }

        return versions.OfType<LabURI>().Distinct().ToList();
    }

    private (Int32, Func<ITwinSection, UInt32, LabURI?>)[] SoundSections() =>
    [
        (Constants.CODE_SOUND_EFFECTS_SECTION, _soundResolver.GetResolvedUri<SoundEffect>),
        (Constants.CODE_LANG_ENG_SECTION, _soundResolver.GetResolvedUri<SoundEffectEN>),
        (Constants.CODE_LANG_FRE_SECTION, _soundResolver.GetResolvedUri<SoundEffectFR>),
        (Constants.CODE_LANG_GER_SECTION, _soundResolver.GetResolvedUri<SoundEffectGR>),
        (Constants.CODE_LANG_ITA_SECTION, _soundResolver.GetResolvedUri<SoundEffectIT>),
        (Constants.CODE_LANG_SPA_SECTION, _soundResolver.GetResolvedUri<SoundEffectSP>),
        (Constants.CODE_LANG_JPN_SECTION, _soundResolver.GetResolvedUri<SoundEffectJP>)
    ];

    // The game plays some of the startup chunk's sounds without any object referencing them
    public List<MetaAsset> CreateUnreferencedSounds(ITwinSection chunk, Package package)
    {
        var code = chunk.GetItem<ITwinSection>(Constants.LEVEL_CODE_SECTION);
        var objects = code.GetItem<ITwinSection>(Constants.CODE_GAME_OBJECTS_SECTION);
        var referenced = Enumerable.Range(0, objects.GetItemsAmount()).Select(i => (ITwinObject)objects.GetItem(i))
            .SelectMany(o => o.RefSounds.Concat(o.SoundSlots)).Select(id => (UInt32)id).ToHashSet();
        var sounds = new List<MetaAsset?>();
        foreach (var (sectionId, create) in new (Int32, Func<ITwinSection, UInt32, MetaAsset?>)[]
                 {
                     (Constants.CODE_SOUND_EFFECTS_SECTION, (section, id) => _soundResolver.CreateAssetFromId<SoundEffect>(section, package, id)),
                     (Constants.CODE_LANG_ENG_SECTION, (section, id) => _soundResolver.CreateAssetFromId<SoundEffectEN>(section, package, id)),
                     (Constants.CODE_LANG_FRE_SECTION, (section, id) => _soundResolver.CreateAssetFromId<SoundEffectFR>(section, package, id)),
                     (Constants.CODE_LANG_GER_SECTION, (section, id) => _soundResolver.CreateAssetFromId<SoundEffectGR>(section, package, id)),
                     (Constants.CODE_LANG_ITA_SECTION, (section, id) => _soundResolver.CreateAssetFromId<SoundEffectIT>(section, package, id)),
                     (Constants.CODE_LANG_SPA_SECTION, (section, id) => _soundResolver.CreateAssetFromId<SoundEffectSP>(section, package, id)),
                     (Constants.CODE_LANG_JPN_SECTION, (section, id) => _soundResolver.CreateAssetFromId<SoundEffectJP>(section, package, id))
                 })
        {
            if (!code.ContainsItem((UInt32)sectionId))
            {
                continue;
            }

            var section = code.GetItem<ITwinSection>((UInt32)sectionId);
            for (var i = 0; i < section.GetItemsAmount(); i++)
            {
                var id = section.GetItem(i).GetID();
                if (!referenced.Contains(id))
                {
                    sounds.Add(create(section, id));
                }
            }
        }

        return sounds.OfType<MetaAsset>().ToList();
    }

    protected override IAsset CreateAsset(ITwinSection chunk, Package package, ITwinObject item, bool needVariant, string variant)
    {
        return new GameObject(package.URI, needVariant, variant, item.GetID(), item.GetName(), item, _starterMap);
    }

    public override void FinalizeResolve()
    {
        _ogiResolver.FinalizeResolve();
        _ogiResolver.AddAnimations(_ogiToAnimations);
        _soundResolver.FinalizeResolve();
        
        base.FinalizeResolve();
    }
}