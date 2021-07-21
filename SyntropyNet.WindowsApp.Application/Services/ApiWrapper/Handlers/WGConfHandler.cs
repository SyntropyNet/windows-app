using log4net;
using Newtonsoft.Json;
using SyntropyNet.WindowsApp.Application.Comparers;
using SyntropyNet.WindowsApp.Application.Constants;
using SyntropyNet.WindowsApp.Application.Contracts;
using SyntropyNet.WindowsApp.Application.Domain.Enums.WireGuard;
using SyntropyNet.WindowsApp.Application.Domain.Models.Messages;
using SyntropyNet.WindowsApp.Application.Domain.Models.WireGuard;
using SyntropyNet.WindowsApp.Application.Helpers;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Websocket.Client;

namespace SyntropyNet.WindowsApp.Application.Services.ApiWrapper.Handlers
{
    public class WGConfHandler : BaseHandler
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(WGConfHandler));

        private readonly bool DebugLogger;
        private Thread mainTask;
        private readonly IWGConfigService _WGConfigService;
        private readonly IAppSettings _appSettings;
        private readonly IHttpRequestService _httpRequestService;

        public WGConfHandler(
            WebsocketClient client,
            IWGConfigService WGConfigService,
            IAppSettings appSettings,
            IHttpRequestService httpRequestService)
            : base(client)
        {
            DebugLogger = Convert.ToBoolean(ConfigurationManager.AppSettings.Get("DebugLogger"));

            _WGConfigService = WGConfigService;
            _appSettings = appSettings;
            _httpRequestService = httpRequestService;
        }

        public void Start(WGConfRequest request)
        {
            mainTask?.Abort();

            mainTask = new Thread(async () =>
            {
                try
                {
                    if (request.Data.Count() > 0)
                    {
                        List<WGRouteStatusData> WGRouteStatusDataResponse = new List<WGRouteStatusData>();

                        foreach (var item in request.Data.OrderBy(x => x, new WGConfRequestDataComparer()))
                        {
                            switch (item.Fn)
                            {
                                case "create_interface":
                                    CreateInterface(request.Id, item);
                                    break;
                                case "remove_interface":
                                    RemoveInterace(item);
                                    break;
                                case "add_peer":
                                    WGRouteStatusDataResponse.Add(AddPeer(item));
                                    break;
                                case "remove_peer":
                                    RemovePeer(item);
                                    break;
                            }
                        }

                        if(WGRouteStatusDataResponse.Count > 0)
                        {
                            var message = JsonConvert.SerializeObject(new WGRouteStatusRequest
                            {
                                Data = WGRouteStatusDataResponse
                            }, JsonSettings.GetSnakeCaseNamingStrategy());
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
                catch (Exception ex)
                {
                    if (!(ex is System.Threading.ThreadAbortException))
                    {
                        try
                        {
                            var errorMsg = new WGConfError
                            {
                                Id = request.Id,
                                Error = new WGConfErrorData
                                {
                                    Message = ex.Message,
                                    Stacktrace = ex.StackTrace
                                }
                            };

                            var message = JsonConvert.SerializeObject(errorMsg,
                                JsonSettings.GetSnakeCaseNamingStrategy());
                            Client.Send(message);

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

        public bool IsAlive()
        {
            return mainTask?.IsAlive ?? false;
        }

        public void Interrupt()
        {
            mainTask?.Abort();
        }

        private void CreateInterface(string idRequest, WGConfRequestData data)
        {
            var nameInterfce = _WGConfigService.GetWGInterfaceNameFromString(data.Args.Ifname);

            var WGConfResponse = new WGConfResponse
            {
                Id = idRequest,
                Data = new WGConfResponseData
                {
                    Ifname = _WGConfigService.GetInterfaceName(nameInterfce),
                    InternalIp = data.Args.InternalIp,
                    ListenPort = _WGConfigService.GetListenPort(nameInterfce),
                    PublicKey = _WGConfigService.GetPublicKey(nameInterfce)
                }
            };

            var message = JsonConvert.SerializeObject(WGConfResponse, 
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

        private void RemoveInterace(WGConfRequestData data)
        {
            var nameInterfce = _WGConfigService.GetWGInterfaceNameFromString(data.Args.Ifname);
            _WGConfigService.RemoveInterface(nameInterfce);
            _WGConfigService.ApplyModifiedConfigs();
        }

        private WGRouteStatusData AddPeer(WGConfRequestData data)
        {
            var nameInterfce = _WGConfigService.GetWGInterfaceNameFromString(data.Args.Ifname);
            List<Peer> WgPeers = _WGConfigService.GetPeerSections(nameInterfce).ToList();
            
            List<WGRouteStatus> wGRouteStatuses = new List<WGRouteStatus>();
            List<Peer> newPeers = new List<Peer>();

            var requestPeer = new Peer
            {
                PublicKey = data.Args.PublicKey,
                AllowedIPs = data.Args.AllowedIps,
                Endpoint = data.Args.EndpointIpv4 != null ? $"{data.Args.EndpointIpv4}:{data.Args.EndpointPort}" : null,
                ConnectionId = data.Metadata.ConnectionId
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
                    if(newPeers.Count > 0)
                        _WGConfigService.SetPeersThroughPipe(nameInterfce, newPeers);
                    return new WGRouteStatusData
                    {
                        ConnectionId = data.Metadata.ConnectionId,
                        PublicKey = data.Args.PublicKey,
                        Statuses = wGRouteStatuses
                    };
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
            if (newPeers.Count > 0)
                _WGConfigService.SetPeersThroughPipe(nameInterfce, newPeers);
            return new WGRouteStatusData
            {
                ConnectionId = data.Metadata.ConnectionId,
                PublicKey = data.Args.PublicKey,
                Statuses = wGRouteStatuses
            };
        }

        private void RemovePeer(WGConfRequestData data)
        {
            foreach (WGInterfaceName interfaceName in Enum.GetValues(typeof(WGInterfaceName)))
            {
                List<Peer> WgPeers = _WGConfigService.GetPeerSections(interfaceName).ToList();

                foreach (var WgPeer in WgPeers)
                {
                    if (WgPeer.PublicKey == data.Args.PublicKey)
                    {
                        WgPeers.Remove(WgPeer);
                        _WGConfigService.SetPeerSections(interfaceName, WgPeers);
                        _WGConfigService.DeletePeersThroughPipe(interfaceName, WgPeer);
                        return;
                    }
                }
            }
        }

        private bool EqualPeer(Peer peer1, Peer peer2)
        {
            if (peer1.PublicKey != peer2.PublicKey)
                return false;
            if(peer1.Endpoint != peer2.Endpoint)
                return false;
            return true;
        }

    }
}
