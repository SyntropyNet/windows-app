using Prism.Mvvm;
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
        public MainWindowViewModel()
        {
            // ToDo:: implement config & API Code window
            // temporary pass const data for development
            // also need inject via DI
            var apiService = new ApiWrapperService("wss://controller-sandbox-platform-agents.syntropystack.com", "jFJ4OvvgJmpEhkrggbeXq5VKkgmau8nN", "Sibers", "test", "0.0.75");
            apiService.Run();
        }
    }
}
