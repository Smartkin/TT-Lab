using Caliburn.Micro;
using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using GlmSharp;
using TT_Lab.Extensions;
using TT_Lab.Util;
using TT_Lab.ViewModels.Interfaces;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.ViewModels.Composite;

public class Matrix4ViewModel : Conductor<Vector4ViewModel>.Collection.AllActive, ISaveableViewModel<Matrix4>, IHaveChildrenEditors
{
    private Vector4ViewModel _v1;
    private Vector4ViewModel _v2;
    private Vector4ViewModel _v3;
    private Vector4ViewModel _v4;
    private mat4 _internalMatrix;
    private bool isDirty;
    private readonly DirtyTracker dirtyTracker;

    public Matrix4ViewModel()
    {
        dirtyTracker = new DirtyTracker(this);
        _internalMatrix = mat4.Identity;
        UpdateVecsByInternalMatrix();
    }

    private void VectorChanged(object? s, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IsDirty))
        {
            return;
        }

        UpdateInternalMatrix();
    }

    public Matrix4ViewModel(Matrix4 m)
    {
        dirtyTracker = new DirtyTracker(this);
        _internalMatrix = m.ToGlm();
        UpdateVecsByInternalMatrix();
    }

    public override String ToString()
    {
        return "Matrix";
    }

    public mat4 GetMatrix()
    {
        return _internalMatrix;
    }

    public Vector4ViewModel this[int key]
    {
        get
        {
            return key switch
            {
                0 => _v1,
                1 => _v2,
                2 => _v3,
                3 => _v4,
                _ => throw new IndexOutOfRangeException(),
            };
        }
        set
        {
            switch (key)
            {
                case 0:
                    _v1 = value;
                    break;
                case 1:
                    _v2 = value;
                    break;
                case 2:
                    _v3 = value;
                    break;
                case 3:
                    _v4 = value;
                    break;
            }
            throw new IndexOutOfRangeException();
        }
    }

    public vec4 Position => _internalMatrix.Column3;

    public vec3 Rotation
    {
        get
        {
            var euler = _internalMatrix.ToQuaternion.EulerAngles;
            return new vec3((float)euler.x, (float)euler.y, (float)euler.z);
        }
    }

    public void Translate(vec3 translation)
    {
        _internalMatrix = mat4.Translate(translation) * _internalMatrix;
        UpdateVecsByInternalMatrix();
        dirtyTracker.MarkDirty();
    }

    public void Rotate(quat rotation)
    {
        _internalMatrix = rotation.ToMat4 * _internalMatrix;
        UpdateVecsByInternalMatrix();
        dirtyTracker.MarkDirty();
    }

    public void Rotate(vec3 rotation)
    {
        Rotate(new quat(rotation));
    }

    public Vector4ViewModel V1 => _v1;
    public Vector4ViewModel V2 => _v2;
    public Vector4ViewModel V3 => _v3;
    public Vector4ViewModel V4 => _v4;

    public void ResetDirty()
    {
        dirtyTracker.ResetDirty();
    }

    public bool IsDirty => dirtyTracker.IsDirty;

    public void Save(Matrix4 o)
    {
        var m = o;
        _v1.Save(m.Column1);
        _v2.Save(m.Column2);
        _v3.Save(m.Column3);
        _v4.Save(m.Column4);
        
        ResetDirty();
    }

    public DirtyTracker DirtyTracker => dirtyTracker;

    private void UpdateInternalMatrix()
    {
        var newMat = new Matrix4();
        _v1.Save(newMat.Column1);
        _v2.Save(newMat.Column2);
        _v3.Save(newMat.Column3);
        _v4.Save(newMat.Column4);

        _internalMatrix = newMat.ToGlm();
        NotifyOfPropertyChange(nameof(Rotation));
        NotifyOfPropertyChange(nameof(Position));
    }

    [MemberNotNull(nameof(_v1))]
    [MemberNotNull(nameof(_v2))]
    [MemberNotNull(nameof(_v3))]
    [MemberNotNull(nameof(_v4))]
    private void UpdateVecsByInternalMatrix()
    {
        if (_v1 != null)
        {
            for (var i = 0; i < 4; i++)
            {
                this[i].PropertyChanged -= VectorChanged;
                dirtyTracker.RemoveChild(this[i]);
            }
        }

        var twinMat = _internalMatrix.ToTwin();
        _v1 = new Vector4ViewModel(twinMat.Column1);
        _v2 = new Vector4ViewModel(twinMat.Column2);
        _v3 = new Vector4ViewModel(twinMat.Column3);
        _v4 = new Vector4ViewModel(twinMat.Column4);
        for (var i = 0; i < 4; i++)
        {
            this[i].PropertyChanged += VectorChanged;
            dirtyTracker.AddChild(this[i]);
        }
        
        NotifyOfPropertyChange(nameof(V1));
        NotifyOfPropertyChange(nameof(V2));
        NotifyOfPropertyChange(nameof(V3));
        NotifyOfPropertyChange(nameof(V4));
    }
}