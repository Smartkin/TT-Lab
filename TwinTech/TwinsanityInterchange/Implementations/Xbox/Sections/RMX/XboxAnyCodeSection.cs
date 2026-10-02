using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.Xbox.Sections.RMX.Code;

namespace Twinsanity.TwinsanityInterchange.Implementations.Xbox.Sections.RMX
{
    public class XboxAnyCodeSection : BaseTwinSection
    {
        public XboxAnyCodeSection() : base()
        {
            idToClassDictionary.Add(Constants.CODE_GAME_OBJECTS_SECTION, typeof(XboxAnyGameObjectsSection));
            idToClassDictionary.Add(Constants.CODE_BEHAVIOURS_SECTION, typeof(XboxAnyBehavioursSection));
            idToClassDictionary.Add(Constants.CODE_ANIMATIONS_SECTION, typeof(XboxAnyAnimationsSection));
            idToClassDictionary.Add(Constants.CODE_OGIS_SECTION, typeof(XboxAnyOGIsSection));
            idToClassDictionary.Add(Constants.CODE_BEHAVIOUR_COMMANDS_SEQUENCES_SECTION, typeof(XboxAnyBehaviourCommandsSequencesSection));
            idToClassDictionary.Add(Constants.CODE_UNUSED_SECTION, typeof(BaseTwinItem));
            idToClassDictionary.Add(Constants.CODE_SOUND_EFFECTS_SECTION, typeof(XboxAnySoundsSection));
            idToClassDictionary.Add(Constants.CODE_LANG_ENG_SECTION, typeof(XboxAnySoundsSection));
            idToClassDictionary.Add(Constants.CODE_LANG_FRE_SECTION, typeof(XboxAnySoundsSection));
            idToClassDictionary.Add(Constants.CODE_LANG_GER_SECTION, typeof(XboxAnySoundsSection));
            idToClassDictionary.Add(Constants.CODE_LANG_SPA_SECTION, typeof(XboxAnySoundsSection));
            idToClassDictionary.Add(Constants.CODE_LANG_ITA_SECTION, typeof(XboxAnySoundsSection));
            idToClassDictionary.Add(Constants.CODE_LANG_JPN_SECTION, typeof(XboxAnySoundsSection));
        }
    }
}
