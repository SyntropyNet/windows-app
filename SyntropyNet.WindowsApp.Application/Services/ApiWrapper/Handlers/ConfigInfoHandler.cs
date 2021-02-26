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
                if (!CheckPublicKeyAndPort(request))
                {
                    var message = JsonConvert.SerializeObject(SetPublicKeyAndPort(request), 
                        JsonSettings.GetSnakeCaseNamingStrategy());
                    Debug.WriteLine($"Update agent config: {message}");
                    Client.Send(message);
                }

                if (request.Data.Vpn.Count() != 0)
                {
                    SetPeers(request);
                }
            });

            mainTask.Start();
        }

        public void Interrupt()
        {
            mainTask?.Abort();
        }

        private void SetPeers(ConfigInfoRequest request)
        {
            var @interface = _WGConfigService.GetInterface();
            List<string> address = new List<string>();
            if (@interface.Address != null)
                address = @interface.Address.ToList<string>();

            List<Peer> peers = new List<Peer>();

            if(request.Data.Vpn.Count() > 0)
            {
                foreach (var item in request.Data.Vpn)
                {
                    if (!address.Contains(item.Args.GwIpv4))
                        address.Add(item.Args.GwIpv4);

                    peers.Add(new Peer
                    {
                        PublicKey = item.Args.PublicKey,
                        AllowedIPs = item.Args.AllowedIps,
                        Endpoint = $"{item.Args.EndpointIpv4}:{item.Args.EndpointPort}"
                    });
                }

                @interface.Address = address;
                _WGConfigService.SetInterface(@interface);
                _WGConfigService.SetPeers(peers);
                _WGConfigService.ApplyChange();
            }
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
