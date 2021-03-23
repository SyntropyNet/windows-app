using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using SyntropyNet.WindowsApp.Application.Contracts;
using SyntropyNet.WindowsApp.Application.Domain.Enums.WireGuard;
using SyntropyNet.WindowsApp.Application.Domain.Models;
using SyntropyNet.WindowsApp.Application.Exceptions;
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
        private readonly IApiWrapperService _apiService;
        private readonly IUserConfig _userConfig;
        private readonly IContext _appContext;
        private readonly IWGConfigService _WGConfigService;
        private readonly Dictionary<string, List<string>> _errorsByPropertyName = new Dictionary<string, List<string>>();
        public string Title => "Add Agent Token";
        public bool HasErrors => _errorsByPropertyName.Any();
        public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;
        private int _countCreatedInterface = 0;
        private int _totalInterfaces =  Enum.GetNames(typeof(WGInterfaceName)).Length;

        public AddTokenViewModel(IApiWrapperService apiService, IContext appContext, IUserConfig userConfig, IWGConfigService WGConfigService)
        {
            _apiService = apiService;
            _appContext = appContext;
            _userConfig = userConfig;
            _WGConfigService = WGConfigService;

            _WGConfigService.CreateInterfaceEvent += _WGConfigService_CreateInterfaceEvent;
            _WGConfigService.ErrorCreateInterfaceEvent += _WGConfigService_ErrorCreateInterfaceEvent;
        }

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

        private string _connectionError = string.Empty;
        public string ConnectionError
        {
            get { return _connectionError; }
            set
            {
                ValidateAgentToken(value);
                SetProperty(ref _connectionError, value);
            }
        }

        private bool _connectionErrorVisible = false;
        public bool ConnectionErrorVisible
        {
            get { return _connectionErrorVisible; }
            set
            {
               SetProperty(ref _connectionErrorVisible, value);
            }
        }

        private bool _loading = false;
        public bool Loading
        {
            get { return _loading; }
            set
            {
                if (SetProperty(ref _loading, value))
                {
                  IsEnabled = !value;
                }
            }
        }

        private bool _isEnabled = true;
        public bool IsEnabled
        {
            get { return _isEnabled; }
            set
            {
                SetProperty(ref _isEnabled, value);
            }
        }

        private bool _isEnabled1 = false;
        public bool IsEnabled1
        {
            get { return _isEnabled1; }
            set
            {
                SetProperty(ref _isEnabled1, value);
            }
        }

        private bool _isChecked = true;
        public bool IsChecked
        {
            get { return _isChecked; }
            set
            {
                SetProperty(ref _isChecked, value);
            }
        }

        public event Action<IDialogResult> RequestClose;

        private DelegateCommand<string> _closeDialogCommand;
        public DelegateCommand<string> CloseDialogCommand =>
            _closeDialogCommand ?? (_closeDialogCommand = new DelegateCommand<string>(CloseDialog));

        protected virtual void CloseDialog(string parameter)
        {
            ConnectionError = string.Empty;
            ConnectionErrorVisible = false;
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
            // try to connect to WS controller
            Loading = true;
            _userConfig.Authenticate(Name, AgentToken);
            Task.Run(() => {
                try
                {
                    _apiService.Run((WSConnectionResponse response) =>
                    {
                        if (response.State == Domain.Enums.WSConnectionState.Failed)
                        {
                            _userConfig.Quit();
                            ShowConnectionError(response.Error);
                        }

                    });
                }
                catch (NoFreePortException ex)
                {
                    _userConfig.Quit();
                    ShowConnectionError(ex.Message);
                }
            });
        }

        private void _WGConfigService_ErrorCreateInterfaceEvent(object arg1, Services.WireGuard.WGConfigServiceEventArgs arg2)
        {
            _countCreatedInterface = 0;
            ShowConnectionError($"Error creating {arg2.Interface.Name} interface");
        }

        private void _WGConfigService_CreateInterfaceEvent(object arg1, Services.WireGuard.WGConfigServiceEventArgs arg2)
        {
            _countCreatedInterface++;

            if(_countCreatedInterface == _totalInterfaces)
            {
                _countCreatedInterface = 0;
                FinishDialog();
            }
                
        }

        private void ShowConnectionError(string error)
        {
            if (!_appContext.IsSynchronized)
            {
                _appContext.BeginInvoke(ShowConnectionError, error);
                return;
            }
            ConnectionError = error;
            ConnectionErrorVisible = true;
            Loading = false;
        }

        private void FinishDialog()
        {
            if (!_appContext.IsSynchronized)
            {
                _appContext.BeginInvoke(FinishDialog);
                return;
            }
            RaiseRequestClose(new DialogResult(ButtonResult.OK, new Prism.Services.Dialogs.DialogParameters(){
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
