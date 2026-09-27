using System;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using Avalonia.Controls;
using ReactiveUI;
using ReactiveUI.Validation.Extensions;
using TT_Lab.ViewModels.Editors.Instance;

namespace TT_Lab.Views.Editors.Instance;

public partial class ParticleSystemFieldView : DocumentBaseView<ParticleSystemFieldViewModel>
{
    public ParticleSystemFieldView()
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
            if (Choices.SelectedItem is not ParticleSystemChoice choice)
            {
                return;
            }

            ViewModel?.Pick(choice);
            browseFlyout.Hide();
        };
    }

    protected override void HandleActivation(CompositeDisposable disposables)
    {
        this.Bind(ViewModel, viewModel => viewModel.Text, view => view.TextField.Text).DisposeWith(disposables);
        this.BindValidation(ViewModel, viewModel => viewModel.Text, view => view.TextFieldError.Text).DisposeWith(disposables);
    }
}
