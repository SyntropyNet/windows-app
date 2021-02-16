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
        public MainWindowViewModel(IApiWrapperService apiService)
        {
            _apiService = apiService;
            _apiService.Run();
        }
    }
}
