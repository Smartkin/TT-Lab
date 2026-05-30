using Caliburn.Micro;
using System;
using Splat;
using TT_Lab.Project;

namespace TT_Lab.Command
{
    public class OpenProjectCommand : ICommand
    {
        public event EventHandler? CanExecuteChanged;

        private string path;

        public OpenProjectCommand(string path)
        {
            this.path = path;
        }

        public Boolean CanExecute(Object? parameter)
        {
            return true;
        }

        public void Execute(Object? parameter = null)
        {
#if !DEBUG
            try
            {
#endif
            Locator.Current.GetService<ProjectManager>()!.OpenProject(path);
#if !DEBUG
            }
            catch (Exception ex)
            {
                Log.WriteLine($"Failed to open project: {ex.Message}");
            }
#endif
        }

        public void Unexecute()
        {
            throw new NotImplementedException();
        }
    }
}
