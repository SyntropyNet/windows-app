using Newtonsoft.Json;
using SyntropyNet.WindowsApp.Application.Contracts;
using SyntropyNet.WindowsApp.Application.Domain.Models.Messages;
using SyntropyNet.WindowsApp.Application.Domain.Models.WireGuard;
using SyntropyNet.WindowsApp.Application.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Websocket.Client;

namespace SyntropyNet.WindowsApp.Application.Services.ApiWrapper.Handlers
{
    public class IfacesPeersBWDataHandler : BaseHandler
    {
        private static int REFRESH_INFO = 10000;
        private readonly IWGConfigService _WGConfigService;

        private Thread mainTask;
        public IfacesPeersBWDataHandler(WebsocketClient client, IWGConfigService WGConfigService) : base(client)
        {
            _WGConfigService = WGConfigService;
        }

        public void Start()
        {
            mainTask?.Abort();

            mainTask = new Thread(async () =>
            {
                while (true)
                {
                    IEnumerable<Peer> peersInConfig = _WGConfigService.GetPeers();
                    List<IfacesPeersBWDataRequestPeer> peersForRequest = new List<IfacesPeersBWDataRequestPeer>();
                    foreach (var peer in peersInConfig)
                    {
                        peersForRequest.Add(new IfacesPeersBWDataRequestPeer 
                        { 
                            PublicKey = peer.PublicKey,
                            AllowedIps = peer.AllowedIPs,
                            Endpoint = peer.Endpoint,
                            InternalIp = GetInternalIp(peer.AllowedIPs),
                        });
                    }

                    peersForRequest = JuxtaposeData(peersForRequest, _WGConfigService.GetPeersDataFromPipe());
                    peersForRequest = SetStatus(peersForRequest);



                    var ifacesPeersBWDataRequest = new IfacesPeersBWDataRequest
                    {
                        Data = new IfacesPeersBWDataRequestData
                        {
                            Iface = _WGConfigService.InterfaceName,
                            IfacePublicKey = _WGConfigService.PublicKey,
                            Peers = peersForRequest
                        }
                    };

                    var message = JsonConvert.SerializeObject(ifacesPeersBWDataRequest,
                        JsonSettings.GetSnakeCaseNamingStrategy());
                    Debug.WriteLine($"IFACES_PEERS_BW_DATA: {message}");
                    Client.Send(message);

                    Thread.Sleep(REFRESH_INFO);
                }

            });

            mainTask.Start();
        }
        
        private List<IfacesPeersBWDataRequestPeer> SetStatus(
            List<IfacesPeersBWDataRequestPeer> peersForRequest)
        {
            foreach (var peerForRequest in peersForRequest)
            {
                Ping pingSender = new Ping();
                PingOptions options = new PingOptions();

                int timeout = 5000;
                try
                {
                    PingReply reply = pingSender.Send(peerForRequest.InternalIp, timeout);
                    var status = reply.Status;
                    var roundtripTime = reply.RoundtripTime;
                    if (status == IPStatus.Success)
                    {
                        if (roundtripTime >= 1000)
                        {
                            peerForRequest.Status = IfacesPeersBWDataRequestStatus.WARNING;
                            peerForRequest.LatencyMs = roundtripTime;
                            peerForRequest.PacketLoss = 0;
                            continue;
                        }

                        peerForRequest.Status = IfacesPeersBWDataRequestStatus.CONNECTED;
                        peerForRequest.LatencyMs = roundtripTime;
                        peerForRequest.PacketLoss = 0;
                        continue;
                    }

                    peerForRequest.Status = IfacesPeersBWDataRequestStatus.OFFLINE;
                    peerForRequest.LatencyMs = roundtripTime;
                    peerForRequest.PacketLoss = 1;
                    continue;
                }
                catch (PingException ex)
                {
                    peerForRequest.Status = IfacesPeersBWDataRequestStatus.OFFLINE;
                    peerForRequest.LatencyMs = 0;
                    peerForRequest.PacketLoss = 1;
                    continue;
                }
            }

            return peersForRequest;
        }

        private string GetInternalIp(IEnumerable<string> allowedIPs)
        {
            Interface @interface = _WGConfigService.GetInterface();
            string address = @interface.Address.First();

            foreach (var item in allowedIPs)
            {
                byte[] ip1 = IPAddress.Parse(address).GetAddressBytes();
                byte[] ip2 = IPAddress.Parse(item.Split('/')[0]).GetAddressBytes();

                if (ip1[0] == ip2[0] && ip1[1] == ip2[1])
                {
                    return item.Split('/')[0];
                }
            }
            return "";
        }

        private List<IfacesPeersBWDataRequestPeer> JuxtaposeData(List<IfacesPeersBWDataRequestPeer> peersForRequest, 
            IEnumerable<PeerDataFromPipe> peersDataFromPipe)
        {
            foreach (var peerForRequest in peersForRequest)
            {
                foreach (var peerDataFromPipe in peersDataFromPipe)
                {
                    if(peerForRequest.Endpoint == peerDataFromPipe.Endpoint)
                    {
                        peerForRequest.KeepAliveInterval = peerDataFromPipe.KeepAliveInterval;
                        peerForRequest.LastHandshake = peerDataFromPipe.LastHandshake;
                        peerForRequest.RxBytes = peerDataFromPipe.RxBytes;
                        peerForRequest.TxBytes = peerDataFromPipe.TxBytes;
                        break;
                    }
                }
            }

            return peersForRequest;
        }

        public void Interrupt()
        {
            mainTask?.Abort();
        }
    }
}
