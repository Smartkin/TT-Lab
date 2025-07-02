using Caliburn.Micro;
using TT_Lab.Assets;
using TT_Lab.Project.Messages;

namespace TT_Lab.ViewModels.Interfaces;

public interface ILabManager : IHandle<ProjectManagerMessage>
{
    void SaveProject();
    void OpenEditor(IAsset asset);
    void BuildPs2();
    void BuildPs2Iso();
}