using System;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading;
using AvaloniaEdit.Document;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Extensions;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using Twinsanity.AgentLab;

namespace TT_Lab.ViewModels.Editors;

public partial class CodeEditorViewModel(DocumentViewModel document, PropertyNode code, params DocumentNodeViewModel[] dependencies) : DocumentDataViewModel<string>(document, code, dependencies)
{
    [Reactive]
    private TextDocument _code;
    
    private bool _canClose = true;

    protected override void ApplyValidationRules(CompositeDisposable disposables)
    {
        base.ApplyValidationRules(disposables);
        
        if (!GetEditorParameter(ValidateAgentLabCode, false))
        {
            return;
        }
        
        this.ValidationRule(x => x.Code.Text, code =>
        {
            if (string.IsNullOrEmpty(code))
            {
                return true;
            }
            
            var agentLabParser = new AgentLabParser(new AgentLabLexer(code));
            try
            {
                agentLabParser.Parse();
            }
            catch (Exception e)
            {
                _parseError = e.Message;
                return false;
            }

            return true;
        }, x => $"Script error: {_parseError}").DisposeWith(FullDeactivationDisposables);
            
        this.IsValid().Subscribe(x =>
        {
            _canClose = x;
        }).DisposeWith(FullDeactivationDisposables);
    }

    protected override void OnInitialized(CompositeDisposable disposables)
    {
        base.OnInitialized(disposables);

        RxSchedulers.MainThreadScheduler.Schedule(this, (_, viewModel) =>
        {
            Code = new TextDocument(CurrentValue);
            return viewModel.WhenAnyValue(x => x.Code.Text).ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(code =>
                {
                    SetValueCommand.Execute(code);
                });
        }).DisposeWith(disposables);
    }

    public override Boolean CanClose()
    {
        return _canClose;
    }

    private string _parseError = string.Empty;

    public const string ValidateAgentLabCode = "CODE_EDITOR_VALIDATE_AGENT_LAB_CODE";
}