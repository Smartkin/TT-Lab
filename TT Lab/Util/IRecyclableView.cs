namespace TT_Lab.Util;

/// <summary>
/// A view the <see cref="ViewLocator"/> gives another view model of the same type instead of making a new view, when the content it
/// shows changes (an item scrolled into a long list's recycled row). Making the views of a row again for every row scrolled in took most
/// of the time scrolling a long list
/// </summary>
public interface IRecyclableView
{
    bool CanBeRecycled { get; }

    /// <summary>
    /// Shows the view model, set up the way a new view of it would be
    /// </summary>
    void Recycle(object viewModel);
}
