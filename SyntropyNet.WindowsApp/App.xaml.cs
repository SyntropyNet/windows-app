﻿using log4net;
using log4net.Appender;
using log4net.Repository.Hierarchy;
using NetFwTypeLib;
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
        private static Mutex _mutex = null;
        private const string appMutexName = "SyntropyNetWinApp";

        private static readonly ILog log = LogManager.GetLogger(typeof(App));
        private static readonly string EnableSentry = ConfigurationManager.AppSettings["EnableSentry"];

        const uint LOAD_LIBRARY_SEARCH_DEFAULT_DIRS = 0x00001000;

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool SetDllDirectory(string lpPathName);

        protected override void OnStartup(StartupEventArgs e)
        {
            try {
                SetupProgramDataDirs();
                SetupLoggerAppenders();
                _SetFirewallRules();

                var startingAsAService = e.Args.Any() && e.Args.Contains("/service");
                log4net.Config.XmlConfigurator.Configure();
                var currentDir = System.IO.Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);

                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
                this.Dispatcher.UnhandledException += App_DispatcherUnhandledException;
                this.DispatcherUnhandledException += App_DispatcherUnhandledException;

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
                        catch(Exception ex) {
                        
                        }
                    });
                    try { 
                        t.Start();
                        WGConfigService.Run(e.Args[1]);
                        log.Info("Service stopped");
                        t.Interrupt();
                    }
                    catch (Exception ex)
                    {
                        log.Error("Failed to start WG winservice", ex);
                        throw ex;
                    }
                    return;
                }
                else
                {
                    bool createdNew;
                    _mutex = new Mutex(true, appMutexName, out createdNew);

                    if (!createdNew)
                    {
                        //app is already running! Exiting the application  
                        this.Shutdown();
                    }
                }

                log.Info("Started as Desktop App");
                base.OnStartup(e);
            }
            catch(Exception ex)
            {
                log.Error("Error in Startup", ex);
            }
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
            containerRegistry.RegisterSingleton<IPublicIPChecker, PublicIPChecker>();
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

        private void SetupLoggerAppenders()
        {
            if(EnableSentry.ToLower() != "true")
            {
                // Remove Sentry Log4Net appender if it is disabled in config
                var root = ((log4net.Repository.Hierarchy.Hierarchy)LogManager.GetRepository()).Root;
                IAppender removedAppender = null;
                if (root.Appenders.ToArray().Any(x => x.Name == "SentryAppender"))
                {
                    removedAppender = root.RemoveAppender("SentryAppender");
                }
            }
        }

        private void SetupProgramDataDirs()
        {
            var syntropyLocalDir = Path.Combine(System.Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Syntropy\\");
            if (!Directory.Exists(syntropyLocalDir))
            {
                var dirInfo = Directory.CreateDirectory(syntropyLocalDir);
            }
            var logsDir = Path.Combine(syntropyLocalDir, "logs\\");
            if (!Directory.Exists(logsDir))
            {
                Directory.CreateDirectory(logsDir);
            }
        }

        private void _SetFirewallRules() {
            try {
                INetFwPolicy2 firewallPolicy = Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwPolicy2")) as INetFwPolicy2;

                if (firewallPolicy == null) {
                    log.Error("Unable to create Syntropy_IMCP_Inbound firewall rule. Unable to access FW policy");
                    return;
                }

                string name = "Syntropy_IMCP_Inbound";
                //string appPath = Assembly.GetEntryAssembly().Location;
                INetFwRule firewallRule = null;
                bool exist = false;

                foreach (INetFwRule2 rule in firewallPolicy.Rules) {
                    if (rule.Name == name) {
                        firewallRule = rule;
                        exist = true;
                    }
                }

                if (firewallRule == null) {
                    firewallRule = Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FWRule")) as INetFwRule;
                }

                if (firewallRule == null) {
                    log.Error("Unable to create Syntropy_IMCP_Inbound firewall rule");
                    return;
                }


                firewallRule.Action = NET_FW_ACTION_.NET_FW_ACTION_ALLOW;
                firewallRule.Direction = NET_FW_RULE_DIRECTION_.NET_FW_RULE_DIR_IN;
                firewallRule.Enabled = true;
                firewallRule.InterfaceTypes = "All";
                firewallRule.Name = name;
                firewallRule.Protocol = 1;
                //firewallRule.ApplicationName = appPath;

                //NOTE: Must do this after setting the Protocol!
                //firewallRule.LocalPorts = port.ToString();

                log.DebugFormat("Adding Windows Firewall Rule {0}...", firewallRule.Name);

                if (!exist) { 
                    firewallPolicy.Rules.Add(firewallRule);
                }

                log.InfoFormat("Windows Firewall Rule {0} added.", firewallRule.Name);
            } catch (Exception ex) {
                log.Error("Windows Firewall Rule for Syntropy could not be added", ex);
            }
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            var apiService = Container.Resolve<IApiWrapperService>();
            var ipChecker = Container.Resolve<IPublicIPChecker>();
            apiService.Stop();
            ipChecker.StopIPCheker();
        }
    }
}
