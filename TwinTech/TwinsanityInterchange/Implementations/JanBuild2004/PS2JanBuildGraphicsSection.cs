using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.Graphics;

namespace Twinsanity.TwinsanityInterchange.Implementations.JanBuild2004;

public class PS2JanBuildGraphicsSection : BaseTwinSection
{
    public PS2JanBuildGraphicsSection() : base()
    {
        idToClassDictionary.Add(Constants.GRAPHICS_TEXTURES_SECTION, typeof(BaseTwinSection));
        idToClassDictionary.Add(Constants.GRAPHICS_MATERIALS_SECTION, typeof(PS2JanBuildUnkGraphicsSection));
        idToClassDictionary.Add(Constants.GRAPHICS_MODELS_SECTION, typeof(BaseTwinSection));
        idToClassDictionary.Add(Constants.GRAPHICS_RIGID_MODELS_SECTION, typeof(BaseTwinSection));
        idToClassDictionary.Add(Constants.GRAPHICS_SKINS_SECTION, typeof(BaseTwinSection));
        idToClassDictionary.Add(Constants.GRAPHICS_BLEND_SKINS_SECTION, typeof(BaseTwinSection));
        idToClassDictionary.Add(Constants.GRAPHICS_MESHES_SECTION, typeof(BaseTwinSection));
        idToClassDictionary.Add(Constants.GRAPHICS_LODS_SECTION, typeof(BaseTwinSection));
        idToClassDictionary.Add(Constants.GRAPHICS_SKYDOMES_SECTION, typeof(BaseTwinSection));
    }
}