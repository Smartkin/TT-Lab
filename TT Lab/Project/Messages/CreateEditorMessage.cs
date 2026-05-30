using System;
using TT_Lab.Assets;
using TT_Lab.ViewModels.Interfaces;

namespace TT_Lab.Project.Messages;

public class CreateEditorMessage<T> where T : IEditorViewModel
{
    public CreateEditorMessage(IAsset asset)
    {
        Asset = asset;
    }

    public IAsset Asset { get; set; }
}