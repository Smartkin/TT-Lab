using System;

namespace TT_Lab.ViewModels.Interfaces;

public interface IDocumentModel : IDisposable
{
    string Name { get; }
    
    void Save();

    void IDisposable.Dispose() { }
}