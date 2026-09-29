using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using TT_Lab.Assets.Graphics;
using TT_Lab.ViewModels.Editors.PropertyGraph;

namespace TT_Lab.ViewModels.Editors;

public partial class RenderBucketFieldViewModel : DocumentDataViewModel<UInt32>
{
    [Reactive]
    private RenderBucket? _selectedBucket;

    public RenderBucketFieldViewModel(DocumentViewModel document, PropertyNode node, params DocumentNodeViewModel[] dependencies) : base(document, node, dependencies)
    {
        var buckets = new List<RenderBucket>(RenderBuckets.All);
        var current = RenderBuckets.Find(CurrentValue);
        // A value outside of the game's buckets is still shown, so it can be put right
        if (!buckets.Contains(current))
        {
            buckets.Add(current);
        }

        Buckets = buckets;
        _selectedBucket = current;
    }

    public IReadOnlyList<RenderBucket> Buckets { get; }

    protected override void OnActivated(CompositeDisposable disposables)
    {
        base.OnActivated(disposables);
        this.WhenAnyValue(x => x.SelectedBucket)
            .Skip(1)
            .WhereNotNull()
            .Where(bucket => bucket.Id != CurrentValue)
            .Subscribe(bucket => SetCurrentValue(bucket.Id))
            .DisposeWith(disposables);
    }

    protected override void OnCurrentValueChanged()
    {
        SelectedBucket = RenderBuckets.Find(CurrentValue);
    }
}
