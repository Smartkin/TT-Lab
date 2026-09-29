using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ReactiveUI;
using ReactiveUI.Avalonia;
using TT_Lab.Util;
using TT_Lab.ViewModels.Editors;

namespace TT_Lab.Views.Editors;

[ExcludeFromViewRegistration]
public abstract class DocumentBaseView<T> : ReactiveUserControl<T>, IRecyclableView where T : DocumentBaseViewModel
{
    // Each try brings the node into view of at least one more of the nested scroll viewers around it
    private const int MaxBringIntoViewTries = 8;

    private CompositeDisposable? _activation;

    protected DocumentBaseView()
    {
        this.WhenActivated(disposables =>
        {
            Activate();
            Disposable.Create(this, view => view.Deactivate()).DisposeWith(disposables);
        });
    }

    /// <summary>
    /// Whether the view can show another view model of its type in place of the one it shows, like the rows of a long list get when
    /// scrolling. Editors keeping state of their own about what they show can't
    /// </summary>
    public virtual bool CanBeRecycled => true;

    /// <summary>
    /// Whether the view's bindings get made again for a recycled view model, like a new view's, after the old ones are gone. ReactiveUI's
    /// bindings follow the view model on their own and making them again costs the most of a recycled row, so only views whose activation
    /// decides something from the view model, or whose controls reset while the view model changes (a combo box's items) and would write
    /// what they reset to into it through a two way binding, ask for it
    /// </summary>
    protected virtual bool RebindsOnRecycle => false;

    public void Recycle(object viewModel)
    {
        if (!RebindsOnRecycle || _activation == null)
        {
            ViewModel = (T)viewModel;
            return;
        }

        Deactivate();
        ViewModel = (T)viewModel;
        Activate();
    }

    protected abstract void HandleActivation(CompositeDisposable disposables);

    private void Activate()
    {
        Deactivate();
        var disposables = new CompositeDisposable();
        _activation = disposables;
        this.OneWayBind(ViewModel, x => x.IsVisible, view => view.IsVisible).DisposeWith(disposables);
        this.OneWayBind(ViewModel, x => x.CanWrite, view => view.IsEnabled).DisposeWith(disposables);
        this.OneWayBind(ViewModel, x => x.DepthDependentBrush, view => view.Background).DisposeWith(disposables);

        HandleActivation(disposables);
    }

    private void Deactivate()
    {
        _activation?.Dispose();
        _activation = null;
    }

    /// <summary>
    /// Scrolls to the node once the composite's items are laid out, the item may not have a container yet in a virtualized list
    /// </summary>
    protected static void BringNodeIntoView(ItemsControl items, IList<DocumentNodeViewModel> nodes, NodeScrollRequest? request)
    {
        if (request == null)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            // A scroll viewer only scrolls when the ones inside it already show the node and positions of items that weren't realized are
            // estimates, so it takes a few tries with a layout pass after each
            for (var tries = 0; tries < MaxBringIntoViewTries; tries++)
            {
                var index = nodes.IndexOf(request.Node);
                if (index < 0 || !items.IsEffectivelyVisible || TopLevel.GetTopLevel(items) == null)
                {
                    return;
                }

                var container = items.ContainerFromIndex(index);
                if (container != null && IsShown(container, request.IsTarget))
                {
                    return;
                }

                if (container == null)
                {
                    items.ScrollIntoView(index);
                    container = items.ContainerFromIndex(index);
                }

                if (container != null)
                {
                    container.BringIntoView(WithMargin(container, GetPartToShow(container, request.IsTarget)));
                }

                items.UpdateLayout();
            }
        }, DispatcherPriority.Background);
    }

    /// <summary>
    /// Whether all scroll viewers around the container show it. The node being revealed has to be shown entirely, the ones leading to it
    /// only need to be partly in sight for the virtualized lists to realize what's inside them
    /// </summary>
    private static bool IsShown(Control container, bool entirely)
    {
        var part = GetPartToShow(container, entirely);
        foreach (var presenter in container.GetVisualAncestors().OfType<ScrollContentPresenter>())
        {
            if (container.TransformToVisual(presenter) is not { } transform)
            {
                return false;
            }

            var rect = part.TransformToAABB(transform);
            var viewport = new Rect(presenter.Bounds.Size);
            if (entirely ? !viewport.Inflate(1.0).Contains(rect) : !viewport.Intersects(rect))
            {
                return false;
            }
        }

        return true;
    }

    // Of something taller than the viewports only the top can be shown, and of something wider only its left edge where its caption is
    private static Rect GetPartToShow(Control container, bool isTarget)
    {
        if (!isTarget)
        {
            return new Rect(container.Bounds.Size);
        }

        return new Rect(0.0, 0.0, Math.Min(container.Bounds.Width, 1.0), Math.Min(container.Bounds.Height, GetSmallestViewportHeight(container)));
    }

    // Some room around what's brought into view, so it doesn't end up pressed against the edge where it's easily missed
    private static Rect WithMargin(Control container, Rect part)
    {
        var margin = Math.Clamp((GetSmallestViewportHeight(container) - part.Height) / 2.0, 0.0, 40.0);
        return part.Inflate(new Thickness(0.0, margin));
    }

    private static double GetSmallestViewportHeight(Control container)
    {
        return container.GetVisualAncestors().OfType<ScrollContentPresenter>()
            .Select(presenter => presenter.Bounds.Height)
            .DefaultIfEmpty(container.Bounds.Height)
            .Min();
    }
}
