using Caliburn.Micro;

namespace TT_Lab.ViewModels.Interfaces;

public interface IDirtyMarker : INotifyPropertyChangedEx
{
    void ResetDirty();
    
    int GetStorageHash() { return GetHashCode(); }
    
    bool IsDirty { get; }
}