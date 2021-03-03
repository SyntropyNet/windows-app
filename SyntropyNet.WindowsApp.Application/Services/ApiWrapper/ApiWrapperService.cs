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

namespace SyntropyNet.WindowsApp.Application.Services.ApiWrapper
{
    public class ApiWrapperService: IApiWrapperService
    {
        private readonly IAppSettings _appSettings;
        private readonly IUserConfig _userConfig;
        private readonly IHttpRequestService _httpRequestService;
        private readonly IWGConfigService _WGConfigService;
        private readonly IDockerApiService _dockerApiService;

        public delegate void ServicesUpdated(IEnumerable<ServiceModel> services);
        public event ServicesUpdated ServicesUpdatedEvent;

        private ManualResetEvent exitEvent { get; set;}
        private bool Running { get; set; }

        private AutoPingHandler autoPingHandler;
        private GetInfoHandler getInfoHandler;
        private ConfigInfoHandler configInfoHandler;
        private WGConfHandler WGConfHandler;

        public ApiWrapperService(
            IAppSettings appSettings, 
            IUserConfig userConfig,
            IHttpRequestService httpRequestService,
            IWGConfigService WGConfigService,
            IDockerApiService dockerApiService)
        {
            _appSettings = appSettings;
            _userConfig = userConfig;
            _httpRequestService = httpRequestService;
            _WGConfigService = WGConfigService;
            _dockerApiService = dockerApiService;
        }

        public void Run(Action<WSConnectionResponse> callback)
        {
            if (Running)
            {
                // Already started
                return;
            }
            if(!_userConfig.IsAuthenticated || String.IsNullOrEmpty(_userConfig.AgentToken))
            {
                // User needs to enter Agent Token & Name
                // ToDo:: add custom exception here;
                return;
            }
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
                        KeepAliveInterval = TimeSpan.FromSeconds(30)
                    }
                    };
                    wsCLient.Options.SetRequestHeader("Authorization", _userConfig.AgentToken);
                    wsCLient.Options.SetRequestHeader("X-DeviceId", _appSettings.DeviceId);
                    wsCLient.Options.SetRequestHeader("X-DeviceName", _userConfig.DeviceName);
                    wsCLient.Options.SetRequestHeader("X-AgentVersion", _appSettings.AgentVersion);

                    return wsCLient;
                });

                using (var client = new WebsocketClient(url, factory))
                {
                    client.ReconnectionHappened.Subscribe(info =>
                        Debug.WriteLine($"Reconnection happened, type: {info.Type}"));

                    client.MessageReceived.Subscribe(msg => {
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

                                if (autoPingHandler != null)
                                {
                                    autoPingHandler.Interrupt();
                                    autoPingHandler = null;
                                }

                                autoPingHandler = new AutoPingHandler(client);
                                autoPingHandler.Start(autoPingRequest);

                                break;
                            case "CONFIG_INFO":
                                var configInfoRequest = JsonConvert.DeserializeObject<ConfigInfoRequest>(
                                    msg.Text, JsonSettings.GetSnakeCaseNamingStrategy());

                                if (configInfoHandler != null)
                                {
                                    configInfoHandler.Interrupt();
                                    configInfoHandler = null;
                                }

                                configInfoHandler = new ConfigInfoHandler(client, _WGConfigService);
                                configInfoHandler.Start(configInfoRequest);

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
                                                    Name = service.AgentServiceName,
                                                    Ip = service.AgentServiceSubnetIp,
                                                    Port = tcpPort.ToString()
                                                });
                                            }

                                            foreach (var udpPort in service.AgentServiceUdpPorts)
                                            {
                                                newServices.Add(new ServiceModel
                                                {
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

                                if (getInfoHandler != null)
                                {
                                    getInfoHandler.Interrupt();
                                    getInfoHandler = null;
                                }

                                getInfoHandler = new GetInfoHandler(client, _httpRequestService, _dockerApiService);
                                getInfoHandler.Start(getInfoRequest);

                                break;
                           case "WG_CONF":
                                var WGConfRequest = JsonConvert.DeserializeObject<WGConfRequest>(
                                    msg.Text, JsonSettings.GetSnakeCaseNamingStrategy());

                                if (WGConfHandler != null)
                                {
                                    WGConfHandler.Interrupt();
                                    WGConfHandler = null;
                                }

                                WGConfHandler = new WGConfHandler(client, _WGConfigService);
                                WGConfHandler.Start(WGConfRequest);

                                break;
                            default:
                                return;
                        }
                    });
                    client.DisconnectionHappened.Subscribe(x =>
                    {
                        Debug.WriteLine($"Disconnect: {x.Type}");
                        exitEvent.Set();
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
                    exitEvent.WaitOne();
                    Debug.WriteLine($"Connection finished");

                }
            }).Start();
        }

        public void Stop()
        {
            if(exitEvent != null)
            {
                // set ManualResetEvent to stop the Thread
                // client will be disposed in using
                exitEvent.Set();
            }
            if (autoPingHandler != null)
            {
                autoPingHandler.Interrupt();
                autoPingHandler = null;
            }
            Running = false;
        }
    }
}