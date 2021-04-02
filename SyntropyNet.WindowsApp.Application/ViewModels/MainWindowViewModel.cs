using Prism.Commands;
using Prism.Mvvm;
using SyntropyNet.WindowsApp.Application.Contracts;
using SyntropyNet.WindowsApp.Application.Domain.Enums.WireGuard;
using SyntropyNet.WindowsApp.Application.Domain.Models;
using SyntropyNet.WindowsApp.Application.Exceptions;
using SyntropyNet.WindowsApp.Application.Models;
using SyntropyNet.WindowsApp.Application.Services.ApiWrapper;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Websocket.Client;
using static SyntropyNet.WindowsApp.Application.Services.ApiWrapper.ApiWrapperService;

namespace SyntropyNet.WindowsApp.Application.ViewModels
{
    public class MainWindowViewModel: BindableBase
    {
        private readonly IApiWrapperService _apiService;
        private readonly IAppSettings _appSettings;
        private readonly IUserConfig _userConfig;
        private readonly IContext _appContext;
        private readonly IWGConfigService _WGConfigService;
        private readonly Prism.Services.Dialogs.IDialogService _prismDialogs;
        private int _countCreatedInterface = 0;
        private int _totalInterfaces = Enum.GetNames(typeof(WGInterfaceName)).Length;

        private bool _autoDisconnection = false;

        public MainWindowViewModel(IApiWrapperService apiService,
                                   Prism.Services.Dialogs.IDialogService prismDialogs,
                                   IUserConfig userConfig,
                                   IContext appContext,
                                   IAppSettings appSettings,
                                   IWGConfigService WGConfigService)
        {
            _appSettings = appSettings;
            _prismDialogs = prismDialogs;
            _apiService = apiService;
            _userConfig = userConfig;
            _appContext = appContext;
            _WGConfigService = WGConfigService;

            _apiService.ServicesUpdatedEvent += UpdateServices;
            _apiService.PeersServicesUpdatedEvent += UpdatePeersServices;
            _apiService.DisconnectedEvent += Disconnected;
            _apiService.ReconnectingEvent += Reconnecting;
            _apiService.ReconnectedEvent += Reconnected;
            _WGConfigService.CreateInterfaceEvent += _WGConfigService_CreateInterfaceEvent;
            _WGConfigService.ErrorCreateInterfaceEvent += _WGConfigService_ErrorCreateInterfaceEvent;

            Host = _appSettings.DeviceName;
        }

        public void Disconnected(DisconnectionType type, string error)
        {
            if (!_appContext.IsSynchronized)
            {
                ApiWrapperService.Disconnected methodDelegate = Disconnected;
                _appContext.BeginInvoke(methodDelegate, type, error);
                return;
            }
            _autoDisconnection = true;
            Started = false;
            if(type == DisconnectionType.Error)
            {
                ShowError(error);
            }
        }
        public void Reconnecting(DisconnectionType type, string error)
        {
            SetReconnecting();
        }

        public void Reconnected()
        {
            if (!_appContext.IsSynchronized)
            {
                _appContext.BeginInvoke(Reconnected);
                return;
            }
            Status = "Connected";
        }

        private void ShowError(string error)
        {
            if (!_appContext.IsSynchronized)
            {
                _appContext.BeginInvoke(ShowError,  error);
                return;
            }
            Error = error;
            ErrorVisible = true;
        }

        private void StopLoading()
        {
            if (!_appContext.IsSynchronized)
            {
                _appContext.BeginInvoke(StopLoading);
                return;
            }
            Loading = false;
            OnoffEnabled = true;
        }

        private void SetDisconnected()
        {
            if (!_appContext.IsSynchronized)
            {
                _appContext.BeginInvoke(SetDisconnected);
                return;
            }
            _autoDisconnection = true;
            Started = false;
        }

        private void SetConnected()
        {
            if (!_appContext.IsSynchronized)
            {
                _appContext.BeginInvoke(SetConnected);
                return;
            }
            Status = "Connected";
            Started = true;
        }

        private void SetReconnecting()
        {
            if (!_appContext.IsSynchronized)
            {
                _appContext.BeginInvoke(SetReconnecting);
                return;
            }
            Status = "Reconnecting";
        }

        public void UpdateServices(IEnumerable<ServiceModel> services)
        {
            if (!_appContext.IsSynchronized)
            {
                _appContext.BeginInvoke(UpdateServices, services);
                return;
            }

            Services.Clear();
            foreach(var service in services)
            {
                if(!Services.Any(x => x.Name == service.Name && x.Address == service.Address))
                {
                    Services.Add(service);
                }
            }

        }

        public void UpdatePeersServices(IEnumerable<ServiceModel> addedServices, IEnumerable<string> removedPeers)
        {
            if (!_appContext.IsSynchronized)
            {
                PeersServicesUpdated methodDelegate = UpdatePeersServices;
                _appContext.BeginInvoke(methodDelegate, addedServices, removedPeers);
                return;
            }

            Services.AddRange(addedServices);

            var persistServices = Services.Where(x => removedPeers.All(uid => uid != x.PeerUid)).ToList();
            Services.Clear();
            foreach (var service in persistServices)
            {
                if (!Services.Any(x => x.Name == service.Name && x.Address == service.Address))
                {
                    Services.Add(service);
                }
            }
        }

        public ObservableCollection<ServiceModel> Services{ get; private set; } = new ObservableCollection<ServiceModel>();

        private bool _loggedIn = false;
        public bool LoggedIn
        {
            get { return _loggedIn; }
            set
            {
                if(SetProperty(ref _loggedIn, value))
                {
                    AddTokenVisible = !value;
                }
            }
        }

        private bool _started = true;
        public bool Started
        {
            get { return _started; }
            set
            {
                if(SetProperty(ref _started, value))
                {
                    if (value)
                    {
                        OnoffEnabled = false;
                        Status = string.Empty;
                        Loading = true;
                        Connect();
                    }
                    else
                    {
                        Status = "Disconnected";
                        if (!_autoDisconnection)
                        {
                            Task.Run(() => {
                                _apiService.Stop();
                            });
                        }
                        _autoDisconnection = false;
                    }
                }
                else
                {
                    _autoDisconnection = false;
                }
            }
        }

        private bool _errorVisible = false;
        public bool ErrorVisible
        {
            get { return _errorVisible; }
            set
            {
                if(SetProperty(ref _errorVisible, value))
                {
                    StatusVisible = !value;
                }
            }
        }

        private bool _statusVisible = true;
        public bool StatusVisible
        {
            get { return _statusVisible; }
            set
            {
                SetProperty(ref _statusVisible, value);
            }
        }


        private bool _addTokenVisible = true;
        public bool AddTokenVisible
        {
            get { return _addTokenVisible; }
            set
            {
                SetProperty(ref _addTokenVisible, value);
            }
        }

        private string _name = string.Empty;
        public string Name
        {
            get { return _name; }
            set
            {
                SetProperty(ref _name, value);
            }
        }

        private string _error = string.Empty;
        public string Error
        {
            get { return _error; }
            set
            {
                SetProperty(ref _error, value);
            }
        }

        private string _status = "Connected";
        public string Status
        {
            get { return _status; }
            set
            {
                SetProperty(ref _status, value);
            }
        }

        private string _host = string.Empty;
        public string Host
        {
            get { return _host; }
            set
            {
                SetProperty(ref _host, value);
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
                }
            }
        }

        private bool _onoffEnabled= true;
        public bool OnoffEnabled
        {
            get { return _onoffEnabled; }
            set
            {
                if (SetProperty(ref _onoffEnabled, value))
                {
                }
            }
        }

        private void Connect()
        {
            ErrorVisible = false;
            Task.Run(() => {
                try
                {
                    _apiService.Run((WSConnectionResponse response) =>
                    {
                        if (response.State == Domain.Enums.WSConnectionState.Failed)
                        {
                            SetDisconnected();
                            ShowError(response.Error);
                            StopLoading();
                        }
                    });
                }
                catch (NoFreePortException ex)
                {
                    SetDisconnected();
                    ShowError(ex.Message);
                    StopLoading();
                }
            });
        }

        private void _WGConfigService_ErrorCreateInterfaceEvent(object arg1, Services.WireGuard.WGConfigServiceEventArgs arg2)
        {
            _countCreatedInterface = 0;
            SetDisconnected();
            StopLoading();
            ShowError($"Error creating {arg2.Interface.Name} interface");
        }

        private void _WGConfigService_CreateInterfaceEvent(object arg1, Services.WireGuard.WGConfigServiceEventArgs arg2)
        {
            _countCreatedInterface++;

            if (_countCreatedInterface == _totalInterfaces)
            {
                _countCreatedInterface = 0;
                SetConnected();
                StopLoading();
            }

        }

        private DelegateCommand _commandLogout = null;
        public DelegateCommand CommandLogout =>
            _commandLogout ?? (_commandLogout = new DelegateCommand(CommandLogoutExecute));

        private void CommandLogoutExecute()
        {
            _userConfig.Quit();
            _apiService.Stop();
            Name = string.Empty;
            LoggedIn = false;
        }


        private DelegateCommand _commandAddToken = null;
        public DelegateCommand CommandAddToken =>
            _commandAddToken ?? (_commandAddToken = new DelegateCommand(CommandAddTokenExecute));

        private void CommandAddTokenExecute()
        {
            _appSettings.ModalWindowActivated = true;
            _prismDialogs.ShowDialog("AddToken", new Prism.Services.Dialogs.DialogParameters(){
            }, r => {
                _appSettings.ModalWindowActivated = false;
                if (r.Result == Prism.Services.Dialogs.ButtonResult.OK)
                {
                    var name = r.Parameters.GetValue<string>("Name");
                    var agentToken = r.Parameters.GetValue<string>("AgentToken");
                    SetUserAuthentication(true,name);
                    SetConnected();
                }
            });
        }

        private delegate void SetUserAuthenticationDelegate(bool value, string name);
        private void SetUserAuthentication(bool value,string name)
        {
            if (!_appContext.IsSynchronized)
            {
                SetUserAuthenticationDelegate methodDelegate = SetUserAuthentication;
                _appContext.BeginInvoke(methodDelegate, value, name);
                return;
            }

            Name = name;
            LoggedIn = value;
        }
    }
}
