using Caliburn.Micro;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TT_Lab.Command;
using TT_Lab.Project;
using TT_Lab.Services;

namespace TT_Lab.ViewModels;

public class ProjectCreationViewModel : Screen, INotifyDataErrorInfo
{
    private string _projectName = "New project";
    private string _projectPath = Preferences.GetPreference<string>(Preferences.ProjectsPath);
    private string _ps2DiscContentPath = Preferences.GetPreference<string>(Preferences.Ps2DiscContentPath);
    private string _xboxDiscContentPath = ""; // TODO: Add back in when XBox builds are gonna be supported
    private Boolean _copyDiscContents = true;
    private readonly IWindowManager _windowManager;
    private readonly ProjectManager _projectManager;
    private readonly IDataValidatorService _dataValidatorService;

    private readonly Dictionary<String, Func<Boolean>> _discContentIsCurrentValidMap = new();

    const Int32 PROJECT_NAME_LIMIT = 32;
    const String PROJECT_NAME_INVALID_CHARS_ERROR = "Project name must not contain invalid characters";
    const String PROJECT_NAME_EMPTY_ERROR = "Project name must not be empty";
    const String PROJECT_NAME_TOO_LONG_ERROR = "Project name must be less than 32 characters long";
    const String PROJECT_PATH_EMPTY_ERROR = "Project path must not be empty";
    const String PROJECT_WITH_THIS_NAME_IN_THIS_FOLDER_ALREADY_EXISTS_ERROR = "Project with this name in the chosen folder already exists";
    const String DISC_CONTENT_PATH_EMPTY_ERROR = "PS2 and XBox disc content paths must not be both empty";
    const String PROJECT_ALREADY_EXISTS_IN_THAT_PATH = "Project files on this path already exist";

    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged
    {
        add => _dataValidatorService.ErrorsChanged += value;
        remove => _dataValidatorService.ErrorsChanged -= value;
    }

    public ProjectCreationViewModel(IWindowManager windowManager, ProjectManager projectManager, IDataValidatorService validatorService)
    {
        _dataValidatorService = validatorService;
        _windowManager = windowManager;
        _projectManager = projectManager;
        _discContentIsCurrentValidMap.Add(nameof(PS2DiscContentPath), IsPs2DiscContentPathValid);
        _discContentIsCurrentValidMap.Add(nameof(XboxDiscContentPath), IsXboxDiscContentPathValid);
        
        _dataValidatorService.RegisterProperty<string>(nameof(PS2DiscContentPath), (newVal) => IsDiscContentPathValid(nameof(PS2DiscContentPath), newVal));
        _dataValidatorService.RegisterProperty<string>(nameof(XboxDiscContentPath), (newVal) => IsDiscContentPathValid(nameof(XboxDiscContentPath), newVal));
        _dataValidatorService.RegisterProperty<string>(nameof(ProjectName), IsProjectNameValid);
        _dataValidatorService.RegisterProperty<string>(nameof(ProjectPath), IsProjectPathValid);
    }

    public ICommand SetProjectPathCommand => new SelectFolderCommand(null, this, nameof(ProjectPath));

    public ICommand SetPS2DiscContentPathCommand => new SelectFolderCommand(null, this, nameof(PS2DiscContentPath));

    public ICommand SetXboxDiscContentPathCommand => new SelectFolderCommand(null, this, nameof(XboxDiscContentPath));

    protected override void OnViewReady(object view)
    {
        _dataValidatorService.ValidateProperty(ProjectName, nameof(ProjectName));
        _dataValidatorService.ValidateProperty(ProjectPath, nameof(ProjectPath));
        if (_dataValidatorService.ValidateProperty(PS2DiscContentPath, nameof(PS2DiscContentPath)))
        {
            return;
        }
            
        _dataValidatorService.ValidateProperty(XboxDiscContentPath, nameof(XboxDiscContentPath));
    }

    public Task Create()
    {
#if !DEBUG
            try
            {
#endif
        if (_projectManager.OpenedProject != null)
        {
            _projectManager.CloseProject();
        }
        _projectManager.CreateProject(ProjectName.Trim(), ProjectPath, PS2DiscContentPath, XboxDiscContentPath, CopyDiscContents);

#if !DEBUG
            }
            catch (Exception ex)
            {
                Log.WriteLine($"Error creating project: {ex.Message}");
            }
#endif
        return TryCloseAsync();
    }

    public Boolean IsProjectNameValid(String projectName)
    {
        var isValid = true;

        if (String.IsNullOrEmpty(projectName.Trim()))
        {
            _dataValidatorService.AddError(nameof(ProjectName), PROJECT_NAME_EMPTY_ERROR);
            isValid = false;
        }
        else
        {
            _dataValidatorService.RemoveError(nameof(ProjectName), PROJECT_NAME_EMPTY_ERROR);
        }

        if (projectName.Length > PROJECT_NAME_LIMIT)
        {
            _dataValidatorService.AddError(nameof(ProjectName), PROJECT_NAME_TOO_LONG_ERROR);
            isValid = false;
        }
        else
        {
            _dataValidatorService.RemoveError(nameof(ProjectName), PROJECT_NAME_TOO_LONG_ERROR);
        }

        if (projectName.IndexOfAny(Path.GetInvalidFileNameChars()) != -1)
        {
            _dataValidatorService.AddError(nameof(ProjectName), PROJECT_NAME_INVALID_CHARS_ERROR);
            isValid = false;
        }
        else
        {
            _dataValidatorService.RemoveError(nameof(ProjectName), PROJECT_NAME_INVALID_CHARS_ERROR);
        }
            
        if (!string.IsNullOrEmpty(projectName) && Directory.Exists(ProjectPath + "\\" + projectName))
        {
            _dataValidatorService.AddError(nameof(ProjectName), PROJECT_WITH_THIS_NAME_IN_THIS_FOLDER_ALREADY_EXISTS_ERROR);
            isValid = false;
        }
        else
        {
            _dataValidatorService.RemoveError(nameof(ProjectName), PROJECT_WITH_THIS_NAME_IN_THIS_FOLDER_ALREADY_EXISTS_ERROR);
        }

        return isValid;
    }

    public Boolean IsProjectPathValid(String path)
    {
        if (String.IsNullOrEmpty(path))
        {
            _dataValidatorService.AddError(nameof(ProjectPath), PROJECT_PATH_EMPTY_ERROR);
            return false;
        }

        if (Directory.Exists(path))
        {
            var projectFilesCheck = Directory.GetFiles(path, "*.tson", SearchOption.TopDirectoryOnly);
            if (projectFilesCheck.Length > 0)
            {
                _dataValidatorService.AddError(nameof(ProjectPath), PROJECT_ALREADY_EXISTS_IN_THAT_PATH);
                return false;
            }

            projectFilesCheck = Directory.GetFiles(path, "*.xson", SearchOption.TopDirectoryOnly);
            if (projectFilesCheck.Length > 0)
            {
                _dataValidatorService.AddError(nameof(ProjectPath), PROJECT_ALREADY_EXISTS_IN_THAT_PATH);
                return false;
            }
        }

        _dataValidatorService.RemoveError(nameof(ProjectPath));
        return true;
    }

    public Boolean IsDiscContentPathValid(String otherContentPathProperty, String discContentPath)
    {
        Debug.Assert(_discContentIsCurrentValidMap.ContainsKey(otherContentPathProperty), "Invalid property name passed to check!");

        if (!_discContentIsCurrentValidMap[otherContentPathProperty]() && String.IsNullOrEmpty(discContentPath))
        {
            _dataValidatorService.AddError(nameof(PS2DiscContentPath), DISC_CONTENT_PATH_EMPTY_ERROR);
            _dataValidatorService.AddError(nameof(XboxDiscContentPath), DISC_CONTENT_PATH_EMPTY_ERROR);
            return false;
        }

        _dataValidatorService.RemoveError(nameof(PS2DiscContentPath));
        _dataValidatorService.RemoveError(nameof(XboxDiscContentPath));
        return true;
    }

    public Boolean IsPs2DiscContentPathValid()
    {
        return !String.IsNullOrEmpty(PS2DiscContentPath);
    }

    public Boolean IsXboxDiscContentPathValid()
    {
        return !String.IsNullOrEmpty(XboxDiscContentPath);
    }

    public IEnumerable GetErrors(String? propertyName)
    {
        return _dataValidatorService.GetErrors(propertyName);
    }

    public bool CanCreate => !HasErrors;

    public string ProjectName
    {
        get => _projectName;
        set
        {
            _dataValidatorService.ValidateProperty(value, nameof(ProjectName));
            _projectName = value;
            _dataValidatorService.ValidateProperty(ProjectPath, nameof(ProjectPath));
            NotifyOfPropertyChange(nameof(ProjectName));
            NotifyOfPropertyChange(nameof(ProjectPath));
            NotifyOfPropertyChange(nameof(CanCreate));
        }
    }

    public string ProjectPath
    {
        get => _projectPath;
        set
        {
            _dataValidatorService.ValidateProperty(value);
            Preferences.SetPreference(Preferences.ProjectsPath, value);
            _projectPath = value;
            _dataValidatorService.ValidateProperty(ProjectName, nameof(ProjectName));
            NotifyOfPropertyChange(nameof(ProjectPath));
            NotifyOfPropertyChange(nameof(ProjectName));
            NotifyOfPropertyChange(nameof(CanCreate));
        }
    }

    public string PS2DiscContentPath
    {
        get => _ps2DiscContentPath;
        set
        {
            _dataValidatorService.ValidateProperty(value);
            Preferences.SetPreference(Preferences.Ps2DiscContentPath, value);
            _ps2DiscContentPath = value;
            NotifyOfPropertyChange(nameof(PS2DiscContentPath));
            NotifyOfPropertyChange(nameof(CanCreate));
        }
    }

    public string XboxDiscContentPath
    {
        get => _xboxDiscContentPath;
        set
        {
            _dataValidatorService.ValidateProperty(value);
            Preferences.SetPreference(Preferences.XboxDiscContentPath, value);
            _xboxDiscContentPath = value;
            NotifyOfPropertyChange(nameof(XboxDiscContentPath));
            NotifyOfPropertyChange(nameof(CanCreate));
        }
    }

    public Boolean CopyDiscContents
    {
        get => _copyDiscContents;
        set
        {
            _copyDiscContents = value;
            NotifyOfPropertyChange();
        }
    }

    public Boolean HasErrors => _dataValidatorService.HasErrors;
}