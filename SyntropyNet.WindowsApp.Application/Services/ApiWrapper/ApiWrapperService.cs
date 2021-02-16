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

namespace SyntropyNet.WindowsApp.Application.Services.ApiWrapper
{
    public class ApiWrapperService: IApiWrapperService
    {
        private readonly IAppSettings _appSettings;
        private readonly IUserConfig _userConfig;

        private ManualResetEvent exitEvent { get; set;}
        private bool Running { get; set; }

        private AutoPingHandler autoPingHandler;
        private GetInfoHandler getInfoHandler;
        public ApiWrapperService(IAppSettings appSettings, IUserConfig userConfig)
        {
            _appSettings = appSettings;
            _userConfig = userConfig;
        }

        public void Run()
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
            new Thread(() =>
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
                            case "GET_INFO":
                                var getInfoRequest = JsonConvert.DeserializeObject<GetInfoRequest>(msg.Text);

                                if (getInfoHandler != null)
                                {
                                    getInfoHandler.Interrupt();
                                    getInfoHandler = null;
                                }

                                getInfoHandler = new GetInfoHandler(client);
                                getInfoHandler.Start(getInfoRequest);

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
                        client.Start();
                        Running = true;
                    }
                    catch (WebsocketException e)
                    {
                        Debug.WriteLine($"Exception");
                        exitEvent.Set();
                        throw e;
                    }
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