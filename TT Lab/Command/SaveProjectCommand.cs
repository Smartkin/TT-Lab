using Caliburn.Micro;
using System;
using System.Threading.Tasks;
using Splat;
using TT_Lab.Project;

namespace TT_Lab.Command
{
    public class SaveProjectCommand : ICommand
    {
        public event EventHandler? CanExecuteChanged;

        public Boolean CanExecute(Object? parameter)
        {
            return true;
        }

        public void Execute(Object? parameter = null)
        {
            var projectManager = Locator.Current.GetService<ProjectManager>()!;
            if (!projectManager.ProjectOpened) return;

            projectManager.WorkableProject = false;
            Task.Factory.StartNew(() =>
            {
                try
                {
                    Log.WriteLine($"Saving project...");
                    var now = DateTime.Now;
                    var pr = Locator.Current.GetService<ProjectManager>()!.FullProjectTree;
                    //foreach (var viewModel in pr)
                    //{
                    //    viewModel.Save(null);
                    //}
                    Log.WriteLine($"Saved project in {DateTime.Now - now}");
                }
                catch (Exception ex)
                {
                    Log.WriteLine($"Error saving project: {ex.Message}");
                }
                finally
                {
                    Locator.Current.GetService<ProjectManager>()!.WorkableProject = true;
                }
            });
        }

        public void Unexecute()
        {
            throw new NotImplementedException();
        }
    }
}
