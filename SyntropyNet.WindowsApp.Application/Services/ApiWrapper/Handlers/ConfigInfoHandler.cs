using Newtonsoft.Json;
using SyntropyNet.WindowsApp.Application.Contracts;
using SyntropyNet.WindowsApp.Application.Domain.Models.Messages;
using SyntropyNet.WindowsApp.Application.Domain.Models.WireGuard;
using SyntropyNet.WindowsApp.Application.Helpers;
using SyntropyNet.WindowsApp.Application.Services.WireGuard;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Websocket.Client;

namespace SyntropyNet.WindowsApp.Application.Services.ApiWrapper.Handlers
{
    public class ConfigInfoHandler : BaseHandler
    {
        private Thread mainTask;
        private readonly IWGConfigService _WGConfigService;

        public ConfigInfoHandler(WebsocketClient client, IWGConfigService WGConfigService) 
            : base(client)
        {
            _WGConfigService = WGConfigService;
        }

        public void Start(ConfigInfoRequest request)
        {
            mainTask?.Abort();

            mainTask = new Thread(async () =>
            {
                if (request.Data.Vpn.Count() != 0)
                {
                    //If CONFIG_INFO contains public_key, but agent didn’t 
                    //have private_key anymore, or its out of date,  
                    //agent should generate new public_key
                    //If CONFIG_INFO contains listen_port, but you cannot assign 
                    //anything to this port because its already taken or closed you have to assign new port 
                    if (!CheckPublicKeyAndPort(request))
                    {
                        var message = JsonConvert.SerializeObject(SetPublicKeyAndPort(request), 
                            JsonSettings.GetSnakeCaseNamingStrategy());
                        Debug.WriteLine($"Update agent config: {message}");
                        Client.Send(message);

                        var message2 = JsonConvert.SerializeObject(new GetConfigInfoRequest(),
                            JsonSettings.GetSnakeCaseNamingStrategy());
                        Debug.WriteLine($"Get config info: {message2}");
                        Client.Send(message2);

                        return;
                    }

                    //ToDo: Configure Wireguard connection
                }
                //If you’re connecting first time you only get internal_ip.
                //Public_key and listen_port should be created by agent
                else
                {
                    var message = JsonConvert.SerializeObject(SetPublicKeyAndPort(request),
                        JsonSettings.GetSnakeCaseNamingStrategy());
                    Debug.WriteLine($"Update agent config: {message}");
                    Client.Send(message);

                    var message2 = JsonConvert.SerializeObject(new GetConfigInfoRequest(),
                        JsonSettings.GetSnakeCaseNamingStrategy());
                    Debug.WriteLine($"Get config info: {message2}");
                    Client.Send(message2);
                }
            });

            mainTask.Start();
        }

        public void Interrupt()
        {
            mainTask?.Abort();
        }

        private bool CheckPublicKeyAndPort(ConfigInfoRequest request)
        {
            bool check = true;
            string publicKey = _WGConfigService.PublicKey;
            int listenPort = _WGConfigService.GetInterface().ListenPort;

            if (request.Data.Network.Public.PublicKey != publicKey ||
                request.Data.Network.Sdn1.PublicKey != publicKey ||
                request.Data.Network.Sdn2.PublicKey != publicKey ||
                request.Data.Network.Sdn3.PublicKey != publicKey)
                check = false;

            if (request.Data.Network.Public.ListenPort != listenPort ||
                request.Data.Network.Sdn1.ListenPort != listenPort ||
                request.Data.Network.Sdn2.ListenPort != listenPort ||
                request.Data.Network.Sdn3.ListenPort != listenPort)
                check = false;

            return check;
        }

        private UpdateAgentConfigRequest<CreateInterface> SetPublicKeyAndPort(ConfigInfoRequest request)
        {
            string interfaceName = _WGConfigService.InterfaceName;
            string publicKey = _WGConfigService.PublicKey;
            int listenPort = _WGConfigService.GetInterface().ListenPort;

            var createInterfacePublic = new CreateInterface
            {
                Data = new CreateInterfaceData
                {
                    Ifname = interfaceName,
                    InternalIp = request.Data.Network.Public.InternalIp,
                    PublicKey = publicKey,
                    ListenPort = listenPort,
                }
            };

            var createInterfaceSdn1 = new CreateInterface
            {
                Data = new CreateInterfaceData
                {
                    Ifname = interfaceName,
                    InternalIp = request.Data.Network.Sdn1.InternalIp,
                    PublicKey = publicKey,
                    ListenPort = listenPort,
                }
            };

            var createInterfaceSdn2 = new CreateInterface
            {
                Data = new CreateInterfaceData
                {
                    Ifname = interfaceName,
                    InternalIp = request.Data.Network.Sdn2.InternalIp,
                    PublicKey = publicKey,
                    ListenPort = listenPort,
                }
            };
            var createInterfaceSdn3 = new CreateInterface
            {
                Data = new CreateInterfaceData
                {
                    Ifname = interfaceName,
                    InternalIp = request.Data.Network.Sdn3.InternalIp,
                    PublicKey = publicKey,
                    ListenPort = listenPort,
                }
            };

            var updateRequest = new UpdateAgentConfigRequest<CreateInterface>
            {
                Id = $"Id{DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond}",
                Data = new List<CreateInterface> { 
                    createInterfacePublic, 
                    createInterfaceSdn1, 
                    createInterfaceSdn2, 
                    createInterfaceSdn3
                }
            };

            return updateRequest;
        }
    }
}
