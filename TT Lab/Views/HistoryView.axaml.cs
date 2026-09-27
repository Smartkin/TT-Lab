using System;
using ReactiveUI;
using ReactiveUI.Avalonia;
using TT_Lab.ViewModels;

namespace TT_Lab.Views;

public partial class HistoryView : ReactiveUserControl<HistoryViewModel>
{
    public HistoryView()
    {
        InitializeComponent();
        // The step the document is at stays in sight as the history grows
        this.WhenAnyValue(view => view.ViewModel!.SelectedEntry).Subscribe(entry =>
        {
            if (entry != null)
            {
                EntriesList.ScrollIntoView(entry);
            }
        });
    }
}
