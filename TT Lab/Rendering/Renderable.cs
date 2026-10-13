using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using GlmSharp;
using TT_Lab.Rendering.Lighting;
using TT_Lab.Rendering.Services;

namespace TT_Lab.Rendering;

public abstract class Renderable
{
    public delegate void ChildChangedHandler(Renderable child);
    public event ChildChangedHandler? ChildAdded;
    public event ChildChangedHandler? ChildRemoved;
    
    protected readonly RenderContext Context;
    private mat4 _cachedWorldTransform = mat4.Identity;
    private mat4 _cachedRenderTransform = mat4.Identity;
    private mat4 _localTransform = mat4.Identity;
    private vec3 _initialPosition = vec3.Zero;
    private quat _initialRotation = quat.Identity;
    private vec3 _initialScale = vec3.Ones;
    private Renderable? _parent = null;
    private readonly Dictionary<string, Renderable> _children = [];
    // The scene graph is walked every frame, the children only get copied when they change
    private Renderable[]? _childrenSnapshot;
    private bool _isVisible = true;
    private bool _canUpdate = true;
    private vec4 _diffuse = vec4.Ones;
    private bool _inheritScale = true;
    private bool _inheritRotation = true;
    private bool _inheritDiffuse = true;
    private bool _inheritVisibility = true;
    // 1 while the renderable waits in its context's queue of changed render transforms
    private int _queued;

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

    public void SetInitialPosition(vec3 position)
    {
        _initialPosition = position;
    }

    public void SetInitialRotation(quat rotation)
    {
        _initialRotation = rotation;
    }

    public void SetInitialScale(vec3 scale)
    {
        _initialScale = scale;
    }

    public void SetInitialTransform(vec3 position, quat rotation, vec3 scale)
    {
        SetInitialPosition(position);
        SetInitialRotation(rotation);
        SetInitialScale(scale);
    }

    public void ResetLocalTransform()
    {
        LocalTransform = mat4.Translate(_initialPosition) * _initialRotation.ToMat4 * mat4.Scale(_initialScale);
    }

    public void SetInheritScale(bool inheritScale)
    {
        _inheritScale = inheritScale;
        UpdateTransform();
    }

    public void SetInheritRotation(bool inheritRotation)
    {
        _inheritRotation = inheritRotation;
        UpdateTransform();
    }

    public void SetInheritDiffuse(bool inheritDiffuse)
    {
        _inheritDiffuse = inheritDiffuse;
    }

    public void SetInheritVisibility(bool inheritVisibility)
    {
        _inheritVisibility = inheritVisibility;
    }

    public void Render(float delta)
    {
        if (!IsVisible)
        {
            return;
        }
        
        RenderSelf(delta);
    }

    public virtual void EndRender() {}

    /// <summary>
    /// The renderable goes into its context's queue of what changed since the frame before (<see cref="RenderContext.FlushRenderTransforms"/>),
    /// from any thread
    /// </summary>
    protected void QueueRenderTransform()
    {
        if (Context != null && Interlocked.Exchange(ref _queued, 1) == 0)
        {
            Context.QueueRenderTransform(this);
        }
    }

    /// <summary>
    /// What changed becomes what's rendered, out of the context's queue on the render thread
    /// </summary>
    internal void ApplyRenderTransform()
    {
        // Out of the queue before reading anything: a change made meanwhile puts the renderable in again
        Interlocked.Exchange(ref _queued, 0);
        TakeRenderState();
    }

    /// <summary>
    /// The world transform becomes the transform it's rendered with, and what else the renderable draws with what's been set (bones, weights)
    /// </summary>
    protected virtual void TakeRenderState()
    {
        if (SameBits(_cachedRenderTransform, _cachedWorldTransform))
        {
            return;
        }

        _cachedRenderTransform = _cachedWorldTransform;
        OnDrawnChanged();
    }

    public T GetChild<T>(string name) where T : Renderable
    {
        var result = GetChild(name);
        if (result != null)
        {
            return (T)result;
        }
        foreach (var child in ChildArray)
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

    public virtual vec3 GetRotation()
    {
        var transform = WorldTransform;
        transform.m00 = 1.0f;
        transform.m11 = 1.0f;
        transform.m22 = 1.0f;
        var quat = GlmSharp.quat.FromMat4(transform);
        var angles = quat.EulerAngles;
        return new vec3((float)angles.x, (float)angles.y, (float)angles.z);
    }

    public quat GetRotationQuat()
    {
        var scale = GetScale();
        var rotMat = new mat3(WorldTransform);
        if (scale.x > 0.000001f)
        {
            rotMat.Column0 /= scale.x;
        }
        if (scale.y > 0.000001f)
        {
            rotMat.Column1 /= scale.y;
        }
        if (scale.z > 0.000001f)
        {
            rotMat.Column2 /= scale.z;
        }
        return quat.FromMat3(rotMat);
    }

    public virtual vec3 GetScale()
    {
        var left = GetLeft();
        var up = GetUp();
        var forward = GetForward();
        var scaleX = (float)Math.Sqrt(left.x * left.x + left.y * left.y + left.z * left.z);
        var scaleY = (float)Math.Sqrt(up.x * up.x + up.y * up.y + up.z * up.z);
        var scaleZ = (float)Math.Sqrt(forward.x * forward.x + forward.y * forward.y + forward.z * forward.z);
        return new vec3(scaleX, scaleY, scaleZ);
    }

    public virtual vec3 GetPosition()
    {
        return WorldTransform.Column3.xyz;
    }

    public virtual void SetPosition(vec3 position)
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

    public vec3 GetLeft()
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

    public virtual void Translate(vec3 translation, bool inLocalSpace = false)
    {
        Transform(mat4.Translate(translation), inLocalSpace);
    }

    public virtual void Rotate(quat rotation, bool inLocalSpace = false)
    {
        Transform(rotation.ToMat4, inLocalSpace);
    }

    public void Rotate(vec3 rotation, bool inLocalSpace = false)
    {
        Rotate(new quat(rotation), inLocalSpace);
    }

    public virtual void Scale(vec3 scale, bool inLocalSpace = false)
    {
        Transform(mat4.Scale(scale), inLocalSpace);
    }

    public void Transform(mat4 transform, bool inLocalSpace = false)
    {
        _localTransform = inLocalSpace ? _localTransform * transform : transform * _localTransform;
        _localTransform.m33 = 1.0f;
        UpdateTransform();
    }

    private Renderable? GetChild(string name)
    {
        return _children.FirstOrDefault(x => x.Key == name).Value;
    }

    public void AddChild(Renderable child)
    {
        lock (_children)
        {
            _children.Add(child.Name, child);
            _childrenSnapshot = null;
        }

        child.Parent = this;
        child.UpdateTransform();
        ChildAdded?.Invoke(child);
    }

    public void RemoveChild(Renderable child)
    {
        // Only the child itself: another one of the same name, made again for the same asset, went with a removal of the one it replaced
        lock (_children)
        {
            if (!_children.TryGetValue(child.Name, out var existing) || existing != child)
            {
                return;
            }

            _children.Remove(child.Name);
            _childrenSnapshot = null;
        }

        child.Parent = null;
        child.UpdateTransform();
        ChildRemoved?.Invoke(child);
    }

    /// <summary>
    /// Sometimes God takes mommies and puppies away and sometimes just sometimes I do.
    /// </summary>
    public void KillChildren()
    {
        lock (_children)
        {
            _children.Clear();
            _childrenSnapshot = null;
        }
    }

    protected virtual void RenderSelf(float delta) {}

    /// <summary>
    /// The renderable's visibility, color or transform as rendered changed: what's drawn of it has to be gathered again. From any thread,
    /// after the change
    /// </summary>
    protected virtual void OnDrawnChanged() {}

    // Compared by their bits, so even a change only a NaN's payload or a zero's sign tells apart counts
    private static bool SameBits<T>(in T a, in T b) where T : unmanaged
    {
        return MemoryMarshal.AsBytes(new ReadOnlySpan<T>(in a)).SequenceEqual(MemoryMarshal.AsBytes(new ReadOnlySpan<T>(in b)));
    }

    protected virtual void UpdateSelf(float delta) {}

    public Renderable? Parent
    {
        get => _parent;
        set
        {
            _parent = value;
            Interlocked.Increment(ref _parentsVersion);
            UpdateTransform();
        }
    }

    private static int _parentsVersion;

    /// <summary>
    /// Changes whenever a renderable gets another parent, what cached walks up the scene go by
    /// </summary>
    internal static int ParentsVersion => Volatile.Read(ref _parentsVersion);

    internal Renderable? CachedLightAnchor { get; set; }
    internal int LightAnchorVersion { get; set; } = -1;

    /// <summary>
    /// As a light anchor (<see cref="LightAnchor"/>): the light set it was given in the frame's instance buffer, and the lights at
    /// it the last time they were gathered
    /// </summary>
    internal ref AnchorLighting AnchorLight => ref _anchorLight;

    private AnchorLighting _anchorLight;

    internal struct AnchorLighting
    {
        public Buffers.InstanceBuffer? Buffer;
        public int Frame;
        public int Index;
        public SceneLights? Lights;
        public vec3 Position;
        public LightSet Set;
    }

    /// <summary>
    /// Shows what's about to be placed: its meshes and those under it are drawn after the scene, seen through what's in front of them
    /// (PreviewPass)
    /// </summary>
    public bool DrawsAsPreview { get; init; }

    public bool IsInPreview => DrawsAsPreview || _parent?.IsInPreview == true;

    public sealed override Int32 GetHashCode()
    {
        // ReSharper disable once BaseObjectGetHashCodeCallInGetHashCode
        return base.GetHashCode();
    }
    
    private void UpdateTransform()
    {
        _cachedWorldTransform = CalculateWorldTransform();
        // Only what changed has its render transform made again, the whole scene every frame took a millisecond
        QueueRenderTransform();
        foreach (var child in ChildArray)
        {
            child.UpdateTransform();
        }
    }

    protected bool InheritsScale => _inheritScale;

    protected virtual mat4 CalculateWorldTransform()
    {
        // This is some horrible logic and nesting but trust me this is for optimization purposes to reduce matrix decomposition
        if (_inheritScale && _inheritRotation)
        {
            return _parent != null ? _parent.WorldTransform * _localTransform : _localTransform;
        }

        if (_parent == null)
        {
            return _localTransform;
        }

        var parentTranslation = mat4.Translate(_parent.GetPosition());
        if (!_inheritRotation && !_inheritScale)
        {
            return parentTranslation * _localTransform;
        }

        var rotation = quat.Identity;
        if (_inheritRotation)
        {
            rotation = _parent.GetRotationQuat();
        }

        var scale = vec3.Ones;
        if (_inheritScale)
        {
            scale = _parent.GetScale();
        }

        return parentTranslation * rotation.ToMat4 * mat4.Scale(scale) * _localTransform;
    }

    public vec4 Diffuse
    {
        get => _diffuse;
        set
        {
            var changed = !SameBits(_diffuse, value);
            _diffuse = value;
            if (changed)
            {
                OnDrawnChanged();
            }

            foreach (var child in ChildArray)
            {
                if (child._inheritDiffuse)
                {
                    child.Diffuse = value;
                }
            }
        }
    }

    public mat4 RenderTransform => _cachedRenderTransform;
    public IReadOnlyList<Renderable> Children => ChildArray;

    // The children as they were last changed. Children get added and removed on the render thread while the UI thread goes through them
    // (visibility, transforms), so the copy is made and dropped under the same lock
    private Renderable[] ChildArray
    {
        get
        {
            var snapshot = _childrenSnapshot;
            if (snapshot != null)
            {
                return snapshot;
            }

            lock (_children)
            {
                return _childrenSnapshot ??= _children.Values.ToArray();
            }
        }
    }

    public string Name { get; }

    public virtual bool DoesUpdates => false;
    
    public bool CanUpdate
    {
        get => _canUpdate;
        set
        {
            _canUpdate = value;
            foreach (var child in ChildArray)
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
            var changed = _isVisible != value;
            _isVisible = value;
            if (changed)
            {
                OnDrawnChanged();
            }

            foreach (var child in ChildArray)
            {
                if (child._inheritVisibility)
                {
                    child.IsVisible = value;
                }
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