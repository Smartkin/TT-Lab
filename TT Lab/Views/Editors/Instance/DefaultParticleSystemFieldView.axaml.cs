using System.Reactive.Disposables;
using Avalonia.Controls;
using TT_Lab.ViewModels.Editors.Instance;

namespace TT_Lab.Views.Editors.Instance;

public partial class DefaultParticleSystemFieldView : DocumentBaseView<DefaultParticleSystemFieldViewModel>
{
    public DefaultParticleSystemFieldView()
    {
        InitializeComponent();
        GoToSystem.Click += (_, _) => ViewModel?.GoToSystem();
        var browseFlyout = (Flyout)Browse.Flyout!;
        browseFlyout.Opening += (_, _) =>
        {
            ViewModel?.LoadChoices();
            Choices.SelectedItem = null;
        };
        browseFlyout.Opened += (_, _) => SearchBox.Focus();
        Choices.SelectionChanged += (_, _) =>
        {
            if (Choices.SelectedItem is not DefaultParticleSystemChoice choice)
            {
                return;
            }

            ViewModel?.Pick(choice);
            browseFlyout.Hide();
        };
    }

    // Everything's bound in the markup
    protected override void HandleActivation(CompositeDisposable disposables)
    {
    }
}
