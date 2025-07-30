using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Silk.NET.OpenGL;

namespace TT_Lab.Rendering.ImGuiUtil;

/*
MIT License

- Copyright (c) 2019-2020 Ultz Limited
- Copyright (c) 2021- .NET Foundation and Contributors

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
 */

public struct UniformFieldInfo
{
    public int Location;
    public string Name;
    public int Size;
    public UniformType Type;
}

public class Shader
{
    private uint Program { get; }
    private readonly Dictionary<string, int> _uniformToLocation = new();
    private readonly Dictionary<string, int> _attribLocation = new();
    private bool _initialized;
    private readonly GL _gl;

    public Shader(GL gl, string vertexShader, string fragmentShader)
    {
        _gl = gl;
        (ShaderType Type, string Path)[] files = [(ShaderType.VertexShader, vertexShader), (ShaderType.FragmentShader, fragmentShader)];
        Program = CreateProgram(files);
    }

    public void UseShader()
    {
        _gl.UseProgram(Program);
    }

    public void Dispose()
    {
        if (!_initialized)
        {
            return;
        }
        
        _gl.DeleteProgram(Program);
        _initialized = false;
    }

    public UniformFieldInfo[] GetUniforms()
    {
        _gl.GetProgram(Program, GLEnum.ActiveUniforms, out var uniformCount);
        var uniforms = new UniformFieldInfo[uniformCount];
        for (var i = 0; i < uniformCount; i++)
        {
            string name = _gl.GetActiveUniform(Program, (uint)i, out var size, out var type);
            UniformFieldInfo fieldInfo;
            fieldInfo.Location = GetUniformLocation(name);
            fieldInfo.Name = name;
            fieldInfo.Size = size;
            fieldInfo.Type = type;
            uniforms[i] = fieldInfo;
        }

        return uniforms;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetUniformLocation(string uniform)
    {
        if (_uniformToLocation.TryGetValue(uniform, out var location))
        {
            return location;
        }
        
        location = _gl.GetUniformLocation(Program, uniform);
        _uniformToLocation.Add(uniform, location);
        if (location == -1)
        {
            Debug.Print($"The uniform '{uniform}' does not exist in the shader!");
        }

        return location;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetAttribLocation(string attrib)
    {
        if (_attribLocation.TryGetValue(attrib, out var location))
        {
            return location;
        }
        
        location = _gl.GetAttribLocation(Program, attrib);
        _attribLocation.Add(attrib, location);
        if (location == -1)
        {
            Debug.Print($"The attrib '{attrib}' does not exist in the shader!");
        }

        return location;
    }

    private uint CreateProgram(params (ShaderType Type, string source)[] shaderPaths)
    {
        var program = _gl.CreateProgram();
        Span<uint> shaders = stackalloc uint[shaderPaths.Length];
        for (var i = 0; i < shaderPaths.Length; i++)
        {
            shaders[i] = CompileShader(shaderPaths[i].Type, shaderPaths[i].source);
        }

        foreach (var shader in shaders)
        {
            _gl.AttachShader(program, shader);
        }
        
        _gl.LinkProgram(program);
        _gl.GetProgram(program, GLEnum.LinkStatus, out var success);
        if (success == 0)
        {
            var info = _gl.GetProgramInfoLog(program);
            Debug.WriteLine($"GL.LinkProgram had info log:\n{info}");
        }

        foreach (var shader in shaders)
        {
            _gl.DetachShader(program, shader);
            _gl.DeleteShader(shader);
        }

        _initialized = true;
        return program;
    }

    private uint CompileShader(ShaderType type, string source)
    {
        var shader = _gl.CreateShader(type);
        _gl.ShaderSource(shader, source);
        _gl.CompileShader(shader);
        _gl.GetShader(shader, ShaderParameterName.CompileStatus, out var success);
        if (success == 0)
        {
            var info = _gl.GetShaderInfoLog(shader);
            Debug.WriteLine($"GL.CompileShader for shader [{type}] had info log:\n{info}");
        }

        return shader;
    }
}