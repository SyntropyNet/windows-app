using log4net;
using Newtonsoft.Json;
using SyntropyNet.WindowsApp.Application.Constants;
using SyntropyNet.WindowsApp.Application.Contracts;
using SyntropyNet.WindowsApp.Application.Domain.Enums.WireGuard;
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

        public void Start(ConfigInfoRequest request)
        {
            mainTask?.Abort();

            mainTask = new Thread(async () =>
            {
                try
                {
                    if (!CheckPublicKeyAndPort(request))
                    {
                        var message = JsonConvert.SerializeObject(SetPublicKeyAndPort(request),
                            JsonSettings.GetSnakeCaseNamingStrategy());
                        Debug.WriteLine($"Update agent config: {message}");
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
                            SetPeers(peers);
                    }
                }
                catch(Exception ex)
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
                        log.Error($"[Message: {ex2.Message}, stacktrace: {ex2.StackTrace}]");
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

        private void SetPeers(IEnumerable<VpnConfig> peers)
        {
            if (peers.Count() > 0)
            {
                List<WGRouteStatusData> WGRouteStatusDataResponse = new List<WGRouteStatusData>();

                foreach (var item in peers)
                {
                    var nameInterfce = _WGConfigService.GetWGInterfaceNameFromString(item.Args.Ifname);
                    List<Peer> WgPeers = _WGConfigService.GetPeerSections(nameInterfce).ToList();

                    List<WGRouteStatus> wGRouteStatuses = new List<WGRouteStatus>();

                    var requestPeer = new Peer
                    {
                        PublicKey = item.Args.PublicKey,
                        AllowedIPs = item.Args.AllowedIps,
                        Endpoint = $"{item.Args.EndpointIpv4}:{item.Args.EndpointPort}"
                    };

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
                                        //ToDo: error message 
                                        Msg = ""
                                    });
                                }
                                else
                                {
                                    wGRouteStatuses.Add(new WGRouteStatus
                                    {
                                        Ip = allowedIpFromRequest,
                                        Status = "OK",
                                        Msg = ""
                                    });
                                }
                                allowedIps.Add(allowedIpFromRequest);
                            }

                            WgPeer.AllowedIPs = allowedIps;
                            _WGConfigService.SetPeerSections(nameInterfce, WgPeers);
                            _WGConfigService.SetPeersThroughPipe(nameInterfce);

                            WGRouteStatusDataResponse.Add(new WGRouteStatusData
                            {
                                ConnectionId = item.Metadata.ConnectionId,
                                PublicKey = item.Args.PublicKey,
                                Statuses = wGRouteStatuses
                            });
                        }
                    }
                }

                if (WGRouteStatusDataResponse.Count > 0)
                {
                    var message = JsonConvert.SerializeObject(new WGRouteStatusRequest
                    {
                        Data = WGRouteStatusDataResponse
                    }, JsonSettings.GetSnakeCaseNamingStrategy());
                    Debug.WriteLine($"WG_ROUTE_STATUS,: {message}");
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
