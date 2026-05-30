using System;
using Avalonia.Controls;
using Silk.NET.Input;
using Silk.NET.Windowing;
using TT_Lab.Controls;

namespace TT_Lab.Rendering.Input;

public class LabInputPlatform : IInputPlatform
{
    public Boolean IsApplicable(IView view) => view is Renderer;

    public IInputContext CreateInput(IView view) => throw new NotImplementedException();
    public IInputContext CreateInput(IView view, Viewport renderArea) => new LabInputContext(view, renderArea);
}