﻿using Newtonsoft.Json;
using SyntropyNet.WindowsApp.Application.Contracts;
using SyntropyNet.WindowsApp.Application.Domain.Models.Messages;
using SyntropyNet.WindowsApp.Application.Domain.Models.WireGuard;
using SyntropyNet.WindowsApp.Application.Helpers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Websocket.Client;

namespace SyntropyNet.WindowsApp.Application.Services.ApiWrapper.Handlers
{
    public class WGConfHandler : BaseHandler
    {
        private Thread mainTask;
        private readonly IWGConfigService _WGConfigService;

        public WGConfHandler(WebsocketClient client, IWGConfigService WGConfigService)
            : base(client)
        {
            _WGConfigService = WGConfigService;
        }

        public void Start(WGConfRequest request)
        {
            mainTask?.Abort();

            mainTask = new Thread(async () =>
            {
                if(request.Data.Count() > 0)
                {
                    foreach (var item in request.Data)
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
                                AddPeer(item);
                                break;
                            case "remove_peer":
                                RemovePeer(item);
                                break;
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

        private void CreateInterface(string idRequest, WGConfRequestData data)
        {
            Interface WGInterface;
            if (File.Exists(_WGConfigService.PathToConfigFile()))
                WGInterface = _WGConfigService.GetInterface();
            else
            {
                _WGConfigService.CreateConfig();
                WGInterface = _WGConfigService.GetInterface();
            }

            var WGConfResponse = new WGConfResponse
            {
                Id = idRequest,
                Data = new WGConfResponseData
                {
                    Ifname = _WGConfigService.InterfaceName,
                    InternalIp = data.Args.InternalIp,
                    ListenPort = WGInterface.ListenPort,
                    PublicKey = _WGConfigService.PublicKey
                }
            };

            var message = JsonConvert.SerializeObject(WGConfResponse, 
                JsonSettings.GetSnakeCaseNamingStrategy());
            Debug.WriteLine($"WG_CONF response: {message}");
            Client.Send(message);
        }

        private void RemoveInterace(WGConfRequestData data)
        {
            if(data.Args.Ifname == _WGConfigService.InterfaceName)
            {
                _WGConfigService.RemoveConfig();
            }
        }

        private void AddPeer(WGConfRequestData data)
        {
            List<Peer> WgPeers = _WGConfigService.GetPeers().ToList();
            List<WGRouteStatus> wGRouteStatuses = new List<WGRouteStatus>();

            var requestPeer = new Peer
            {
                PublicKey = data.Args.PublicKey,
                AllowedIPs = data.Args.AllowedIps,
                Endpoint = $"{data.Args.EndpointIpv4}:{data.Args.EndpointPort}"
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
                                Status = "OK"
                            });
                        }
                        allowedIps.Add(allowedIpFromRequest);
                    }

                    var WGRouteStatusMessage = JsonConvert.SerializeObject(new WGRouteStatusRequest
                    {
                        Data = new WGRouteStatusData
                        {
                            ConnectionId = data.Metadata.ConnectionId,
                            PublicKey = data.Args.PublicKey,
                            Statuses = wGRouteStatuses
                        }
                    }, JsonSettings.GetSnakeCaseNamingStrategy());
                    Debug.WriteLine($"WG_ROUTE_STATUS,: {WGRouteStatusMessage}");
                    Client.Send(WGRouteStatusMessage);

                    WgPeer.AllowedIPs = allowedIps;
                    _WGConfigService.SetPeers(WgPeers);

                    return;
                }
            }

            foreach (var allowedIpFromRequest in requestPeer.AllowedIPs)
            {
                wGRouteStatuses.Add(new WGRouteStatus
                {
                    Ip = allowedIpFromRequest,
                    Status = "OK"
                });
            }

            var message = JsonConvert.SerializeObject(new WGRouteStatusRequest
            {
                Data = new WGRouteStatusData
                {
                    ConnectionId = data.Metadata.ConnectionId,
                    PublicKey = data.Args.PublicKey,
                    Statuses = wGRouteStatuses
                }
            }, JsonSettings.GetSnakeCaseNamingStrategy());
            Debug.WriteLine($"WG_ROUTE_STATUS,: {message}");
            Client.Send(message);

            WgPeers.Add(requestPeer);
            _WGConfigService.SetPeers(WgPeers);
        }

        private void RemovePeer(WGConfRequestData data)
        {
            List<Peer> WgPeers = _WGConfigService.GetPeers().ToList();

            foreach (var WgPeer in WgPeers)
            {
                if(WgPeer.PublicKey == data.Args.PublicKey)
                {
                    WgPeers.Remove(WgPeer);
                    _WGConfigService.SetPeers(WgPeers);
                    return;
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