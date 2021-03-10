using log4net;
using Prism.Ioc;
using Prism.Mvvm;
using Prism.Unity;
using SyntropyNet.WindowsApp.Application.Constants.WireGuard;
using SyntropyNet.WindowsApp.Application.Contracts;
using SyntropyNet.WindowsApp.Application.Domain.Models.WireGuard;
using SyntropyNet.WindowsApp.Application.Services;
using SyntropyNet.WindowsApp.Application.Services.ApiWrapper;
using SyntropyNet.WindowsApp.Application.Services.DockerApi;
using SyntropyNet.WindowsApp.Application.Services.HttpRequest;
using SyntropyNet.WindowsApp.Application.Services.NetworkInformation;
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
using System.Runtime.InteropServices;
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
        private static readonly ILog log = LogManager.GetLogger(typeof(App));

        const uint LOAD_LIBRARY_SEARCH_DEFAULT_DIRS = 0x00001000;

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool SetDefaultDllDirectories(uint DirectoryFlags);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern int AddDllDirectory(string NewDirectory);

        protected override void OnStartup(StartupEventArgs e)
        {
            log4net.Config.XmlConfigurator.Configure();
            // Setup correct references to tunnel.dll
            if (Environment.Is64BitProcess)
            {
                SetDefaultDllDirectories(LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);

                // Add the directory of the native dll
                AddDllDirectory("x64");
            }
            else
            {
                SetDefaultDllDirectories(LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);

                // Add the directory of the native dll
                AddDllDirectory("x86");
            }

            if (e.Args.Any() && e.Args.Contains("/service"))
            {
                log.Info("Started as Service");
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
            log.Info("Started as Desktop App");
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
            log.Error($"Unhandled Exception. {e.Exception.Message}",e.Exception);
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            log.Error($"Unhandled Exception. {(e.ExceptionObject as Exception).Message}", e.ExceptionObject as Exception);
        }

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // register other needed services here
            containerRegistry.Register<IContext, WpfContext>();
            containerRegistry.RegisterSingleton<IAppSettings, AppSettings>();
            containerRegistry.RegisterSingleton<IUserConfig, UserConfig>();
            containerRegistry.RegisterSingleton<IApiWrapperService, ApiWrapperService>();
            containerRegistry.RegisterSingleton<IHttpRequestService, HttpRequestService>();
            containerRegistry.RegisterSingleton<IDockerApiService, DockerApiService>();
            containerRegistry.RegisterSingleton<IWGConfigService, WGConfigService>();
            containerRegistry.RegisterSingleton<INetworkInformationService, NetworkInformationService>();
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
