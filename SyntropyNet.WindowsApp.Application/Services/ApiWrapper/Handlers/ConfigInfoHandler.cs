using Newtonsoft.Json;
using SyntropyNet.WindowsApp.Application.Contracts;
using SyntropyNet.WindowsApp.Application.Domain.Models.Messages;
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
                        var message = JsonConvert.SerializeObject(CreatePublicKeyAndPort(request, true, false));
                        Debug.WriteLine($"Update agent config: {message}");
                        Client.Send(message);
                        return;
                    }

                    //If CONFIG_INFO contains listen_port, but you cannot assign 
                    //anything to this port because its already taken or closed you have to assign new port 
                    if (!_WGConfigService.CheckListenPort(
                        (int)request.Data.Network.Public.ListenPort))
                    {
                        var message = JsonConvert.SerializeObject(CreatePublicKeyAndPort(request, false));
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
                    var message = JsonConvert.SerializeObject(CreatePublicKeyAndPort(request));
                    Debug.WriteLine($"Update agent config: {message}");
                    Client.Send(message);
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
                //ToDo: what to put in ifname
                Ifname = string.Empty,
                InternalIp = request.Data.Network.Public.InternalIp,
                PublicKey = publicKey == "" ? request.Data.Network.Public.PublicKey : publicKey,
                ListenPort = listenPort == 0 ? (int)request.Data.Network.Public.ListenPort : listenPort,
            };

            var createInterface = new CreateInterface
            {
                Data = createInterfaceData
            };

            var updateRequest = new UpdateAgentConfigRequest<CreateInterface>
            {
                Id = request.Id,
                Data = createInterface
            };

            return updateRequest;
        }
    }
}
