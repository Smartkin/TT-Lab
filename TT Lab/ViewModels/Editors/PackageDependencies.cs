using System;
using System.Collections;
using System.Linq;
using Splat;
using TT_Lab.Assets;

namespace TT_Lab.ViewModels.Editors;

/// <summary>
/// A package's dependency added and saved right away: through the package's tab when one has it open, which shows it, else into its file.
/// Link fields look at their packages again
/// </summary>
internal static class PackageDependencies
{
    public static event Action? Changed;

    public static void Add(Package package, Package dependency)
    {
        var document = Locator.Current.GetService<EditorsViewModel>()?.ResourcesEditorsViewModel.Tabs.FirstOrDefault(tab => tab.EditableResource == package.URI)?.Document;
        if (document?.PropertyGraph.Find($"Root.{nameof(Package.Dependencies)}") is { } dependencies && dependencies.GetValue() is IList list)
        {
            dependencies.InsertElement(list.Count, dependency.URI);
            document.Save();
        }
        else
        {
            package.AddDependency(dependency.URI);
            package.Serialize();
        }

        Changed?.Invoke();
    }
}
