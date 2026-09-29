using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace TT_Lab.Controls;

// The one scroll viewer of a document's editors, in a document tab and in the inspector: everything under it expands downwards
// and nothing inside it scrolls on its own but custom editors that lay out on their own (code, font pages, animation lists)
public class DocumentScrollViewer : ScrollViewer
{
    protected override System.Type StyleKeyOverride => typeof(ScrollViewer);

    public DocumentScrollViewer()
    {
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        BringIntoViewOnFocusChange = false;
    }
}
