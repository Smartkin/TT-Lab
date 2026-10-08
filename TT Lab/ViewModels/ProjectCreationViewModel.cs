using Caliburn.Micro;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TT_Lab.Command;
using TT_Lab.Project;
using TT_Lab.Services;
using TT_Lab.Util;

namespace TT_Lab.ViewModels;

public class ProjectCreationViewModel : Screen, INotifyDataErrorInfo
{
    private string _projectName = "New project";
    private string _projectPath = Preferences.GetPreference<string>(Preferences.ProjectsPath);
    private string _ps2DiscContentPath = Preferences.GetPreference<string>(Preferences.Ps2DiscContentPath);
    // Only picked when wanted, a remembered path would add the Xbox version to every new project
    private string _xboxDiscContentPath = "";
    private Boolean _copyDiscContents = true;
    private readonly IWindowManager _windowManager;
    private readonly ProjectManager _projectManager;
    private readonly IDataValidatorService _dataValidatorService;

    private enum DiscContentsStatus
    {
        Valid,
        Empty,
        Invalid
    }

    private readonly Dictionary<String, Func<string, DiscContentsStatus>> _discContentIsCurrentValidMap = new();

    const Int32 PROJECT_NAME_LIMIT = 32;
    const String PROJECT_NAME_INVALID_CHARS_ERROR = "Project name must not contain invalid characters";
    // The project's packages are named after it
    const String PROJECT_NAME_NOT_ASCII_ERROR = "Project name " + NameRules.AsciiOnly;
    const String PROJECT_NAME_EMPTY_ERROR = "Project name must not be empty";
    const String PROJECT_NAME_TOO_LONG_ERROR = "Project name must be less than 32 characters long";
    const String PROJECT_PATH_EMPTY_ERROR = "Project path must not be empty";
    const String PROJECT_WITH_THIS_NAME_IN_THIS_FOLDER_ALREADY_EXISTS_ERROR = "Project with this name in the chosen folder already exists";
    const String DISC_CONTENT_PATH_EMPTY_ERROR = "PS2 and XBox disc content paths must not be both empty";
    const String DISC_CONTENT_INVALID_CONTENTS = "PS2 and XBox disc content paths must contain valid files";
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
        _dataValidatorService.ValidateProperty(PS2DiscContentPath, nameof(PS2DiscContentPath));
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
        return this.DeactivateAsync(true);
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

        if (!NameRules.IsAscii(projectName))
        {
            _dataValidatorService.AddError(nameof(ProjectName), PROJECT_NAME_NOT_ASCII_ERROR);
            isValid = false;
        }
        else
        {
            _dataValidatorService.RemoveError(nameof(ProjectName), PROJECT_NAME_NOT_ASCII_ERROR);
        }
            
        if (!string.IsNullOrEmpty(projectName) && Directory.Exists(ProjectPath + "/" + projectName))
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

    // Either version's disc is enough, a path that's given has to hold that version's files
    private Boolean IsDiscContentPathValid(String changedProperty, String discContentPath)
    {
        Debug.Assert(_discContentIsCurrentValidMap.ContainsKey(changedProperty), "Invalid property name passed to check!");

        var statuses = new Dictionary<String, DiscContentsStatus>
        {
            [nameof(PS2DiscContentPath)] = IsPs2DiscContentPathValid(changedProperty == nameof(PS2DiscContentPath) ? discContentPath : PS2DiscContentPath),
            [nameof(XboxDiscContentPath)] = IsXboxDiscContentPathValid(changedProperty == nameof(XboxDiscContentPath) ? discContentPath : XboxDiscContentPath)
        };
        var bothEmpty = statuses.Values.All(status => status == DiscContentsStatus.Empty);
        foreach (var (property, status) in statuses)
        {
            _dataValidatorService.RemoveError(property);
            if (bothEmpty)
            {
                _dataValidatorService.AddError(property, DISC_CONTENT_PATH_EMPTY_ERROR);
            }
            else if (status == DiscContentsStatus.Invalid)
            {
                _dataValidatorService.AddError(property, DISC_CONTENT_INVALID_CONTENTS);
            }
        }

        return !bothEmpty && statuses[changedProperty] != DiscContentsStatus.Invalid;
    }

    private DiscContentsStatus IsPs2DiscContentPathValid(string newVal)
    {
        if (String.IsNullOrEmpty(newVal))
        {
            return DiscContentsStatus.Empty;
        }

        if (!CheckForFile(newVal, "System.cnf"))
        {
            return DiscContentsStatus.Invalid;
        }
        
        return DiscContentsStatus.Valid;
    }

    private DiscContentsStatus IsXboxDiscContentPathValid(string newVal)
    {
        if (String.IsNullOrEmpty(newVal))
        {
            return DiscContentsStatus.Empty;
        }
        
        if (!CheckForFile(newVal, "Default.xbe"))
        {
            return DiscContentsStatus.Invalid;
        }
        
        return DiscContentsStatus.Valid;
    }

    private static bool CheckForFile(string path, string fileName)
    {
        return Directory.Exists(path) && Directory.EnumerateFiles(path).Any(file => Path.GetFileName(file).Equals(fileName, StringComparison.OrdinalIgnoreCase));
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