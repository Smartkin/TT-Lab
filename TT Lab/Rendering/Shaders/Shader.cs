using System;
using System.Text;
using Silk.NET.OpenGL;
using TT_Lab.Util;
using Twinsanity.Libraries;

namespace TT_Lab.Rendering.Shaders;

public class Shader : IDisposable
{
    [Flags]
    public enum ShaderSwitches : ulong
    {
        None = 0,
        Skinning = 1 << 0,
        BlendSkinning = 1 << 1,
        AlphaBlending = 1 << 2,
        Texturing = 1 << 3,
    }
    
    private readonly RenderContext _context;
    private readonly uint _shader;

    public Shader(RenderContext context, ShaderType shaderType, string sourceFile, ShaderSwitches switches = ShaderSwitches.None)
    {
        var initialShaderPath = "";
        if (sourceFile.Contains('/'))
        {
            initialShaderPath = sourceFile[..(sourceFile.LastIndexOf('/') + 1)];
        }
        var source = ManifestResourceLoader.LoadTextFile($"Media/Shaders/{sourceFile}");
        if (switches != 0)
        {
            ProcessSwitches(ref source, switches);
        }
        
        source = "#version 450 core\r\n" + source;
        ProcessIncludes(ref source, initialShaderPath);
        
        ShaderType = shaderType;
        _context = context;
        _shader = context.Gl.CreateShader(shaderType);
        context.Gl.ShaderSource(_shader, source);
        context.Gl.CompileShader(_shader);
        
        var compilationStatus = context.Gl.GetShader(_shader, ShaderParameterName.CompileStatus);
        if (compilationStatus != 1)
        {
            throw new InvalidOperationException($"Shader compilation failed: {context.Gl.GetShaderInfoLog(_shader)}");
        }
    }
    
    public ShaderType ShaderType { get; }
    public uint Handle => _shader;
    
    public void Dispose()
    {
        _context.Gl.DeleteShader(_shader);
        GC.SuppressFinalize(this);
    }

    private static void ProcessSwitches(ref string shaderSource, ShaderSwitches switches)
    {
        var stringBuilder = new StringBuilder();
        if (switches.HasFlag(ShaderSwitches.Skinning))
        {
            stringBuilder.AppendLine("#define USE_SKINNING");
        }

        if (switches.HasFlag(ShaderSwitches.BlendSkinning))
        {
            stringBuilder.AppendLine("#define USE_BLEND_SKINNING");
        }

        if (switches.HasFlag(ShaderSwitches.AlphaBlending))
        {
            stringBuilder.AppendLine("#define USE_ALPHA_BLENDING");
        }

        if (switches.HasFlag(ShaderSwitches.Texturing))
        {
            stringBuilder.AppendLine("#define USE_TEXTURES");
        }
        
        shaderSource = stringBuilder + shaderSource;
    }

    private static void ProcessIncludes(ref string shaderSource, string initialShaderPath)
    {
        while (shaderSource.Contains("#include \"") && StringUtils.GetStringInBetween(shaderSource, "#include \"", "\"") != string.Empty)
        {
            var includePath = StringUtils.GetStringInBetween(shaderSource, "#include \"", "\"");
            var includeLoadPath = $"Media/Shaders/{initialShaderPath}{includePath}";
            var includeText = ManifestResourceLoader.LoadTextFile(includeLoadPath);
            var furtherIncludePath = initialShaderPath;
            if (includePath.Contains('/'))
            {
                furtherIncludePath = initialShaderPath + '/' + includePath[..(includePath.LastIndexOf('/') + 1)];
            }
            ProcessIncludes(ref includeText, furtherIncludePath);
            
            shaderSource = shaderSource.Replace($"#include \"{includePath}\"", includeText);
        }
    }
}