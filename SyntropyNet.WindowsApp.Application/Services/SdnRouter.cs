using log4net;
using Newtonsoft.Json;
using SyntropyNet.WindowsApp.Application.Constants;
using SyntropyNet.WindowsApp.Application.Domain.Enums.WireGuard;
using SyntropyNet.WindowsApp.Application.Domain.Events;
using SyntropyNet.WindowsApp.Application.Domain.Models;
using SyntropyNet.WindowsApp.Application.Domain.Models.Messages;
using SyntropyNet.WindowsApp.Application.Domain.Models.WireGuard;
using SyntropyNet.WindowsApp.Application.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SyntropyNet.WindowsApp.Application.Services {
    // Pings the latency for Ip's 
    public class SdnRouter {
        #region [ SINGLETON ]

        private static readonly ILog _log = LogManager.GetLogger(typeof(SdnRouter));
        private static object _locker = new object();
        private static SdnRouter _instance;

        private SdnRouter() {
            InterfaceInfos = new Dictionary<WGInterfaceName, InterfaceInfo>();
        }

        public static SdnRouter Instance {
            get {
                if (_instance == null) {
                    lock (_locker) {
                        if (_instance == null) {
                            _instance = new SdnRouter();
                        }
                    }
                }

                return _instance;
            }
        }

        #endregion

        private static object _pingLock = new object();
        private static object _interfaceInfosLock = new object();
        private int _pingDelayMs = 1000;
        private int _pingIntervalS = 30;
        private bool _pingStarted = false;

        // There should be an interface and the IP with the less latency
        private WGInterfaceName _fastestInterfaceName = WGInterfaceName.SYNTROPY_PUBLIC; // Assign default for now
        private string _fastestInterfaceGateway = String.Empty;
        private string _fastestIp = String.Empty;
        private string _fastestIpPrevious = String.Empty;
        private string _fastestPeer = String.Empty;
        private IEnumerable<WGInterfaceName> _avaialbleInterfaces = Enum.GetValues(typeof(WGInterfaceName)).Cast<WGInterfaceName>();

        private List<SdnRouterPingResult> _pingResults = new List<SdnRouterPingResult>();
        private DateTime? _lastPingPeriodEnded;

        private Thread runnerThread;
        public delegate void FastestIpFoundHandler(object sender, FastestRouteFoundEventArgs eventArgs);
        public event FastestIpFoundHandler FastestIpFound;
        private Dictionary<WGInterfaceName, InterfaceInfo> InterfaceInfos { get; set; }

        private IEnumerable<string> _GetCommonIps() {
            IEnumerable<IEnumerable<string>> allAllowedIps;

            lock (_interfaceInfosLock) {
                allAllowedIps = InterfaceInfos.SelectMany(x => x.Value.Peers.Select(p => p.AllowedIPs));
            }

            return allAllowedIps.Skip(1)
                                .Aggregate(
                                    new HashSet<string>(allAllowedIps.First()),
                                    (h, e) => { h.IntersectWith(e); return h; }
                                );
        }

        private void _PingInterfaces() {
            if (InterfaceInfos == null || !InterfaceInfos.Any()) {
                return;
            }

            if (_lastPingPeriodEnded == null) {
                _lastPingPeriodEnded = DateTime.Now;
            }

            IEnumerable<string> commonIps = _GetCommonIps();

            if (!commonIps.Any()) {
                return;
            }

            List<LatencyPingRequest> pingRequests = new List<LatencyPingRequest>();
            List<LatencyPingResponse> pingResponses = new List<LatencyPingResponse>();

            // Collect requests
            foreach (var key in _avaialbleInterfaces) {
                if (InterfaceInfos.TryGetValue(key, out InterfaceInfo entry)) {
                    // Select peers that contain common IPs
                    foreach (Peer peer in entry.Peers.Where(p => p.AllowedIPs.Any(ip => commonIps.Contains(ip)))) {
                        IEnumerable<string> nonCommonIps = peer.AllowedIPs.Except(commonIps);

                        foreach (string ip in nonCommonIps) {
                            string strippedIp = ip.Split('/')[0]; // STRIP PORT NUMBER

                            // Skip if we already going to ping this IP in the interface
                            if (pingRequests.Any(r => r.InterfaceName == key && r.Ip == strippedIp)) {
                                continue;
                            }

                            pingRequests.Add(new LatencyPingRequest {
                                InterfaceName = key,
                                InterfaceGateway = entry.Gateway,
                                PeerEndpoint = peer.Endpoint,
                                Ip = strippedIp,
                                ConnectionId = peer.ConnectionId
                            });
                        }
                    }
                }
            }

            _log.Info($"[REROUTING]: ping endpoints");
            Parallel.ForEach(pingRequests, x => pingResponses.Add(PingEndpoint(x)));

            foreach (LatencyPingResponse response in pingResponses) {
                if (response == null) {
                    continue;
                }

                if (!response.Success) {
                    continue;
                }

                _pingResults.Add(new SdnRouterPingResult {
                    Ip = response.Ip,
                    Latency = response.Latency,
                    Gateway = response.InterfaceGateway,
                    InterfaceName = response.InterfaceName,
                    Peer = response.PeerEndpoint,
                    ConnectionId = response.ConnectionId
                });
            }

            if ((DateTime.Now - _lastPingPeriodEnded.Value).TotalSeconds >= _pingIntervalS) {
                if (_pingResults.Any()) {
                    long minSumLatency = long.MaxValue; // Current minimal latency
                    int connectionId = 0;

                    IEnumerable<IGrouping<int, SdnRouterPingResult>> groupedResults = _pingResults.GroupBy(x => x.Hash);
                    
                    foreach (var group in groupedResults) {
                        long totalLatency = group.Sum(x => x.Latency);
                        SdnRouterPingResult info = group.First();

                        if (totalLatency < minSumLatency) {
                            _fastestInterfaceGateway = info.Gateway;
                            _fastestIp = info.Ip;
                            _fastestInterfaceName = info.InterfaceName;
                            _fastestPeer = info.Peer;
                            minSumLatency = totalLatency;
                            connectionId = info.ConnectionId;
                        }
                    }

                    foreach (string ip in commonIps) {
                        IPNetwork network = IPNetwork.Parse(ip);

                        _OnFastestIpFound(new FastestRouteFoundEventArgs {
                            Ip = network.Network,
                            Gateway = _fastestInterfaceGateway,
                            InterfaceName = _fastestInterfaceName,
                            Mask = network.Netmask,
                            FastestIp = _fastestIp,
                            PrevFastestIp = _fastestIpPrevious,
                            PeerEndpoint = _fastestPeer,
                            ConnectionId = connectionId
                        });
                    }

                    if (!String.IsNullOrEmpty(_fastestIpPrevious) && _fastestIpPrevious != _fastestIp) { // Check latency improvements
                        List<LatencyPingRequest> checkLatencyRequests = new List<LatencyPingRequest> {
                            new LatencyPingRequest { Ip = _fastestIpPrevious },
                            new LatencyPingRequest { Ip = _fastestIp }
                        };
                        List<LatencyPingResponse> checkLatencyResponses = new List<LatencyPingResponse>();
                        Parallel.ForEach(checkLatencyRequests, x => checkLatencyResponses.Add(PingEndpoint(x)));

                        LatencyPingResponse prevIpResponse = checkLatencyResponses.First(x => x.Ip == _fastestIpPrevious);
                        LatencyPingResponse newIpResponse = checkLatencyResponses.First(x => x.Ip == _fastestIp);

                        long? latencyDiff;

                        if (prevIpResponse.Success && newIpResponse.Success) {
                            latencyDiff = newIpResponse.Latency - prevIpResponse.Latency;
                        } else {
                            latencyDiff = null;
                        }

                        if (latencyDiff.HasValue) {
                            foreach (string ip in commonIps) {
                                var logModel = new {
                                    Interface = _fastestInterfaceName.ToString(),
                                    Destination = IpHelper.StripPortNumber(ip),
                                    Peer = IpHelper.StripPortNumber(_fastestPeer),
                                    FastestPingedIp = _fastestIp,
                                    LatencyDelta = latencyDiff.Value
                                };

                                _log.Info($"[REROUTING]: reroute {JsonConvert.SerializeObject(logModel)}");
                            }
                        }
                    }

                    _fastestIpPrevious = _fastestIp;
                    _pingResults.Clear();
                }

                _lastPingPeriodEnded = DateTime.Now;
            }
        }

        private void _OnFastestIpFound(FastestRouteFoundEventArgs args) {
            FastestIpFoundHandler handler = FastestIpFound;
            handler?.Invoke(this, args);
        }

        private LatencyPingResponse PingEndpoint(LatencyPingRequest request) {
            using (Ping pinger = new Ping()) {
                IPAddress parsedIp = IPAddress.Parse(request.Ip);

                try {
                    PingReply reply = pinger.Send(parsedIp, 1000);
                    long latency = 0;
                    bool success = false;

                    if (reply.Status == IPStatus.Success) {
                        latency = reply.RoundtripTime;
                        success = true;
                    }

                    return new LatencyPingResponse(request) {
                        Latency = latency,
                        Success = success
                    };
                } catch (PingException pingEx) {
                    return new LatencyPingResponse(request) {
                        Latency = 0,
                        Success = false
                    };
                }
            }
        }

        private void _InitPingProcess() {
            runnerThread = new Thread(() => {
                Thread.CurrentThread.IsBackground = true;

                while (true) {
                    // Call Ping logic
                    _PingInterfaces();

                    // Cooldown
                    Thread.Sleep(_pingDelayMs);
                }
            });

            runnerThread.Start();
        }

        public void SetPeers(WGInterfaceName interfaceName, string interfaceGateway, IEnumerable<Peer> peers) {
            lock (_interfaceInfosLock) {
                if (!InterfaceInfos.ContainsKey(interfaceName)) {
                    InterfaceInfos.Add(interfaceName, new InterfaceInfo());
                }

                _log.Info($"[ Pinger peers ] iface: {interfaceName.ToString()} / peers: {String.Join(" / ", peers.Select(x => String.Join(", ", x.AllowedIPs)))}");

                InterfaceInfos[interfaceName].Gateway = interfaceGateway;
                InterfaceInfos[interfaceName].Peers = peers?.ToList();

                // Drop ping results in case a network has been changed
                _lastPingPeriodEnded = DateTime.Now;
                _pingResults.Clear();
            }
        }

        public void StartPing() {
            if (!_pingStarted) {
                lock (_pingLock) {
                    if (!_pingStarted) {
                        _InitPingProcess(); // Start latency ping
                        _pingStarted = true;
                    }
                }
            }
        }

        public void StopPing() {
            if (runnerThread != null) {
                runnerThread.Abort();
                _pingStarted = false;
            }
        }
    }
}
