using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Websocket.Client;
using Websocket.Client.Exceptions;
using Newtonsoft.Json;
using SyntropyNet.WindowsApp.Application.Services.ApiWrapper.Handlers;
using SyntropyNet.WindowsApp.Application.Domain.Models.Messages;
using SyntropyNet.WindowsApp.Application.Contracts;
using SyntropyNet.WindowsApp.Application.Domain.Models;
using System.Windows.Controls;
using SyntropyNet.WindowsApp.Application.Helpers;
using SyntropyNet.WindowsApp.Application.Models;
using System.Web.Configuration;
using log4net;
using SyntropyNet.WindowsApp.Application.Constants;
using System.Net;

namespace SyntropyNet.WindowsApp.Application.Services.ApiWrapper
{
    public class ApiWrapperService: IApiWrapperService
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ApiWrapperService));

        private readonly IAppSettings _appSettings;
        private readonly IUserConfig _userConfig;
        private readonly IHttpRequestService _httpRequestService;
        private readonly IWGConfigService _WGConfigService;
        private readonly IDockerApiService _dockerApiService;
        private readonly INetworkInformationService _networkInformationService;

        public delegate void ServicesUpdated(IEnumerable<ServiceModel> services);
        public event ServicesUpdated ServicesUpdatedEvent;

        public delegate void PeersServicesUpdated(IEnumerable<ServiceModel> addedServices, IEnumerable<string> removedPeers);
        public event PeersServicesUpdated PeersServicesUpdatedEvent;

        public delegate void Disconnected(DisconnectionType type, string error);
        public event Disconnected DisconnectedEvent;
        public event Disconnected ReconnectingEvent;
        public delegate void Reconnected();
        public event Reconnected ReconnectedEvent;

        private ManualResetEvent exitEvent { get; set;}
        private bool Running { get; set; }
        private bool Stopping { get; set; }
        private int WaitReconnect { get; set; } = 0;
        private string UserError { get; set; }
        private bool IsRecconect { get; set; } = false;

        private AutoPingHandler autoPingHandler;
        private GetInfoHandler getInfoHandler;
        private ConfigInfoHandler configInfoHandler;
        private WGConfHandler WGConfHandler;
        private ContainerInfoHandler containerInfoHandler;
        private IfaceBWDataHandler ifaceBWDataHandler;
        private IfacesPeersBWDataHandler ifacesPeersBWDataHandler;

        public ApiWrapperService(
            IAppSettings appSettings, 
            IUserConfig userConfig,
            IHttpRequestService httpRequestService,
            IWGConfigService WGConfigService,
            IDockerApiService dockerApiService,
            INetworkInformationService networkInformationService)
        {
            _appSettings = appSettings;
            _userConfig = userConfig;
            _httpRequestService = httpRequestService;
            _WGConfigService = WGConfigService;
            _dockerApiService = dockerApiService;
            _networkInformationService = networkInformationService;
        }

        public void Run(Action<WSConnectionResponse> callback)
        {
            if (Running)
            {
                // Already started
                callback?.Invoke(new WSConnectionResponse
                {
                    State = Domain.Enums.WSConnectionState.Connected
                });
                return;
            }
            if(!_userConfig.IsAuthenticated || String.IsNullOrEmpty(_userConfig.AgentToken))
            {
                // User needs to enter Agent Token & Name
                // ToDo:: add custom exception here;
                return;
            }
            Stopping = false;
            _WGConfigService.CreateInterfaces();
            new Thread(async () =>
            {
                exitEvent = new ManualResetEvent(false);
                var url = new Uri(_appSettings.ControllerUrl);
                // Use custom Func as a ClientWebSocket factory to provide required headers
                var factory = new Func<ClientWebSocket>(() =>
                {
                    var wsCLient = new ClientWebSocket
                    {
                        Options =
                        {
                            KeepAliveInterval = TimeSpan.FromSeconds(90),
                        
                        }
                    };
                    wsCLient.Options.SetRequestHeader("Authorization", _userConfig.AgentToken);
                    wsCLient.Options.SetRequestHeader("X-DeviceId", _appSettings.DeviceId);
                    wsCLient.Options.SetRequestHeader("X-DeviceIp", _appSettings.DeviceIp);
                    wsCLient.Options.SetRequestHeader("X-DeviceName", _userConfig.TokenName);
                    wsCLient.Options.SetRequestHeader("X-AgentType", "Windows");
                    wsCLient.Options.SetRequestHeader("X-AgentVersion", _appSettings.AgentVersion);

                    return wsCLient;
                });

                using (var client = new WebsocketClient(url, factory))
                {
                    UserError = null;

                    client.IsReconnectionEnabled = true;
                    client.ErrorReconnectTimeout = new TimeSpan(5000);
                    client.ReconnectTimeout = new TimeSpan(1000, 1000, 1000);
                    client.ReconnectionHappened.Subscribe(info =>
                    {
                        Debug.WriteLine($"Reconnection happened, type: {info.Type}");
                        WaitReconnect = 0;
                        ReconnectedEvent?.Invoke();
                    });

                    client.MessageReceived.Subscribe(msg => {
                        if (Stopping)
                        {
                            return;
                        }
                        Debug.WriteLine($"Message received: {msg}");
                        var obj = JsonConvert.DeserializeObject<BaseMessage>(msg.Text);
                        if (obj == null)
                        {
                            return;
                        }

                        switch (obj.Type)
                        {
                            case "AUTO_PING":
                                var autoPingRequest = JsonConvert.DeserializeObject<AutoPingRequest>(msg.Text);

                                try
                                {
                                    if (autoPingHandler != null)
                                    {
                                        autoPingHandler.Interrupt();
                                        autoPingHandler = null;
                                    }

                                    autoPingHandler = new AutoPingHandler(client, _appSettings, _httpRequestService);
                                    autoPingHandler.Start(autoPingRequest);
                                }
                                catch (Exception ex)
                                {
                                    try
                                    {
                                        LoggerRequestHelper.Send(
                                            client,
                                            log4net.Core.Level.Error,
                                            _appSettings.DeviceId,
                                            _appSettings.DeviceName,
                                            _appSettings.DeviceId,
                                            $"[Message: {ex.Message}, stacktrace: {ex.StackTrace}]");
                                    }
                                    catch (Exception ex2)
                                    {
                                        log.Error($"[Message: {ex2.Message}, stacktrace: {ex2.StackTrace}]");
                                    }
                                }

                                break;
                            case "CONFIG_INFO":
                                var configInfoRequest = JsonConvert.DeserializeObject<ConfigInfoRequest>(
                                    msg.Text, JsonSettings.GetSnakeCaseNamingStrategy());

                                try
                                {
                                    if (configInfoHandler != null)
                                    {
                                        configInfoHandler.Interrupt();
                                        configInfoHandler = null;
                                    }

                                    configInfoHandler = new ConfigInfoHandler(client, _WGConfigService, _networkInformationService, _appSettings, _httpRequestService);
                                    configInfoHandler.Start(configInfoRequest, IsRecconect);
                                    IsRecconect = false;
                                }
                                catch(Exception ex)
                                {
                                    try
                                    {
                                        LoggerRequestHelper.Send(
                                            client,
                                            log4net.Core.Level.Error,
                                            _appSettings.DeviceId,
                                            _appSettings.DeviceName,
                                            _appSettings.DeviceIp,
                                            $"[Message: {ex.Message}, stacktrace: {ex.StackTrace}]");
                                    }
                                    catch (Exception ex2)
                                    {
                                        log.Error($"[Message: {ex2.Message}, stacktrace: {ex2.StackTrace}]");
                                    }
                                }

                                // prepare Services Liset
                                var newServices = new List<ServiceModel>();
                                foreach(var vpnData in configInfoRequest.Data.Vpn)
                                {
                                    if(vpnData.Metadata != null)
                                    {
                                        foreach(var service in vpnData.Metadata.AllowedIpsInfo)
                                        {
                                            foreach(var tcpPort in service.AgentServiceTcpPorts)
                                            {
                                                newServices.Add(new ServiceModel{
                                                    PeerUid = vpnData.Args.PublicKey,
                                                    Name = service.AgentServiceName,
                                                    Ip = service.AgentServiceSubnetIp,
                                                    Port = tcpPort.ToString()
                                                });
                                            }

                                            foreach (var udpPort in service.AgentServiceUdpPorts)
                                            {
                                                newServices.Add(new ServiceModel
                                                {
                                                    PeerUid = vpnData.Args.PublicKey,
                                                    Name = service.AgentServiceName,
                                                    Ip = service.AgentServiceSubnetIp,
                                                    Port = udpPort.ToString()
                                                });
                                            }
                                        }
                                    }
                                }

                                ServicesUpdatedEvent?.Invoke(newServices);

                                break;
                            case "GET_INFO":
                                var getInfoRequest = JsonConvert.DeserializeObject<GetInfoRequest>(
                                    msg.Text, JsonSettings.GetSnakeCaseNamingStrategy());

                                try
                                {
                                    if (getInfoHandler != null)
                                    {
                                        getInfoHandler.Interrupt();
                                        getInfoHandler = null;
                                    }
                                    getInfoHandler = new GetInfoHandler(client, _httpRequestService, _dockerApiService, _appSettings);
                                    getInfoHandler.Start(getInfoRequest);
                                }
                                catch (Exception ex)
                                {
                                    try
                                    {
                                        LoggerRequestHelper.Send(
                                            client,
                                            log4net.Core.Level.Error,
                                            _appSettings.DeviceId,
                                            _appSettings.DeviceName,
                                            _appSettings.DeviceIp,
                                            $"[Message: {ex.Message}, stacktrace: {ex.StackTrace}]");
                                    }
                                    catch (Exception ex2)
                                    {
                                        log.Error($"[Message: {ex2.Message}, stacktrace: {ex2.StackTrace}]");
                                    }
                                }

                                break;
                           case "WG_CONF":
                                var WGConfRequest = JsonConvert.DeserializeObject<WGConfRequest>(
                                    msg.Text, JsonSettings.GetSnakeCaseNamingStrategy());

                                try
                                {
                                    if (WGConfHandler != null)
                                    {
                                        Debug.WriteLine("+++ WG_CONF time start");
                                        for (int i = 1; i <= 60; i++)
                                        {
                                            if(i == 60)
                                            {
                                                Debug.WriteLine($"+++ WG_CONF time end with error");
                                                throw new Exception("Runtime exceeded");
                                            }
                                            if (!WGConfHandler.IsAlive())
                                            {
                                                WGConfHandler.Interrupt();
                                                WGConfHandler = null;
                                                Debug.WriteLine($"+++ WG_CONF time end with {i}");
                                                break;
                                            }

                                            Thread.Sleep(1000);
                                        }
                                    }
                                    Debug.WriteLine("+++ WG_CONF start");

                                    WGConfHandler = new WGConfHandler(client, _WGConfigService, _appSettings, _httpRequestService);
                                    WGConfHandler.Start(WGConfRequest);
                                }
                                catch (Exception ex)
                                {
                                    try
                                    {
                                        LoggerRequestHelper.Send(
                                            client,
                                            log4net.Core.Level.Error,
                                            _appSettings.DeviceId,
                                            _appSettings.DeviceName,
                                            _appSettings.DeviceIp,
                                            $"[Message: {ex.Message}, stacktrace: {ex.StackTrace}]");
                                    }
                                    catch (Exception ex2)
                                    {
                                        log.Error($"[Message: {ex2.Message}, stacktrace: {ex2.StackTrace}]");
                                    }

                                    break;
                                }
                                
                                var addedServices = new List<ServiceModel>();
                                var removedPeers = new List<string>();
                                foreach (var item in WGConfRequest.Data)
                                {
                                    switch (item.Fn)
                                    {
                                        case "add_peer":
                                            foreach (var service in item.Metadata.AllowedIpsInfo)
                                            {
                                                foreach (var tcpPort in service.AgentServiceTcpPorts)
                                                {
                                                    addedServices.Add(new ServiceModel
                                                    {
                                                        PeerUid = item.Args.PublicKey,
                                                        Name = service.AgentServiceName,
                                                        Ip = service.AgentServiceSubnetIp,
                                                        Port = tcpPort.ToString()
                                                    });
                                                }

                                                foreach (var udpPort in service.AgentServiceUdpPorts)
                                                {
                                                    addedServices.Add(new ServiceModel
                                                    {
                                                        PeerUid = item.Args.PublicKey,
                                                        Name = service.AgentServiceName,
                                                        Ip = service.AgentServiceSubnetIp,
                                                        Port = udpPort.ToString()
                                                    });
                                                }
                                            }
                                            break;
                                        case "remove_peer":
                                            removedPeers.Add(item.Args.PublicKey);
                                            break;
                                    }
                                }

                                PeersServicesUpdatedEvent?.Invoke(addedServices,removedPeers);

                                break;
                            default:
                                return;
                        }
                    });

                    client.DisconnectionHappened.Subscribe(x =>
                    {
                        Debug.WriteLine($"Disconnect: {x.Type}");
                        log.Info($"Disconnected: {x.Type}. Status: {x.CloseStatus}, Description: {x.CloseStatusDescription}. {x.Exception?.Message ?? string.Empty}");

                        if(x.Type == DisconnectionType.Lost && x.CloseStatus == null)
                        {
                            DisconnectedEvent?.Invoke(x.Type, x.Exception?.Message);
                            _WGConfigService.StopWG();
                            Running = false;
                            exitEvent.Set();
                            return;
                        }

                        if (x.CloseStatus != null && (x.CloseStatus.ToString() == "4000" || x.CloseStatus.ToString() == "4001"))
                        {
                            DisconnectedEvent?.Invoke(x.Type, x.Exception?.Message);
                            _WGConfigService.StopWG();
                            Running = false;
                            exitEvent.Set();
                            return;
                        }
                        
                        if (x.Exception?.InnerException is WebException && (x.Exception?.InnerException as WebException)?.Response is HttpWebResponse)
                        {
                            var code = ((x.Exception?.InnerException as WebException)?.Response as HttpWebResponse).StatusCode;

                            if(code == HttpStatusCode.Unauthorized)
                            {
                                UserError = "Token is invalid";
                                DisconnectedEvent?.Invoke(x.Type, x.Exception?.Message);
                                _WGConfigService.StopWG();
                                Running = false;
                                exitEvent.Set();
                                return;
                            }
                        }

                        if (Running)
                        {
                            log.Info($"Attempt to reconnect via {WaitReconnect}ms");
                            ReconnectingEvent?.Invoke(x.Type, x.Exception?.Message);
                            Thread.Sleep(WaitReconnect);
                            try
                            {
                                checked
                                {
                                    WaitReconnect += 5000;
                                }

                                IsRecconect = true;
                            }
                            catch (OverflowException ex)
                            {
                                Running = false;
                                IsRecconect = false;
                                WaitReconnect = 0;
                            }

                            return;
                        }
                        else
                        {
                            DisconnectedEvent?.Invoke(x.Type, x.Exception?.Message);
                            _WGConfigService.StopWG();
                            Running = false;
                            exitEvent.Set();
                        }
                    });

                    try
                    {
                        await client.StartOrFail();
                    }
                    catch (WebsocketException e)
                    {
                        Debug.WriteLine($"Failed to connect");
                        var error = e.Message;
                        if(e.InnerException != null)
                        {
                            error = e.InnerException.Message;
                        }
                        if(UserError != null)
                        {
                            error = UserError;
                        }
                        callback?.Invoke(new WSConnectionResponse{
                             State = Domain.Enums.WSConnectionState.Failed,
                             Error = error
                        });
                        exitEvent.Set();
                        return;
                    }
                    Running = true;
                    callback?.Invoke(new WSConnectionResponse
                    {
                        State = Domain.Enums.WSConnectionState.Connected
                    });
                    Debug.WriteLine($"WebSocket connection started");

                    try
                    {
                        if (containerInfoHandler != null)
                        {
                            containerInfoHandler.Interrupt();
                            containerInfoHandler = null;
                        }

                        containerInfoHandler = new ContainerInfoHandler(client, _dockerApiService, _appSettings, _httpRequestService);
                        containerInfoHandler.Start();

                        if (ifaceBWDataHandler != null)
                        {
                            ifaceBWDataHandler.Interrupt();
                            ifaceBWDataHandler = null;
                        }

                        ifaceBWDataHandler = new IfaceBWDataHandler(client, _networkInformationService, _appSettings, _httpRequestService);
                        ifaceBWDataHandler.Start();

                        if (ifacesPeersBWDataHandler != null)
                        {
                            ifacesPeersBWDataHandler.Interrupt();
                            ifacesPeersBWDataHandler = null;
                        }

                        ifacesPeersBWDataHandler = new IfacesPeersBWDataHandler(client, _WGConfigService, _appSettings, _httpRequestService);
                        ifacesPeersBWDataHandler.Start();
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            LoggerRequestHelper.Send(
                                client,
                                log4net.Core.Level.Error,
                                _appSettings.DeviceId,
                                _appSettings.DeviceName,
                                _appSettings.DeviceIp,
                                $"[Message: {ex.Message}, stacktrace: {ex.StackTrace}]");
                        }
                        catch (Exception ex2)
                        {
                            log.Error($"[Message: {ex2.Message}, stacktrace: {ex2.StackTrace}]");
                        }
                    }

                    exitEvent.WaitOne();
                    _WGConfigService.StopWG();
                    Running = false;
                    //await client.Stop(WebSocketCloseStatus.NormalClosure,string.Empty);
                    Debug.WriteLine($"Connection finished");

                }
            }).Start();
        }

        public void Stop()
        {
            Stopping = true;
            if (autoPingHandler != null)
            {
                autoPingHandler.Interrupt();
                autoPingHandler = null;
            }
            if (getInfoHandler != null)
            {
                getInfoHandler.Interrupt();
                getInfoHandler = null;
            }
            if (configInfoHandler != null)
            {
                configInfoHandler.Interrupt();
                configInfoHandler = null;
            }
            if (WGConfHandler != null)
            {
                WGConfHandler.Interrupt();
                WGConfHandler = null;
            }
            if (containerInfoHandler != null)
            {
                containerInfoHandler.Interrupt();
                containerInfoHandler = null;
            }
            if (ifacesPeersBWDataHandler != null)
            {
                ifacesPeersBWDataHandler.Interrupt();
                ifacesPeersBWDataHandler = null;
            }
            if (ifaceBWDataHandler != null)
            {
                ifaceBWDataHandler.Interrupt();
                ifaceBWDataHandler = null;
            }

            if (exitEvent != null)
            {
                // set ManualResetEvent to stop the Thread
                // client will be disposed in using
                exitEvent.Set();
            }

            _WGConfigService.StopWG();
            Running = false;
        }
    }
}