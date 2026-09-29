using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using ReactiveUI.Validation.Extensions;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Instance.Particle;
using TT_Lab.Assets;
using TT_Lab.ViewModels.Editors.PropertyGraph;

namespace TT_Lab.ViewModels.Editors.Instance;

/// <summary>
/// A particle system's name, which no other system of its version of the game may have: emitters play the system they name. A name
/// another system has shows why and isn't taken
/// </summary>
public class ParticleSystemNameFieldViewModel(DocumentViewModel document, PropertyNode data, params DocumentNodeViewModel[] dependencies)
    : TextFieldViewModel(document, data, dependencies)
{
    protected override void ApplyValidationRules(CompositeDisposable disposables)
    {
        base.ApplyValidationRules(disposables);
        ParticleSystemNames.Prepare();
        this.ValidationRule(viewModel => viewModel.Text, text => FindOther(text) == null,
            text => FindOther(text) is { } other ? $"{other.Alias} has a particle system named {text} already" : string.Empty).DisposeWith(disposables);
    }

    protected override bool CanCommit(string text) => FindOther(text) == null;

    private IAsset? FindOther(string? name)
    {
        if (string.IsNullOrEmpty(name) || Property.Target is not ParticleSystem system || GetOwner() is not { } owner)
        {
            return null;
        }

        return ParticleSystemNames.FindOther(name, owner, system);
    }

    private IAsset? GetOwner()
    {
        for (var node = Property.Parent; node != null; node = node.Parent)
        {
            if (node.GetValue() is AbstractAssetData data)
            {
                return data.GetOwner();
            }
        }

        return null;
    }
}
