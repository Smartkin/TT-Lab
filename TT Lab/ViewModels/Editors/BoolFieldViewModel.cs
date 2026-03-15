using System;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using TT_Lab.ViewModels.Editors.PropertyGraph;

namespace TT_Lab.ViewModels.Editors;

public class BoolFieldViewModel(DocumentViewModel document, PropertyNode node, params DocumentNodeViewModel[] dependencies) : DocumentDataViewModel<bool>(document, node, dependencies);