using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using GlmSharp;
using Silk.NET.OpenGL;

namespace TT_Lab.Rendering.Shaders;

public class ShaderProgram : IDisposable
{
    private readonly RenderContext _context;
    private readonly uint _program;
    private readonly Dictionary<string, int> _attributeLocations = [];
    private readonly Dictionary<string, int> _uniformLocations = [];
    private readonly int[] _knownUniformLocations = new int[KnownUniforms.Count];

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

        // Not every program uses every known uniform, the ones it doesn't have end up as -1 which GL ignores
        for (var i = 0; i < _knownUniformLocations.Length; i++)
        {
            _knownUniformLocations[i] = context.Gl.GetUniformLocation(_program, KnownUniforms.GetName((KnownUniform)i));
        }
    }

    public uint Handle => _program;

    public int this[KnownUniform uniform] => _knownUniformLocations[(int)uniform];

    public void Use()
    {
        _context.State.UseProgram(_program);
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

    public void SetUniform(KnownUniform uniform, float value)
    {
        _context.Gl.Uniform1(this[uniform], value);
    }

    public void SetUniform(KnownUniform uniform, int value)
    {
        _context.Gl.Uniform1(this[uniform], value);
    }

    public void SetUniform(KnownUniform uniform, bool value)
    {
        _context.Gl.Uniform1(this[uniform], value ? 1 : 0);
    }

    public void SetUniform(KnownUniform uniform, vec2 value)
    {
        _context.Gl.Uniform2(this[uniform], value.x, value.y);
    }

    public void SetUniform(KnownUniform uniform, vec3 value)
    {
        _context.Gl.Uniform3(this[uniform], value.x, value.y, value.z);
    }

    public void SetUniform(KnownUniform uniform, vec4 value)
    {
        _context.Gl.Uniform4(this[uniform], value.x, value.y, value.z, value.w);
    }

    public void SetUniform(KnownUniform uniform, in mat4 value)
    {
        // mat4's fields are laid out column after column the same way GL expects them
        _context.Gl.UniformMatrix4(this[uniform], 1, false, ref Unsafe.AsRef(in value.m00));
    }

    public void Dispose()
    {
        _context.Gl.DeleteProgram(_program);
        GC.SuppressFinalize(this);
    }
}
