using Newtonsoft.Json;
using SyntropyNet.WindowsApp.Application.Contracts;
using SyntropyNet.WindowsApp.Application.Domain.Enums.WireGuard;
using SyntropyNet.WindowsApp.Application.Domain.Models.Messages;
using SyntropyNet.WindowsApp.Application.Domain.Models.WireGuard;
using SyntropyNet.WindowsApp.Application.Exceptions;
using SyntropyNet.WindowsApp.Application.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Websocket.Client;

namespace SyntropyNet.WindowsApp.Application.Services.ApiWrapper.Handlers
{
    public class ConfigInfoHandler : BaseHandler
    {
        private Thread mainTask;
        private readonly IWGConfigService _WGConfigService;
        private readonly INetworkInformationService _networkInformationService;

        public ConfigInfoHandler(WebsocketClient client, 
            IWGConfigService WGConfigService,
             INetworkInformationService networkInformationService) 
            : base(client)
        {
            _WGConfigService = WGConfigService;
            _networkInformationService = networkInformationService;
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

                _WGConfigService.ApplyModifiedConfigs();
            });

            mainTask.Start();
        }

        public void Interrupt()
        {
            mainTask?.Abort();
        }

        private void SetPeers(ConfigInfoRequest request)
        {
            var publicPeerSection = _WGConfigService.GetPeerSections(WGInterfaceName.SYNTROPY_PUBLIC).ToList();
            var sdn1PeerSection = _WGConfigService.GetPeerSections(WGInterfaceName.SYNTROPY_SDN1).ToList();
            var sdn2PeerSection = _WGConfigService.GetPeerSections(WGInterfaceName.SYNTROPY_SDN2).ToList();
            var sdn3PeerSection = _WGConfigService.GetPeerSections(WGInterfaceName.SYNTROPY_SDN3).ToList();

            if (request.Data.Vpn.Count() > 0)
            {
                foreach (var item in request.Data.Vpn)
                {
                    if(item.Args.Ifname == _WGConfigService.GetInterfaceName(WGInterfaceName.SYNTROPY_PUBLIC))
                    {
                        publicPeerSection.Add(new Peer
                        {
                            PublicKey = item.Args.PublicKey,
                            AllowedIPs = item.Args.AllowedIps,
                            Endpoint = $"{item.Args.EndpointIpv4}:{item.Args.EndpointPort}"
                        });
                    }
                    else if(item.Args.Ifname == _WGConfigService.GetInterfaceName(WGInterfaceName.SYNTROPY_SDN1))
                    {
                        sdn1PeerSection.Add(new Peer
                        {
                            PublicKey = item.Args.PublicKey,
                            AllowedIPs = item.Args.AllowedIps,
                            Endpoint = $"{item.Args.EndpointIpv4}:{item.Args.EndpointPort}"
                        });
                    }
                    else if(item.Args.Ifname == _WGConfigService.GetInterfaceName(WGInterfaceName.SYNTROPY_SDN2))
                    {
                        sdn2PeerSection.Add(new Peer
                        {
                            PublicKey = item.Args.PublicKey,
                            AllowedIPs = item.Args.AllowedIps,
                            Endpoint = $"{item.Args.EndpointIpv4}:{item.Args.EndpointPort}"
                        });
                    }
                    else if (item.Args.Ifname == _WGConfigService.GetInterfaceName(WGInterfaceName.SYNTROPY_SDN3))
                    {
                        sdn3PeerSection.Add(new Peer
                        {
                            PublicKey = item.Args.PublicKey,
                            AllowedIPs = item.Args.AllowedIps,
                            Endpoint = $"{item.Args.EndpointIpv4}:{item.Args.EndpointPort}"
                        });
                    }
                    else
                    {
                        throw new NotFoundInterfaceException();
                    }
                }

                _WGConfigService.SetPeerSections(WGInterfaceName.SYNTROPY_PUBLIC, publicPeerSection);
                _WGConfigService.SetPeerSections(WGInterfaceName.SYNTROPY_SDN1, sdn1PeerSection);
                _WGConfigService.SetPeerSections(WGInterfaceName.SYNTROPY_SDN2, sdn2PeerSection);
                _WGConfigService.SetPeerSections(WGInterfaceName.SYNTROPY_SDN3, sdn3PeerSection);
            }
        }

        private bool CheckPublicKeyAndPort(ConfigInfoRequest request)
        {
            bool check = true;

            if (request.Data.Network.Public.PublicKey != _WGConfigService.GetPublicKey(WGInterfaceName.SYNTROPY_PUBLIC) ||
                request.Data.Network.Sdn1.PublicKey != _WGConfigService.GetPublicKey(WGInterfaceName.SYNTROPY_SDN1) ||
                request.Data.Network.Sdn2.PublicKey != _WGConfigService.GetPublicKey(WGInterfaceName.SYNTROPY_SDN2) ||
                request.Data.Network.Sdn3.PublicKey != _WGConfigService.GetPublicKey(WGInterfaceName.SYNTROPY_SDN3))
                check = false;

            if (request.Data.Network.Public.ListenPort != _WGConfigService.GetListenPort(WGInterfaceName.SYNTROPY_PUBLIC) ||
                request.Data.Network.Sdn1.ListenPort != _WGConfigService.GetListenPort(WGInterfaceName.SYNTROPY_SDN1) ||
                request.Data.Network.Sdn2.ListenPort != _WGConfigService.GetListenPort(WGInterfaceName.SYNTROPY_SDN2) ||
                request.Data.Network.Sdn3.ListenPort != _WGConfigService.GetListenPort(WGInterfaceName.SYNTROPY_SDN3))
                check = false;

            return check;
        }

        private UpdateAgentConfigRequest<CreateInterface> SetPublicKeyAndPort(ConfigInfoRequest request)
        {
            var publicInterfaceSection = _WGConfigService.GetInterfaceSection(WGInterfaceName.SYNTROPY_PUBLIC);
            var sdn1InterfaceSection = _WGConfigService.GetInterfaceSection(WGInterfaceName.SYNTROPY_SDN1);
            var sdn2InterfaceSection = _WGConfigService.GetInterfaceSection(WGInterfaceName.SYNTROPY_SDN2);
            var sdn3InterfaceSection = _WGConfigService.GetInterfaceSection(WGInterfaceName.SYNTROPY_SDN3);

            var data = new List<CreateInterface>();

            if (!_networkInformationService.CheckPing(request.Data.Network.Public.InternalIp))
            {
                data.Add(new CreateInterface
                {
                    Data = new CreateInterfaceData
                    {
                        Ifname = _WGConfigService.GetInterfaceName(WGInterfaceName.SYNTROPY_PUBLIC),
                        InternalIp = request.Data.Network.Public.InternalIp,
                        PublicKey = _WGConfigService.GetPublicKey(WGInterfaceName.SYNTROPY_PUBLIC),
                        ListenPort = _WGConfigService.GetListenPort(WGInterfaceName.SYNTROPY_PUBLIC),
                    }
                });

                publicInterfaceSection.Address = new List<string>() { request.Data.Network.Public.InternalIp };
            }
            else
            {
                SendError(request.Id, request.Data.Network.Public.InternalIp);
            }

            if (!_networkInformationService.CheckPing(request.Data.Network.Sdn1.InternalIp))
            {
                data.Add(new CreateInterface
                {
                    Data = new CreateInterfaceData
                    {
                        Ifname = _WGConfigService.GetInterfaceName(WGInterfaceName.SYNTROPY_SDN1),
                        InternalIp = request.Data.Network.Sdn1.InternalIp,
                        PublicKey = _WGConfigService.GetPublicKey(WGInterfaceName.SYNTROPY_SDN1),
                        ListenPort = _WGConfigService.GetListenPort(WGInterfaceName.SYNTROPY_SDN1),
                    }
                });

                sdn1InterfaceSection.Address = new List<string>() { request.Data.Network.Sdn1.InternalIp };
            }
            else
            {
                SendError(request.Id, request.Data.Network.Sdn1.InternalIp);
            }

            if (!_networkInformationService.CheckPing(request.Data.Network.Sdn2.InternalIp))
            {
                data.Add(new CreateInterface
                {
                    Data = new CreateInterfaceData
                    {
                        Ifname = _WGConfigService.GetInterfaceName(WGInterfaceName.SYNTROPY_SDN2),
                        InternalIp = request.Data.Network.Sdn2.InternalIp,
                        PublicKey = _WGConfigService.GetPublicKey(WGInterfaceName.SYNTROPY_SDN2),
                        ListenPort = _WGConfigService.GetListenPort(WGInterfaceName.SYNTROPY_SDN2),
                    }
                });

                sdn2InterfaceSection.Address = new List<string>() { request.Data.Network.Sdn2.InternalIp };
            }
            else
            {
                SendError(request.Id, request.Data.Network.Sdn2.InternalIp);
            }

            if (!_networkInformationService.CheckPing(request.Data.Network.Sdn3.InternalIp))
            {
                data.Add(new CreateInterface
                {
                    Data = new CreateInterfaceData
                    {
                        Ifname = _WGConfigService.GetInterfaceName(WGInterfaceName.SYNTROPY_SDN3),
                        InternalIp = request.Data.Network.Sdn3.InternalIp,
                        PublicKey = _WGConfigService.GetPublicKey(WGInterfaceName.SYNTROPY_SDN3),
                        ListenPort = _WGConfigService.GetListenPort(WGInterfaceName.SYNTROPY_SDN3),
                    }
                });

                sdn3InterfaceSection.Address = new List<string>() { request.Data.Network.Sdn3.InternalIp };
            }
            else
            {
                SendError(request.Id, request.Data.Network.Sdn3.InternalIp);
            }

            _WGConfigService.SetInterfaceSection(WGInterfaceName.SYNTROPY_PUBLIC, publicInterfaceSection);
            _WGConfigService.SetInterfaceSection(WGInterfaceName.SYNTROPY_SDN1, sdn1InterfaceSection);
            _WGConfigService.SetInterfaceSection(WGInterfaceName.SYNTROPY_SDN2, sdn2InterfaceSection);
            _WGConfigService.SetInterfaceSection(WGInterfaceName.SYNTROPY_SDN3, sdn3InterfaceSection);

            var updateRequest = new UpdateAgentConfigRequest<CreateInterface>
            {
                Id = $"Id{DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond}",
                Data = data
            };

            return updateRequest;
        }

        private void SendError(string idMsg, string intenalIp)
        {
            var error = new UpdateAgentConfigError()
            {
                Id = idMsg,
                Error = new UpdateAgentConfigErrorData
                {
                    Message = $"IP {intenalIp} is already in use."
                }
            };

            var message = JsonConvert.SerializeObject(error,
                JsonSettings.GetSnakeCaseNamingStrategy());
            Debug.WriteLine($"'UPDATE_AGENT_CONF' error: { message}");
            Client.Send(message);
        }
    }
}
