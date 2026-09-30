using TT_Lab.Rendering.Shaders;
using TT_Lab.Util;

namespace TT_Lab.Tests.Rendering;

// The shaders are embedded in TT Lab and include each other by relative paths (lygia's go up with ../), which have to resolve without files
public class ShaderSourceTests
{
    public static TheoryData<string> Shaders()
    {
        var shaders = new TheoryData<string>();
        foreach (var shader in ManifestResourceLoader.GetFilesIn("Media/Shaders"))
        {
            shaders.Add(Path.GetFileName(shader));
        }

        return shaders;
    }

    [Theory]
    [MemberData(nameof(Shaders))]
    public void EveryShaderLoadsWithItsIncludes(string shader)
    {
        var source = Shader.LoadSource(shader, Shader.ShaderSwitches.Skinning | Shader.ShaderSwitches.Texturing);

        Assert.StartsWith("#version 460 core", source);
        Assert.DoesNotContain("#include \"", source);
    }

    [Fact]
    public void RelativePathsResolveLikeTheFileSystem()
    {
        Assert.Equal(ManifestResourceLoader.LoadTextFile("Media/Shaders/lygia/math/const.glsl"),
            ManifestResourceLoader.LoadTextFile("Media/Shaders//lygia/lighting/specular/../../math/./const.glsl"));
        Assert.Throws<FileNotFoundException>(() => ManifestResourceLoader.Open("Media/Shaders/Missing.frag"));
    }
}
