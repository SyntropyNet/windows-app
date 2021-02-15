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

namespace SyntropyNet.WindowsApp.Application.Services.ApiWrapper
{
    public class ApiWrapperService
    {
        private string _url;
        private string _authorizationKey;
        private string _deviceId;
        private string _deviceName;
        private string _agentVersion;


        private AutoPingHandler autoPingHandler;
        public ApiWrapperService(string url, string authorizationKey, string deviceId, string deviceName, string agentVersion)
        {
            _url = url;
            _authorizationKey = authorizationKey;
            _deviceId = deviceId;
            _deviceName = deviceName;
            _agentVersion = agentVersion;
        }

        public void Run()
        {
            new Thread(() =>
            {
                var exitEvent = new ManualResetEvent(false);
                var url = new Uri(_url);
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
                    wsCLient.Options.SetRequestHeader("Authorization", _authorizationKey);
                    wsCLient.Options.SetRequestHeader("X-DeviceId", _deviceId);
                    wsCLient.Options.SetRequestHeader("X-DeviceName", _deviceName);
                    wsCLient.Options.SetRequestHeader("X-AgentVersion", _agentVersion);

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
                        client.StartOrFail().GetAwaiter().GetResult();
                    }
                    catch (WebsocketException e)
                    {
                        Debug.WriteLine($"Exception");
                    }

                    exitEvent.WaitOne();

                }
            }).Start();
        }
    }
}