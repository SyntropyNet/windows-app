using Newtonsoft.Json;
using SyntropyNet.WindowsApp.Application.Contracts;
using SyntropyNet.WindowsApp.Application.Domain.Models.Messages;
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
                    if (!_WGConfigService.CheckPrivateKey(
                        request.Data.Network.Public.PublicKey))
                    {
                        var message = JsonConvert.SerializeObject(CreatePublicKeyAndPort(request, true, false), 
                            JsonSettings.GetSnakeCaseNamingStrategy());
                        Debug.WriteLine($"Update agent config: {message}");
                        Client.Send(message);
                        return;
                    }

                    //If CONFIG_INFO contains listen_port, but you cannot assign 
                    //anything to this port because its already taken or closed you have to assign new port 
                    if (!_WGConfigService.CheckListenPort(
                        (int)request.Data.Network.Public.ListenPort))
                    {
                        var message = JsonConvert.SerializeObject(CreatePublicKeyAndPort(request, false),
                            JsonSettings.GetSnakeCaseNamingStrategy());
                        Debug.WriteLine($"Update agent config: {message}");
                        Client.Send(message);
                        return;
                    }

                    //ToDo: Configure Wireguard connection
                }
                //If you’re connecting first time you only get internal_ip.
                //Public_key and listen_port should be created by agent
                else
                {
                    var message = JsonConvert.SerializeObject(CreatePublicKeyAndPort(request),
                        JsonSettings.GetSnakeCaseNamingStrategy());
                    Debug.WriteLine($"Update agent config: {message}");
                    Client.Send(message);

                    var message3 = JsonConvert.SerializeObject(new GetConfigInfoRequest(),
                        JsonSettings.GetSnakeCaseNamingStrategy());
                    Debug.WriteLine($"Get config info: {message3}");
                    Client.Send(message3);
                }
            });

            mainTask.Start();
        }

        public void Interrupt()
        {
            mainTask?.Abort();
        }

        private UpdateAgentConfigRequest<CreateInterface> CreatePublicKeyAndPort(
            ConfigInfoRequest request, bool createPublicKey = true, bool createListenPort = true)
        {
            string publicKey = "";
            int listenPort = 0;

            if(createPublicKey)
                publicKey = _WGConfigService.CreatePublicKey();
            if(createListenPort)
                listenPort = _WGConfigService.CreateListenPort();

            

            var createInterfaceData = new CreateInterfaceData
            {
                Ifname = _WGConfigService.GetIfName(),
                InternalIp = request.Data.Network.Public.InternalIp,
                PublicKey = publicKey == "" ? request.Data.Network.Public.PublicKey : publicKey,
                ListenPort = listenPort == 0 ? (int)request.Data.Network.Public.ListenPort : listenPort,
            };

            var createInterfacePublic = new CreateInterface
            {
                Data = new CreateInterfaceData
                {
                    Ifname = _WGConfigService.GetIfName(),
                    InternalIp = request.Data.Network.Public.InternalIp,
                    PublicKey = publicKey == "" ? request.Data.Network.Public.PublicKey : publicKey,
                    ListenPort = listenPort == 0 ? (int)request.Data.Network.Public.ListenPort : listenPort,
                }
            };

            var createInterfaceSdn1 = new CreateInterface
            {
                Data = new CreateInterfaceData
                {
                    Ifname = _WGConfigService.GetIfName(),
                    InternalIp = request.Data.Network.Sdn1.InternalIp,
                    PublicKey = publicKey == "" ? request.Data.Network.Public.PublicKey : publicKey,
                    ListenPort = listenPort == 0 ? (int)request.Data.Network.Public.ListenPort : listenPort,
                }
            };

            var createInterfaceSdn2 = new CreateInterface
            {
                Data = new CreateInterfaceData
                {
                    Ifname = _WGConfigService.GetIfName(),
                    InternalIp = request.Data.Network.Sdn2.InternalIp,
                    PublicKey = publicKey == "" ? request.Data.Network.Public.PublicKey : publicKey,
                    ListenPort = listenPort == 0 ? (int)request.Data.Network.Public.ListenPort : listenPort,
                }
            };
            var createInterfaceSdn3 = new CreateInterface
            {
                Data = new CreateInterfaceData
                {
                    Ifname = _WGConfigService.GetIfName(),
                    InternalIp = request.Data.Network.Sdn3.InternalIp,
                    PublicKey = publicKey == "" ? request.Data.Network.Public.PublicKey : publicKey,
                    ListenPort = listenPort == 0 ? (int)request.Data.Network.Public.ListenPort : listenPort,
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
