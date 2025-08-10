using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using GlmSharp;
using TT_Lab.Rendering.Services;

namespace TT_Lab.Rendering;

public abstract class Renderable
{
    protected readonly RenderContext Context;
    private mat4 _cachedWorldTransform = mat4.Identity;
    private mat4 _cachedRenderTransform = mat4.Identity;
    private mat4 _localTransform = mat4.Identity;
    private Renderable? _parent = null;
    private readonly Dictionary<string, Renderable> _children = [];
    private bool _isVisible = true;
    private bool _canUpdate = true;
    private vec4 _diffuse = vec4.Ones;
    private bool _inheritScale = true;

    protected Renderable(RenderContext context, string name = "")
    {
        Context = context;
        Name = string.IsNullOrEmpty(name) ? $"{GetType().Name} {GetHashCode()}" : name;
    }

    public virtual (string, int)[] GetPriorityPasses()
    {
        return [];
    }

    public virtual void Update(float delta)
    {
        if (!CanUpdate)
        {
            return;
        }
        
        UpdateSelf(delta);
    }

    public void SetInheritScale(bool inheritScale)
    {
        _inheritScale = inheritScale;
        UpdateTransform();
    }

    public void Render(float delta)
    {
        if (!IsVisible)
        {
            return;
        }
        
        var diffuseLoc = Context.CurrentPass.Program.GetUniformLocation("Diffuse");
        Context.Gl.Uniform4(diffuseLoc, Diffuse.Values);
        RenderSelf(delta);
    }

    public virtual void UpdateRenderTransform()
    {
        _cachedRenderTransform = _cachedWorldTransform;
        foreach (var child in Children)
        {
            child.UpdateRenderTransform();
        }
    }

    public T GetChild<T>(string name) where T : Renderable
    {
        var result = GetChild(name);
        if (result != null)
        {
            return (T)result;
        }
        foreach (var child in _children.Values)
        {
            result = child.GetChild(name);
            if (result != null)
            {
                break;
            }
        }
        
        Debug.Assert(result != null, $"Child {name} not found");
        return (T)result;
    }

    public vec3 GetRenderPosition()
    {
        return RenderTransform.Column3.xyz;
    }

    public vec3 GetRenderForward()
    {
        return RenderTransform.Column2.xyz;
    }

    public vec3 GetRotation()
    {
        var quat = GlmSharp.quat.FromMat4(WorldTransform);
        var angles = quat.EulerAngles;
        return new vec3((float)angles.x, (float)angles.y, (float)angles.z);
    }

    public vec3 GetScale()
    {
        return new vec3(WorldTransform.m00, WorldTransform.m11, WorldTransform.m22);
    }

    public vec3 GetPosition()
    {
        return WorldTransform.Column3.xyz;
    }

    public void SetPosition(vec3 position)
    {
        var translation = mat4.Translate(position);
        _localTransform = _localTransform with { Column3 = translation.Column3 };
        _localTransform.m33 = 1.0f;
        UpdateTransform();
    }

    public void SetLocalTransform(mat4 transform)
    {
        _localTransform = transform;
        UpdateTransform();
    }

    public virtual vec3 GetLeft()
    {
        return WorldTransform.Column0.xyz;
    }
    
    public vec3 GetUp()
    {
        return WorldTransform.Column1.xyz;
    }

    public vec3 GetForward()
    {
        return WorldTransform.Column2.xyz;
    }

    public void Translate(vec3 translation)
    {
        Transform(mat4.Translate(translation));
    }

    public void Rotate(quat rotation)
    {
        Transform(rotation.ToMat4);
    }

    public void Rotate(vec3 rotation, bool reverseOrder = false)
    {
        Transform(mat4.RotateZ(rotation.z) * mat4.RotateY(rotation.y) * mat4.RotateX(rotation.x), reverseOrder);
    }

    public void Scale(vec3 scale)
    {
        Transform(mat4.Scale(scale));
    }

    public void Transform(mat4 transform, bool reverseOrder = false)
    {
        _localTransform = reverseOrder ? _localTransform * transform : transform * _localTransform;
        _localTransform.m33 = 1.0f;
        UpdateTransform();
    }

    private Renderable? GetChild(string name)
    {
        return _children.FirstOrDefault(x => x.Key == name).Value;
    }

    public void AddChild(Renderable child)
    {
        _children.Add(child.Name, child);
        child.Parent = this;
        child.UpdateTransform();
    }

    public void RemoveChild(Renderable child)
    {
        _children.Remove(child.Name);
        child.Parent = null;
        child.UpdateTransform();
    }

    protected virtual void RenderSelf(float delta) {}

    protected virtual void UpdateSelf(float delta) {}

    public Renderable? Parent
    {
        get => _parent;
        set
        {
            _parent = value;
            UpdateTransform();
        }
    }

    public sealed override Int32 GetHashCode()
    {
        // ReSharper disable once BaseObjectGetHashCodeCallInGetHashCode
        return base.GetHashCode();
    }
    
    private void UpdateTransform()
    {
        if (_inheritScale)
        {
            _cachedWorldTransform = (_parent != null) ? _parent.WorldTransform * _localTransform : _localTransform;
        }
        else
        {
            if (_parent != null)
            {
                var parentTranslation = mat4.Translate(_parent.GetPosition());
                var rotation = new quat(_parent.GetRotation());
                _cachedWorldTransform = parentTranslation * rotation.ToMat4 * _localTransform;
            }
            else
            {
                _cachedWorldTransform = _localTransform;
            }
        }
        foreach (var child in _children.Values)
        {
            child.UpdateTransform();
        }
    }

    public vec4 Diffuse
    {
        get => _diffuse;
        set
        {
            _diffuse = value;
            foreach (var child in _children.Values)
            {
                child.Diffuse = value;
            }
        }
    }

    public mat4 RenderTransform => _cachedRenderTransform;
    public IReadOnlyList<Renderable> Children => _children.Values.ToImmutableList();
    public string Name { get; }

    public virtual bool DoesUpdates => false;
    
    public bool CanUpdate
    {
        get => _canUpdate;
        set
        {
            _canUpdate = value;
            foreach (var child in _children.Values)
            {
                child.CanUpdate = value;
            }
        }
    }

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            _isVisible = value;
            foreach (var child in _children.Values)
            {
                child.IsVisible = value;
            }
        }
    }

    public mat4 WorldTransform => _cachedWorldTransform;
    
    public mat4 LocalTransform
    {
        get => _localTransform;

        set
        {
            _localTransform = value;
            UpdateTransform();
        }
    }
}