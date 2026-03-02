using System.Linq;
using Caliburn.Micro;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Dock.Model.Core;
using TT_Lab.Project;
using TT_Lab.Project.Messages;
using TT_Lab.ViewModels.Composite;

namespace TT_Lab.ViewModels;

public sealed class ResourcesEditorsViewModel(IFactory factory) : EditorsViewerViewModel(factory);