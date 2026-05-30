using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using AvaloniaEdit.TextMate;
using TextMateSharp.Grammars;
using TT_Lab.ViewModels.Editors.Code.Behaviour;

namespace TT_Lab.Views.Editors.Code.Behaviour;

public partial class BehaviourCommandPackView : BurnBridgeControl<BehaviourCommandPackViewModel>
{
    public BehaviourCommandPackView()
    {
        InitializeComponent();

        var registryOptions = new RegistryOptions(ThemeName.DarkPlus);
        var textMateInstallation = Editor.InstallTextMate(registryOptions);
        textMateInstallation.SetGrammar(registryOptions.GetScopeByExtension(".cs"));
    }
}