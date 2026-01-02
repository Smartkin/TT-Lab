using System.Windows.Input;
using Avalonia.Input;

namespace TT_Lab.Project.Messages.Inputs
{
    public class KeyboardPressedMessage
    {
        public KeyboardPressedMessage(KeyEventArgs keyEvent)
        {
            Key = keyEvent;
        }

        public KeyEventArgs Key { get; set; }
    }
}
