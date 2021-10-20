using log4net;
using Newtonsoft.Json;
using SyntropyNet.WindowsApp.Application.Comparers;
using SyntropyNet.WindowsApp.Application.Constants;
using SyntropyNet.WindowsApp.Application.Contracts;
using SyntropyNet.WindowsApp.Application.Domain.Enums.WireGuard;
using SyntropyNet.WindowsApp.Application.Domain.Helpers;
using SyntropyNet.WindowsApp.Application.Domain.Models.Messages;
using SyntropyNet.WindowsApp.Application.Domain.Models.WireGuard;
using SyntropyNet.WindowsApp.Application.Exceptions;
using SyntropyNet.WindowsApp.Application.Helpers;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Websocket.Client;

namespace SyntropyNet.WindowsApp.Application.Services.ApiWrapper.Handlers
{
    public class ConfigInfoHandler : BaseHandler
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ConfigInfoHandler));

        private readonly bool DebugLogger;

        private Thread mainTask;
        private readonly IWGConfigService _WGConfigService;
        private readonly INetworkInformationService _networkInformationService;
        private readonly IAppSettings _appSettings;
        private readonly IHttpRequestService _httpRequestService;
        private bool isReconnect = false;

        public ConfigInfoHandler(WebsocketClient client, 
            IWGConfigService WGConfigService,
            INetworkInformationService networkInformationService,
            IAppSettings appSettings,
            IHttpRequestService httpRequestService) 
            : base(client)
        {
            DebugLogger = Convert.ToBoolean(ConfigurationManager.AppSettings.Get("DebugLogger"));

            _WGConfigService = WGConfigService;
            _networkInformationService = networkInformationService;
            _appSettings = appSettings;
            _httpRequestService = httpRequestService;

            _WGConfigService.CreateInterfaceEvent += WGConfigServiceCreateInterfaceEvent;
            _WGConfigService.ErrorCreateInterfaceEvent += WGConfigServiceErrorCreateInterfaceEvent;
        }

        public void Start(ConfigInfoRequest request, bool isReconnect)
        {
            mainTask?.Abort();
            this.isReconnect = isReconnect;

            mainTask = new Thread(async () =>
            {
                try
                {
                    if (!CheckPublicKeyAndPort(request))
                    {
                        var message = JsonConvert.SerializeObject(SetPublicKeyAndPort(request),
                            JsonSettings.GetSnakeCaseNamingStrategy());
                        Client.Send(message);
                        _WGConfigService.ApplyModifiedConfigs();

                        if (DebugLogger)
                            LoggerRequestHelper.Send(
                                Client,
                                log4net.Core.Level.Debug,
                                _appSettings.DeviceId,
                                _appSettings.DeviceName,
                                _appSettings.DeviceIp,
                                message);
                    }

                    if (request.Data.Vpn.Count() != 0)
                    {
                        var interfaces = new List<VpnConfig>();
                        var peers = new List<VpnConfig>();

                        foreach (var item in request.Data.Vpn)
                        {
                            switch (item.Fn)
                            {
                                case "create_interface":
                                    interfaces.Add(item);
                                    break;
                                case "add_peer":
                                    peers.Add(item);
                                    break;
                            }
                        }

                        if (interfaces.Count > 0)
                            CreateInterfaces(interfaces);

                        if (peers.Count > 0)
                        {
                            WGRouteStatusRequest controllerRequest = new WGRouteStatusRequest();

                            foreach (var peer in peers.OrderBy(x => x, new VpnConfigComparer())) {
                                WGRouteStatusData dataToAdd = SetPeers(peer);
                                controllerRequest.AddRouteStatusData(dataToAdd);
                            }

                            if (controllerRequest.Data.NotEmpty())
                            {
                                var message = JsonConvert.SerializeObject(controllerRequest, JsonSettings.GetSnakeCaseNamingStrategy());
                                Client.Send(message);

                                if (DebugLogger)
                                    LoggerRequestHelper.Send(
                                    Client,
                                    log4net.Core.Level.Debug,
                                    _appSettings.DeviceId,
                                    _appSettings.DeviceName,
                                    _appSettings.DeviceIp,
                                    message);
                            }
                        }
                    }
                }
                catch(Exception ex)
                {
                    if (!(ex is System.Threading.ThreadAbortException))
                    {
                        try
                        {
                            LoggerRequestHelper.Send(
                                Client,
                                log4net.Core.Level.Error,
                                _appSettings.DeviceId,
                                _appSettings.DeviceName,
                                _appSettings.DeviceIp,
                                $"[Message: {ex.Message}, stacktrace: {ex.StackTrace}]");
                        }
                        catch (Exception ex2)
                        {
                            log.Error($"[Message: {ex2.Message}, stacktrace: {ex2.StackTrace}]", ex);
                        }
                    }
                    
                }
            });

            mainTask.Start();
        }

        public void Interrupt()
        {
            mainTask?.Abort();
        }

        private void WGConfigServiceCreateInterfaceEvent(object sender, WireGuard.WGConfigServiceEventArgs args)
        {
            if (!DebugLogger)
                return;

            var message = $"[WG_CONF] Creating interface {args?.Interface?.Name}, {args?.Interface?.Interface?.Address?.FirstOrDefault()}";
            if (DebugLogger)
                LoggerRequestHelper.Send(
                    Client,
                    log4net.Core.Level.Debug,
                    _appSettings.DeviceId,
                    _appSettings.DeviceName,
                    _appSettings.DeviceIp,
                    message);
        }

        private void WGConfigServiceErrorCreateInterfaceEvent(object sender, WireGuard.WGConfigServiceEventArgs args)
        {
            var message = $"[WG_CONF] Error creating interface {args?.Interface?.Name}, {args?.Interface?.Interface.Address?.FirstOrDefault()}";
            if (DebugLogger)
                LoggerRequestHelper.Send(
                    Client,
                    log4net.Core.Level.Error,
                    _appSettings.DeviceId,
                    _appSettings.DeviceName,
                    _appSettings.DeviceIp,
                    message);
        }

        private void CreateInterfaces(IEnumerable<VpnConfig> interfaces)
        {
            var publicInterfaceSection = _WGConfigService.GetInterfaceSection(WGInterfaceName.SYNTROPY_PUBLIC);
            var sdn1InterfaceSection = _WGConfigService.GetInterfaceSection(WGInterfaceName.SYNTROPY_SDN1);
            var sdn2InterfaceSection = _WGConfigService.GetInterfaceSection(WGInterfaceName.SYNTROPY_SDN2);
            var sdn3InterfaceSection = _WGConfigService.GetInterfaceSection(WGInterfaceName.SYNTROPY_SDN3);

            foreach (var @interface in interfaces)
            {
                var address = new List<string>();

                if (@interface.Args.Ifname == _WGConfigService.GetInterfaceName(WGInterfaceName.SYNTROPY_PUBLIC))
                {
                    address.Add(@interface.Args.InternalIp);
                    publicInterfaceSection.Address = address;
                    _WGConfigService.SetInterfaceSection(WGInterfaceName.SYNTROPY_PUBLIC, publicInterfaceSection);
                }
                else if(@interface.Args.Ifname == _WGConfigService.GetInterfaceName(WGInterfaceName.SYNTROPY_SDN1))
                {
                    address.Add(@interface.Args.InternalIp);
                    sdn1InterfaceSection.Address = address;
                    _WGConfigService.SetInterfaceSection(WGInterfaceName.SYNTROPY_SDN1, sdn1InterfaceSection);
                }
                else if (@interface.Args.Ifname == _WGConfigService.GetInterfaceName(WGInterfaceName.SYNTROPY_SDN2))
                {
                    address.Add(@interface.Args.InternalIp);
                    sdn2InterfaceSection.Address = address;
                    _WGConfigService.SetInterfaceSection(WGInterfaceName.SYNTROPY_SDN2, sdn2InterfaceSection);
                }
                else if (@interface.Args.Ifname == _WGConfigService.GetInterfaceName(WGInterfaceName.SYNTROPY_SDN3))
                {
                    address.Add(@interface.Args.InternalIp);
                    sdn3InterfaceSection.Address = address;
                    _WGConfigService.SetInterfaceSection(WGInterfaceName.SYNTROPY_SDN3, sdn3InterfaceSection);
                }
            }

            _WGConfigService.ApplyModifiedConfigs();
        }

        private WGRouteStatusData SetPeers(VpnConfig peer)
        {
            WGInterfaceName nameInterfce = _WGConfigService.GetWGInterfaceNameFromString(peer.Args.Ifname);
            List<Peer> WgPeers = _WGConfigService.GetPeerSections(nameInterfce).ToList();

            List<WGRouteStatus> wGRouteStatuses = new List<WGRouteStatus>();
            List<Peer> newPeers = new List<Peer>();
            List<Peer> peersToRemove = new List<Peer>();

            var requestPeer = new Peer
            {
                PublicKey = peer.Args.PublicKey,
                AllowedIPs = peer.Args.AllowedIps,
                Endpoint = peer.Args.EndpointIpv4 != null ? $"{peer.Args.EndpointIpv4}:{peer.Args.EndpointPort}" : null,
                ConnectionId = peer.Metadata.ConnectionId,
                ConnectionGroupId = peer.Metadata.ConnectionGroupId
            };

            if (!isReconnect)
            {
                foreach (var WgPeer in WgPeers)
                {
                    if (EqualPeer(requestPeer, WgPeer))
                    {
                        List<string> allowedIps = new List<string>();
                        foreach (var allowedIpFromRequest in requestPeer.AllowedIPs)
                        {
                            if (WgPeer.AllowedIPs.Contains(allowedIpFromRequest))
                            {
                                wGRouteStatuses.Add(new WGRouteStatus
                                {
                                    Ip = allowedIpFromRequest,
                                    Status = "ERROR",
                                    Msg = $"[WG_CONF] add route failed[{ allowedIpFromRequest }] - already exists"
                                });
                            }
                            else
                            {
                                wGRouteStatuses.Add(new WGRouteStatus
                                {
                                    Ip = allowedIpFromRequest,
                                    Status = "OK"
                                });
                                newPeers.Add(WgPeer);
                            }
                            allowedIps.Add(allowedIpFromRequest);
                        }

                        WgPeer.AllowedIPs = allowedIps;
                        _WGConfigService.SetPeerSections(nameInterfce, WgPeers);
                        if (newPeers.Count > 0)
                            _WGConfigService.SetPeersThroughPipe(nameInterfce, newPeers);
                        return new WGRouteStatusData
                        {
                            ConnectionId = peer.Metadata.ConnectionId,
                            ConnectionGroupId = peer.Metadata.ConnectionGroupId,
                            Statuses = wGRouteStatuses
                        };
                    } else if (requestPeer.Endpoint == WgPeer.Endpoint) {
                        // If peers have the same endpoint we need to remove the old one
                        peersToRemove.Add(WgPeer);
                    }
                }
            }

            foreach (var allowedIpFromRequest in requestPeer.AllowedIPs)
            {
                wGRouteStatuses.Add(new WGRouteStatus
                {
                    Ip = allowedIpFromRequest,
                    Status = "OK",
                    Msg = ""
                });
                newPeers.Add(requestPeer);
            }

            WgPeers.Add(requestPeer);
            _WGConfigService.SetPeerSections(nameInterfce, WgPeers);
            if (!isReconnect && newPeers.Count > 0)
                _WGConfigService.SetPeersThroughPipe(nameInterfce, newPeers);

            if (peersToRemove.Any()) {
                foreach (var peerToRemove in peersToRemove) {
                    _WGConfigService.DeletePeersThroughPipe(nameInterfce, peerToRemove, deleteDuplicate: true);
                }
            }

            return new WGRouteStatusData
            {
                ConnectionId = peer.Metadata.ConnectionId,
                ConnectionGroupId = peer.Metadata.ConnectionGroupId,
                Statuses = wGRouteStatuses
            };
        }

        private bool EqualPeer(Peer peer1, Peer peer2)
        {
            if (peer1.PublicKey != peer2.PublicKey)
                return false;
            if (peer1.Endpoint != peer2.Endpoint)
                return false;
            return true;
        }

        private bool CheckPublicKeyAndPort(ConfigInfoRequest request)
        {
            bool check = true;

            if (request.Data.Network.Public.PublicKey != _WGConfigService.GetPublicKey(WGInterfaceName.SYNTROPY_PUBLIC) ||
                request.Data.Network.Sdn1.PublicKey != _WGConfigService.GetPublicKey(WGInterfaceName.SYNTROPY_SDN1) ||
                request.Data.Network.Sdn2.PublicKey != _WGConfigService.GetPublicKey(WGInterfaceName.SYNTROPY_SDN2) ||
                request.Data.Network.Sdn3.PublicKey != _WGConfigService.GetPublicKey(WGInterfaceName.SYNTROPY_SDN3))
                check = false;

            if ((request.Data.Network.Public.ListenPort != 0 && request.Data.Network.Public.ListenPort != _WGConfigService.GetListenPort(WGInterfaceName.SYNTROPY_PUBLIC)) ||
                (request.Data.Network.Sdn1.ListenPort  != 0 && request.Data.Network.Sdn1.ListenPort != _WGConfigService.GetListenPort(WGInterfaceName.SYNTROPY_SDN1)) ||
                (request.Data.Network.Sdn2.ListenPort != 0 && request.Data.Network.Sdn2.ListenPort != _WGConfigService.GetListenPort(WGInterfaceName.SYNTROPY_SDN2)) ||
                (request.Data.Network.Sdn3.ListenPort != 0 && request.Data.Network.Sdn3.ListenPort != _WGConfigService.GetListenPort(WGInterfaceName.SYNTROPY_SDN3)) )
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

            if (!_networkInformationService.IsLocalIpAddress(request.Data.Network.Public.InternalIp) || 
                (publicInterfaceSection.Address.Contains(request.Data.Network.Public.InternalIp) && _networkInformationService.IsLocalIpAddress(request.Data.Network.Public.InternalIp)))
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

            if (!_networkInformationService.IsLocalIpAddress(request.Data.Network.Sdn1.InternalIp) ||
                (sdn1InterfaceSection.Address.Contains(request.Data.Network.Sdn1.InternalIp) && _networkInformationService.IsLocalIpAddress(request.Data.Network.Sdn1.InternalIp)))
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

            if (!_networkInformationService.IsLocalIpAddress(request.Data.Network.Sdn2.InternalIp) ||
                (sdn2InterfaceSection.Address.Contains(request.Data.Network.Sdn2.InternalIp) && _networkInformationService.IsLocalIpAddress(request.Data.Network.Sdn2.InternalIp)))
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

            if (!_networkInformationService.IsLocalIpAddress(request.Data.Network.Sdn3.InternalIp) ||
                (sdn3InterfaceSection.Address.Contains(request.Data.Network.Sdn3.InternalIp) && _networkInformationService.IsLocalIpAddress(request.Data.Network.Sdn3.InternalIp)))
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
            Client.Send(message);

            if (DebugLogger)
                LoggerRequestHelper.Send(
                    Client,
                    log4net.Core.Level.Debug,
                    _appSettings.DeviceId,
                    _appSettings.DeviceName,
                    _appSettings.DeviceIp,
                    message);
        }
    }
}
