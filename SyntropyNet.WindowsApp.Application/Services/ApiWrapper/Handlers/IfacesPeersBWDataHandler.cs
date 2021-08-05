using log4net;
using Newtonsoft.Json;
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
        private static readonly ILog log = LogManager.GetLogger(typeof(IfacesPeersBWDataHandler));

        private readonly bool DebugLogger;
        private static int REFRESH_INFO = 10000;
        private readonly IWGConfigService _WGConfigService;
        private readonly IAppSettings _appSettings;
        private readonly IHttpRequestService _httpRequestService;

        private Thread mainTask;
        public IfacesPeersBWDataHandler(
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

        public void Start()
        {
            mainTask?.Abort();

            mainTask = new Thread(async () =>
            {
                try
                {
                    while (true)
                    {
                        var data = new List<IfacesPeersBWDataRequestData>();
                        IEnumerable<WGInterfaceName> interfaces = Enum.GetValues(typeof(WGInterfaceName)).Cast<WGInterfaceName>();

                        // Collect data
                        Parallel.ForEach(interfaces, i => data.Add(_GetPeerBWDataHelper(i)));

                        var ifacesPeersBWDataRequest = new IfacesPeersBWDataRequest
                        {
                            Data = data
                        };

                        var message = JsonConvert.SerializeObject(ifacesPeersBWDataRequest,
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

                        Thread.Sleep(REFRESH_INFO);
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
                    peerForRequest.LatencyMs = null;
                    peerForRequest.PacketLoss = 1;
                    continue;
                }
                catch (PingException ex)
                {
                    peerForRequest.Status = IfacesPeersBWDataRequestStatus.OFFLINE;
                    peerForRequest.LatencyMs = null;
                    peerForRequest.PacketLoss = 1;
                    continue;
                }
            }

            return peersForRequest;
        }

        private string GetInternalIp(WGInterfaceName interfaceName, IEnumerable<string> allowedIPs)
        {
            Interface @interface = _WGConfigService.GetInterfaceSection(interfaceName);
            string address = @interface.Address != null ? @interface.Address.FirstOrDefault() : null;

            if (string.IsNullOrEmpty(address))
                return "";

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

        private List<IfacesPeersBWDataRequestPeer> JuxtaposeData(List<IfacesPeersBWDataRequestPeer> peersForRequest, WGInterfaceName interfaceName)
        {
            var peersDataFromPipe = _WGConfigService.GetPeersDataFromPipe(interfaceName);

            Thread.Sleep(1000);

            var peersDataFromPipeDelta = _WGConfigService.GetPeersDataFromPipe(interfaceName);

            foreach (var peerForRequest in peersForRequest)
            {
                var peerDataDelta = peersDataFromPipeDelta.FirstOrDefault(x => x.Endpoint == peerForRequest.Endpoint);
                if (peerDataDelta == null) {
                    continue;
                }

                peerForRequest.KeepAliveInterval = peerDataDelta.KeepAliveInterval;
                peerForRequest.LastHandshake = DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt32(peerDataDelta.LastHandshake)).DateTime.ToString("yyyy-MM-ddTHH:mm:ss");
                peerForRequest.RxBytes = peerDataDelta.RxBytes;
                peerForRequest.TxBytes = peerDataDelta.TxBytes;

                var peerDataInitial = peersDataFromPipe.FirstOrDefault(x => x.Endpoint == peerForRequest.Endpoint);
                if (peerDataInitial != null) {
                    // Calculate speed

                    peerForRequest.TxSpeedMbps = Math.Round((peerDataDelta.TxBytes - peerDataInitial.TxBytes) / 1000000.0, 4);
                    peerForRequest.RxSpeedMbps = - Math.Round((peerDataDelta.RxBytes - peerDataInitial.RxBytes) / 1000000.0, 4);
                }
            }

            return peersForRequest;
        }

        private IfacesPeersBWDataRequestData _GetPeerBWDataHelper(WGInterfaceName interfaceName) {
            IEnumerable<Peer> peersInConfig = _WGConfigService.GetPeerSections(interfaceName);
            List<IfacesPeersBWDataRequestPeer> peersForRequest = new List<IfacesPeersBWDataRequestPeer>();

            if (peersInConfig != null && peersInConfig.Count() > 0) {
                foreach (var peer in peersInConfig) {
                    peersForRequest.Add(new IfacesPeersBWDataRequestPeer {
                        PublicKey = peer.PublicKey,
                        AllowedIps = peer.AllowedIPs,
                        Endpoint = peer.Endpoint,
                        ConnectionId = peer.ConnectionId,
                        InternalIp = GetInternalIp(interfaceName, peer.AllowedIPs),
                    });
                }

                peersForRequest = JuxtaposeData(peersForRequest, interfaceName);
                peersForRequest = SetStatus(peersForRequest);
            }

            return new IfacesPeersBWDataRequestData {
                Iface = _WGConfigService.GetInterfaceName(interfaceName),
                IfacePublicKey = _WGConfigService.GetPublicKey(interfaceName),
                Peers = peersForRequest
            };
        }

        public void Interrupt()
        {
            mainTask?.Abort();
        }
    }
}
