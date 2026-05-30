namespace TT_Lab.Command;

public interface ICommand : System.Windows.Input.ICommand
{
    /// <summary>
    /// Performs the command
    /// </summary>
    /// <param Name="parameter">Optional parameter</param>
    new void Execute(object? parameter = null);

    /// <summary>
    /// Undoes the performed command
    /// </summary>
    void Unexecute();
}