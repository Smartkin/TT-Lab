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
    // What MaterialEntry and DrawBase hold, set for every material drawn: consecutive draws of a material skip them
    private int _materialEntry = -1;
    private int _drawBase = int.MinValue;
    // The shader's own initial value
    private int _alphaSubpass;

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

    /// <summary>
    /// The material entry the program draws with (<see cref="Materials.MaterialTable"/>), whether the program is in use or not
    /// </summary>
    public void SetMaterialEntry(int entry)
    {
        if (_materialEntry == entry)
        {
            return;
        }

        _materialEntry = entry;
        _context.Gl.ProgramUniform1(_program, this[KnownUniform.MaterialEntry], entry);
    }

    /// <summary>
    /// Where an indirect draw's entries start (<see cref="IndirectDraws"/>), -1 for the draws of one batch
    /// </summary>
    public void SetDrawBase(int first)
    {
        if (_drawBase == first)
        {
            return;
        }

        _drawBase = first;
        _context.Gl.ProgramUniform1(_program, this[KnownUniform.DrawBase], first);
    }

    /// <summary>
    /// Which fragments the draws have (<see cref="AlphaFailDraw"/>, <c>ModelLayout.frag</c>'s AlphaSubpass), whether the program is in use or not
    /// </summary>
    public void SetAlphaSubpass(int subpass)
    {
        if (_alphaSubpass == subpass)
        {
            return;
        }

        _alphaSubpass = subpass;
        _context.Gl.ProgramUniform1(_program, this[KnownUniform.AlphaSubpass], subpass);
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
