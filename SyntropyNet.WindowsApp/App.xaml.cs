using Prism.Ioc;
using Prism.Mvvm;
using Prism.Unity;
using SyntropyNet.WindowsApp.Application.Constants.WireGuard;
using SyntropyNet.WindowsApp.Application.Contracts;
using SyntropyNet.WindowsApp.Application.Domain.Models.WireGuard;
using SyntropyNet.WindowsApp.Application.Services;
using SyntropyNet.WindowsApp.Application.Services.ApiWrapper;
using SyntropyNet.WindowsApp.Application.Services.HttpRequest;
using SyntropyNet.WindowsApp.Application.Services.WireGuard;
using SyntropyNet.WindowsApp.Application.ViewModels;
using SyntropyNet.WindowsApp.Services;
using SyntropyNet.WindowsApp.Views;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace SyntropyNet.WindowsApp
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App: PrismApplication
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            if (e.Args.Any() && e.Args.Contains("/service"))
            {
                var t = new Thread(() =>
                {
                    try
                    {
                        var currentProcess = Process.GetCurrentProcess();
                        var uiProcess = Process.GetProcessById(int.Parse(e.Args[2]));
                        if (uiProcess.MainModule.FileName != currentProcess.MainModule.FileName)
                            return;
                        uiProcess.WaitForExit();
                        WGConfigService.Remove(e.Args[1], false);
                    }
                    catch { }
                });
                t.Start();
                WGConfigService.Run(e.Args[1]);
                t.Interrupt();
                return;
            }

            base.OnStartup(e);
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            this.Dispatcher.UnhandledException += App_DispatcherUnhandledException;
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            // Process unhandled exception
            // Prevent default unhandled exception processing
            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
        }

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // register other needed services here
            containerRegistry.Register<IContext, WpfContext>();
            containerRegistry.RegisterSingleton<IAppSettings, AppSettings>();
            containerRegistry.RegisterSingleton<IUserConfig, UserConfig>();
            containerRegistry.RegisterSingleton<IApiWrapperService, ApiWrapperService>();
            containerRegistry.RegisterSingleton<IHttpRequestService, HttpRequestService>();
            containerRegistry.RegisterInstance(new TunnelSettings(
                WireGuardConstants.CONFIG_FILE_LOCATION, WireGuardConstants.INTERFACE_NAME));
            containerRegistry.RegisterSingleton<IWGConfigService, WGConfigService>();
            containerRegistry.RegisterDialog<AddToken, AddTokenViewModel>();
        }

        protected override Window CreateShell()
        {
            var w = Container.Resolve<MainWindow>();
            return w;
        }

        protected override void ConfigureViewModelLocator()
        {
            base.ConfigureViewModelLocator();

            ViewModelLocationProvider.SetDefaultViewTypeToViewModelTypeResolver((viewType) =>
            {
                var viewName = viewType.FullName.Replace(".Views.", ".ViewModels.").Replace(".WindowsApp.", ".WindowsApp.Application.");
                var viewAssemblyName = typeof(MainWindowViewModel).GetTypeInfo().Assembly.FullName;
                var viewModelName = $"{viewName}ViewModel, {viewAssemblyName}";
                return Type.GetType(viewModelName);
            });
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            var apiService = Container.Resolve<IApiWrapperService>();
            apiService.Stop();
        }
    }
}
