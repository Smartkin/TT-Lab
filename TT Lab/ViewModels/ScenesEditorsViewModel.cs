using System.Linq;
using Caliburn.Micro;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Dock.Model.Core;
using TT_Lab.Project;
using TT_Lab.Project.Messages;
using TT_Lab.ViewModels.Composite;
using TT_Lab.ViewModels.Editors;

namespace TT_Lab.ViewModels;

public sealed class ScenesEditorsViewModel(IFactory factory) : EditorsViewerViewModel(factory);