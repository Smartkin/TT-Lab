using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reflection;
using DynamicData.Kernel;
using ReactiveUI;
using TT_Lab.Attributes;
using TT_Lab.Attributes.EditorParamWrappers;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using TT_Lab.ViewModels.Interfaces;

namespace TT_Lab.ViewModels.Editors;

public class DocumentModelViewModel(
    DocumentViewModel document,
    PropertyNode property,
    params DocumentNodeViewModel[] dependencies)
    : DocumentCompositeViewModel(document, property, dependencies)
{
    public override ReactiveCommand<Unit, Unit>? AddCommand => null;
}