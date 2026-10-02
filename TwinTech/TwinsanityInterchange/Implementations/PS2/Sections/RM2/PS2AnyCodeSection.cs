using System.Linq;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.RM2.Code;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.RM2
{
    public class PS2AnyCodeSection : BaseTwinSection
    {
        public PS2AnyCodeSection() : base()
        {
            idToClassDictionary.Add(Constants.CODE_GAME_OBJECTS_SECTION, typeof(PS2AnyGameObjectsSection));
            idToClassDictionary.Add(Constants.CODE_BEHAVIOURS_SECTION, typeof(PS2AnyBehavioursSection));
            idToClassDictionary.Add(Constants.CODE_ANIMATIONS_SECTION, typeof(PS2AnyAnimationsSection));
            idToClassDictionary.Add(Constants.CODE_OGIS_SECTION, typeof(PS2AnyOGIsSection));
            idToClassDictionary.Add(Constants.CODE_BEHAVIOUR_COMMANDS_SEQUENCES_SECTION, typeof(PS2AnyBehaviourCommandsSequencesSection));
            idToClassDictionary.Add(Constants.CODE_UNUSED_SECTION, typeof(BaseTwinItem));
            idToClassDictionary.Add(Constants.CODE_SOUND_EFFECTS_SECTION, typeof(PS2AnySoundsSection));
            idToClassDictionary.Add(Constants.CODE_LANG_ENG_SECTION, typeof(PS2AnySoundsSection));
            idToClassDictionary.Add(Constants.CODE_LANG_FRE_SECTION, typeof(PS2AnySoundsSection));
            idToClassDictionary.Add(Constants.CODE_LANG_GER_SECTION, typeof(PS2AnySoundsSection));
            idToClassDictionary.Add(Constants.CODE_LANG_SPA_SECTION, typeof(PS2AnySoundsSection));
            idToClassDictionary.Add(Constants.CODE_LANG_ITA_SECTION, typeof(PS2AnySoundsSection));
            idToClassDictionary.Add(Constants.CODE_LANG_JPN_SECTION, typeof(PS2AnySoundsSection));
        }

        protected override void PreprocessWrite()
        {
            base.PreprocessWrite();
            foreach (var item in Items.Where(i => i is BaseTwinSection).Cast<BaseTwinSection>())
            {
                item.SortItems(delegate (ITwinItem item1, ITwinItem item2)
                {
                    return item1.GetID().CompareTo(item2.GetID());
                });
            }
        }
    }
}
