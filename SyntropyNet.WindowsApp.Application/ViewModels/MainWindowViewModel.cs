using Prism.Commands;
using Prism.Mvvm;
using SyntropyNet.WindowsApp.Application.Contracts;
using SyntropyNet.WindowsApp.Application.Services.ApiWrapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyntropyNet.WindowsApp.Application.ViewModels
{
    public class MainWindowViewModel: BindableBase
    {
        private readonly IApiWrapperService _apiService;
        private readonly IUserConfig _userConfig;
        private readonly IContext _appContext;
        private readonly Prism.Services.Dialogs.IDialogService _prismDialogs;
        public MainWindowViewModel(IApiWrapperService apiService,
                                   Prism.Services.Dialogs.IDialogService prismDialogs,
                                   IUserConfig userConfig,
                                   IContext appContext)
        {
            _prismDialogs = prismDialogs;
            _apiService = apiService;
            _userConfig = userConfig;
            _appContext = appContext;
        }

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
            _prismDialogs.ShowDialog("AddToken", new Prism.Services.Dialogs.DialogParameters(){
            }, r => {
                if (r.Result == Prism.Services.Dialogs.ButtonResult.OK)
                {
                    var name = r.Parameters.GetValue<string>("Name");
                    var agentToken = r.Parameters.GetValue<string>("AgentToken");
                    SetUserAuthentication(true,name);
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
