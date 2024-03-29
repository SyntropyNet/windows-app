﻿using log4net;
using Newtonsoft.Json;
using SyntropyNet.WindowsApp.Application.Comparers;
using SyntropyNet.WindowsApp.Application.Constants;
using SyntropyNet.WindowsApp.Application.Contracts;
using SyntropyNet.WindowsApp.Application.Domain.Enums.WireGuard;
using SyntropyNet.WindowsApp.Application.Domain.Helpers;
using SyntropyNet.WindowsApp.Application.Domain.Models.Messages;
using SyntropyNet.WindowsApp.Application.Domain.Models.WireGuard;
using SyntropyNet.WindowsApp.Application.Helpers;
using SyntropyNet.WindowsApp.Application.Services.WireGuard;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Markup;
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
                        WGRouteStatusRequest controllerRequest = new WGRouteStatusRequest();
                        var doOptimize = true;
                        List<Peer> activeIfPeers = _WGConfigService.GetPeerSections(SdnRouter.Instance.FastestInterfaceName).ToList();
                        // Check if all peers of Active interface were removed
                        // then do Optimize
                        foreach(var aPeer in activeIfPeers)
                        {
                            if(!request.Data.Any(x => x.Fn == "remove_peer"
                                && _WGConfigService.GetWGInterfaceNameFromString(x.Args.Ifname) == SdnRouter.Instance.FastestInterfaceName
                                && x.Args.PublicKey == aPeer.PublicKey))
                            {
                                doOptimize = false;
                                break;
                            }
                        }


                        foreach (var item in request.Data.OrderBy(x => x, new WGConfRequestDataComparer()))
                        {
                            switch (item.Fn)
                            {
                                case "create_interface":
                                    CreateInterface(request.Id, item);
                                    break;
                                case "remove_interface":
                                    // do an optimize if we remove the active interface
                                    var currentInterfce = _WGConfigService.GetWGInterfaceNameFromString(item.Args.Ifname);
                                    if(currentInterfce == SdnRouter.Instance.FastestInterfaceName)
                                    {
                                        doOptimize = true;
                                    }
                                    RemoveInterace(item);
                                    break;
                                case "add_peer":
                                    WGRouteStatusData dataToAdd = AddPeer(item);
                                    controllerRequest.AddRouteStatusData(dataToAdd);
                                    break;
                                case "remove_peer":
                                    RemovePeer(item);
                                    break;
                            }
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

                        if (doOptimize)
                        {
                            _WGConfigService.ChangeActiveRouteEvent();
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
                AllowedIPs = data.Args.AllowedIps,
                Endpoint = data.Args.EndpointIpv4 != null ? $"{data.Args.EndpointIpv4}:{data.Args.EndpointPort}" : null,
                ConnectionId = data.Metadata.ConnectionId,
                ConnectionGroupId = data.Metadata.ConnectionGroupId,
                PublicKey = data.Args.PublicKey
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
                        ConnectionGroupId = data.Metadata.ConnectionGroupId,
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
                ConnectionGroupId = data.Metadata.ConnectionGroupId,
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
