using System;
using System.Windows.Controls;
using Silk.NET.Input;
using Silk.NET.Windowing;

namespace TT_Lab.Rendering.Input;

public class LabInputPlatform : IInputPlatform
{
    public Boolean IsApplicable(IView view) => view is Renderer;

    public IInputContext CreateInput(IView view) => throw new NotImplementedException();
    public IInputContext CreateInput(IView view, Image renderArea) => new LabInputContext(view, renderArea);
}