using System.Reactive.Disposables;
using Avalonia.Controls;
using Avalonia.Interactivity;
using TT_Lab.ViewModels.Editors.Instance;

namespace TT_Lab.Views.Editors.Instance;

public partial class ParticleTextureView : DocumentBaseView<ParticleTextureViewModel>
{
    // Keeps state of its own about what it shows
    public override bool CanBeRecycled => false;

    public ParticleTextureView()
    {
        InitializeComponent();
        PageEditor.DragStarted += () => ViewModel?.BeginDrag();
        PageEditor.RectDragged += rect => ViewModel?.DragRect(rect);
        PageEditor.DragEnded += () => ViewModel?.EndDrag();
        PageEditor.SpritePicked += sprite => ViewModel?.PickSprite(sprite);
        PageEditor.HoveredSpriteChanged += sprite => ViewModel?.HoverSprite(sprite);
    }

    protected override void HandleActivation(CompositeDisposable disposables)
    {
    }

    private void PageClicked(object? sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is ParticlePageChoice choice)
        {
            ViewModel?.ChoosePage(choice.Index);
        }
    }
}
