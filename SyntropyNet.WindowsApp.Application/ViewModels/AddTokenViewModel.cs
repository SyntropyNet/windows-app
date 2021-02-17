using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyntropyNet.WindowsApp.Application.ViewModels
{
    public class AddTokenViewModel : BindableBase, IDialogAware, INotifyDataErrorInfo
    {

        private readonly Dictionary<string, List<string>> _errorsByPropertyName = new Dictionary<string, List<string>>();
        public string Title => "Add Agent Token";
        public bool HasErrors => _errorsByPropertyName.Any();
        public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

        public IEnumerable GetErrors(string propertyName)
        {
            return _errorsByPropertyName.ContainsKey(propertyName) ?
                _errorsByPropertyName[propertyName] : null;
        }

        private void OnErrorsChanged(string propertyName)
        {
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
        }

        private void AddError(string propertyName, string error)
        {
            if (!_errorsByPropertyName.ContainsKey(propertyName))
                _errorsByPropertyName[propertyName] = new List<string>();

            if (!_errorsByPropertyName[propertyName].Contains(error))
            {
                _errorsByPropertyName[propertyName].Add(error);
                OnErrorsChanged(propertyName);
            }
        }
        private void ClearErrors(string propertyName)
        {
            if (_errorsByPropertyName.ContainsKey(propertyName))
            {
                _errorsByPropertyName.Remove(propertyName);
                OnErrorsChanged(propertyName);
            }
        }

        public void ValidateName(string val)
        {
            ClearErrors(nameof(Name));
            if (string.IsNullOrEmpty(val))
            {
                AddError(nameof(Name), "Name is required.");
            }
        }

        public void ValidateAgentToken(string val)
        {
            ClearErrors(nameof(AgentToken));
            if (string.IsNullOrEmpty(val))
            {
                AddError(nameof(AgentToken), "API key is required.");
            }
        }

        private string _name = string.Empty;
        public string Name
        {
            get { return _name; }
            set 
            { 
                ValidateName(value);
                SetProperty(ref _name, value); 
            }
        }

        private string _agentToken = string.Empty;
        public string AgentToken
        {
            get { return _agentToken; }
            set 
            { 
                ValidateAgentToken(value);
                SetProperty(ref _agentToken, value); 
            }
        }

        public event Action<IDialogResult> RequestClose;

        private DelegateCommand<string> _closeDialogCommand;
        public DelegateCommand<string> CloseDialogCommand =>
            _closeDialogCommand ?? (_closeDialogCommand = new DelegateCommand<string>(CloseDialog));

        protected virtual void CloseDialog(string parameter)
        {
            ButtonResult result = ButtonResult.None;
            bool parsed;
            if (string.IsNullOrEmpty(parameter) || !bool.TryParse(parameter, out parsed) || !parsed)
            {
                result = ButtonResult.Cancel;
            }
            else
            {
                // Validate input parameters
                ValidateName(Name);
                ValidateAgentToken(AgentToken);
                if (HasErrors)
                {
                    return;
                }
                result = ButtonResult.OK;
            }

            RaiseRequestClose(new DialogResult(result, new Prism.Services.Dialogs.DialogParameters(){
                    { "Name", Name },
                    { "AgentToken", AgentToken }
                })
            );
        }

        public virtual void RaiseRequestClose(IDialogResult dialogResult)
        {
            RequestClose?.Invoke(dialogResult);
        }

        public virtual bool CanCloseDialog()
        {
            return true;
        }

        public virtual void OnDialogClosed()
        {

        }

        public virtual void OnDialogOpened(IDialogParameters parameters)
        {
        }
    }
}
