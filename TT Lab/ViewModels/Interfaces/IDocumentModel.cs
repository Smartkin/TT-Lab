using System;

namespace TT_Lab.ViewModels.Interfaces;

public interface IDocumentModel : IDisposable
{
    string DocumentName { get; }

    void Save() { }

    void IDisposable.Dispose() { }
}