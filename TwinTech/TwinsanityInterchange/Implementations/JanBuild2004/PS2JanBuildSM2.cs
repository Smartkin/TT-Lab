using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections;

namespace Twinsanity.TwinsanityInterchange.Implementations.JanBuild2004;

public class PS2JanBuildSM2 : BaseTwinSection
{
    public PS2JanBuildSM2() : base()
    {
        idToClassDictionary.Add(0x5, typeof(BaseTwinItem));
        idToClassDictionary.Add(Constants.SCENERY_SECENERY_ITEM, typeof(PS2AnyScenery));
        // idToClassDictionary.Add(Constants.SCENERY_UNK_1_ITEM, typeof(BaseTwinItem));
        // idToClassDictionary.Add(Constants.SCENERY_UNK_2_ITEM, typeof(BaseTwinItem));
        // idToClassDictionary.Add(Constants.SCENERY_UNK_3_ITEM, typeof(BaseTwinItem));
        // idToClassDictionary.Add(Constants.SCENERY_DYNAMIC_SECENERY_ITEM, typeof(PS2AnyDynamicScenery));
        // idToClassDictionary.Add(Constants.SCENERY_LINK_ITEM, typeof(PS2AnyLink));
    }
}