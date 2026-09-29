using System;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;

namespace TT_Lab.Controls;

/// <summary>
/// An items control whose rows keep what's in them when the virtualizing panel recycles them, so a row scrolled in shows its item
/// with the views of the row scrolled out (see <see cref="Util.IRecyclableView"/>) instead of new ones. ItemsControl clears a recycled
/// row's content and template, which drops everything in it, and making it all again for every row scrolled in was what made long
/// lists slow to scroll
/// </summary>
public class RecyclingItemsControl : ItemsControl
{
    protected override Type StyleKeyOverride => typeof(ItemsControl);

    protected override void ClearContainerForItemOverride(Control container)
    {
        // Kept for the next item
    }

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        if (container is ContentPresenter presenter && container != item && ItemTemplate != null)
        {
            presenter.ContentTemplate = ItemTemplate;
            presenter.Content = item;
            return;
        }

        base.PrepareContainerForItemOverride(container, item, index);
    }
}
