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

            twinGameObject.RefAnimations.ForEach(animRef => _animationResolver.CreateAssetFromId(chunk, animSection, package, animRef));
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

    protected override IAsset CreateAsset(ITwinSection chunk, Package package, ITwinObject item, bool needVariant, string variant)
    {
        return new GameObject(package.URI, needVariant, variant, item.GetID(), item.GetName(), item, _starterMap);
    }

    public override void FinalizeResolve()
    {
        _animationResolver.FinalizeResolve();
        _ogiResolver.FinalizeResolve();
        _ogiResolver.AddAnimationLinks(_ogiToAnimations);
        _soundResolver.FinalizeResolve();
        
        base.FinalizeResolve();
    }
}