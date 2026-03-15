using System.Collections.Generic;

namespace TT_Lab.ViewModels.Interfaces;

public interface ILifecycleNode
{
    IReadOnlyList<ILifecycleNode> Dependencies { get; }

    void Initialize();
}