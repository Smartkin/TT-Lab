using System;
using System.Collections.Generic;
using TT_Lab.Util;

namespace TT_Lab.Command
{
    public class OpenProjectDialogueCommand : ICommand
    {
        public event EventHandler? CanExecuteChanged;

        public Boolean CanExecute(Object? parameter)
        {
            return true;
        }

        public void Execute(Object? parameter = null)
        {
            List<string>? recents = null;
            var proj = MiscUtils.GetFileFromDialogue("PS2 TT Lab WPF Project|*.tson|XBox TT Lab WPF Project|*.xson", (recents != null && recents.Count != 0 ? recents[0] : ""));
            if (proj != string.Empty)
            {
                var open = new OpenProjectCommand(System.IO.Path.GetDirectoryName(proj)!);
                open.Execute();
            }
        }

        public void Unexecute()
        {
            throw new NotImplementedException();
        }
    }
}
