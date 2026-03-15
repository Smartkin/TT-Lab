using System;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using TT_Lab.ViewModels.Interfaces;

namespace TT_Lab.ViewModels.Editors;

public class DocumentRootViewModel(
    DocumentViewModel document,
    PropertyNode rootNode,
    params DocumentNodeViewModel[] dependencies)
    : DocumentModelViewModel(document, rootNode, dependencies);