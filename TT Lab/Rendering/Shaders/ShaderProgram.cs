using System;
using System.Collections.Generic;
using Silk.NET.OpenGL;

namespace TT_Lab.Rendering.Shaders;

public class ShaderProgram : IDisposable
{
    private readonly RenderContext _context;
    private readonly uint _program;
    private readonly Dictionary<string, int> _attributeLocations = [];
    private readonly Dictionary<string, int> _uniformLocations = [];

    public ShaderProgram(RenderContext context, params Shader[] shaders)
    {
        _context = context;
        _program = context.Gl.CreateProgram();
        foreach (var shader in shaders)
        {
            context.Gl.AttachShader(_program, shader.Handle);
        }
        
        context.Gl.LinkProgram(_program);

        var linkStatus = context.Gl.GetProgram(_program, GLEnum.LinkStatus);
        if (linkStatus != 1)
        {
            throw new InvalidOperationException($"Program link failed: {context.Gl.GetProgramInfoLog(_program)}");
        }
    }
    
    public uint Handle => _program;

    public void Use()
    {
        _context.Gl.UseProgram(_program);
    }

    public int GetUniformLocation(string name)
    {
        if (_uniformLocations.TryGetValue(name, out var location))
        {
            return location;
        }
        
        location = _context.Gl.GetUniformLocation(_program, name);
        if (location == -1)
        {
            Console.WriteLine($@"WARNING: uniform {name} location not found or uniform is unused");
        }
        _uniformLocations[name] = location;
        return location;
    }

    public int GetAttributeLocation(string name)
    {
        if (_attributeLocations.TryGetValue(name, out var location))
        {
            return location;
        }
        
        location = _context.Gl.GetAttribLocation(_program, name);
        if (location == -1)
        {
            throw new InvalidOperationException($"Attribute {name} not found");
        }
        
        _attributeLocations[name] = location;
        return location;
    }
    
    public void Dispose()
    {
        _context.Gl.DeleteProgram(_program);
        GC.SuppressFinalize(this);
    }
}