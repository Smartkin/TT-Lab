using System.Collections.Generic;
using System.Linq;
using TT_Lab.ViewModels.Interfaces;

namespace TT_Lab.ViewModels;

public sealed class ReactiveLifecycle
{
    private readonly List<ILifecycleNode> _nodes = [];
    private readonly HashSet<ILifecycleNode> _initializedNodes = [];

    public void Register(ILifecycleNode node)
    {
        _nodes.Add(node);
    }

    public void Initialize()
    {
        while (_initializedNodes.Count < _nodes.Count)
        {
            var readyNodes = _nodes.Where(n => !_initializedNodes.Contains(n))
                .Where(n => n.Dependencies.All(d => _initializedNodes.Contains(d)))
                .ToList();

            if (readyNodes.Count == 0)
            {
                Log.WriteLine("Circular dependency detected", Log.LogType.Error);
                break;
            }

            foreach (var node in readyNodes)
            {
                node.Initialize();
                _initializedNodes.Add(node);
            }
        }
    }
}