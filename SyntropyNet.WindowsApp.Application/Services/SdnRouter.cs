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
        private bool _pingStarted = false;

        // There should be an interface and the IP with the less latency
        private WGInterfaceName _fastestInterfaceName = WGInterfaceName.SYNTROPY_PUBLIC; // Assign default for now
        private string _fastestInterfaceGateway = String.Empty;
        private string _fastestIp = String.Empty;
        private string _fastestIpPrevious = String.Empty;
        private string _fastestPeer = String.Empty;

        private Thread runnerThread;
        public delegate void FastestIpFoundHandler(object sender, FastestRouteFoundEventArgs eventArgs);
        public event FastestIpFoundHandler FastestIpFound; 
        public Dictionary<WGInterfaceName, InterfaceInfo> InterfaceInfos { get; set; }

        private IEnumerable<string> _GetCommonIps() {
            IEnumerable<IEnumerable<string>> allAllowedIps = InterfaceInfos.SelectMany(x => x.Value.Peers.Select(p => p.AllowedIPs));

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

            string IInfoJson = JsonConvert.SerializeObject(InterfaceInfos);

            IEnumerable<string> commonIps = _GetCommonIps();

            long minLatency = long.MaxValue; // Current minimal latency
            bool hasPingedIps = false; // will be "true" if atleast one IP was successfully pinged below

            List<LatencyPingRequest> pingRequests = new List<LatencyPingRequest>();
            List<LatencyPingResponse> pingResponses = new List<LatencyPingResponse>();

            // Collect requests
            lock (_interfaceInfosLock) {
                foreach (var entry in InterfaceInfos) {
                    // Select peers that contain common IPs
                    foreach (Peer peer in entry.Value.Peers.Where(p => p.AllowedIPs.Any(ip => commonIps.Contains(ip)))) {
                        IEnumerable<string> nonCommonIps = peer.AllowedIPs.Except(commonIps);

                        foreach (string ip in nonCommonIps) {
                            string strippedIp = ip.Split('/')[0]; // STRIP PORT NUMBER

                            // Skip if we already going to ping this IP in the interface
                            if (pingRequests.Any(r => r.InterfaceName == entry.Key && r.Ip == strippedIp)) {
                                continue;
                            }

                            pingRequests.Add(new LatencyPingRequest {
                                InterfaceName = entry.Key,
                                InterfaceGateway = entry.Value.Gateway,
                                PeerEndpoint = peer.Endpoint,
                                Ip = strippedIp
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

                hasPingedIps = true;

                if (response.Latency < minLatency) {
                    _fastestInterfaceGateway = response.InterfaceGateway;
                    _fastestIp = response.Ip;
                    _fastestInterfaceName = response.InterfaceName;
                    _fastestPeer = response.PeerEndpoint;
                    minLatency = response.Latency;
                }
            }

            if (hasPingedIps) {
                foreach (string ip in commonIps) {
                    IPNetwork network = IPNetwork.Parse(ip);

                    _OnFastestIpFound(new FastestRouteFoundEventArgs {
                        Ip = network.Network,
                        Gateway = _fastestInterfaceGateway,
                        InterfaceName = _fastestInterfaceName,
                        Mask = network.Netmask,
                        Metric = RouteTableConstants.Metric,
                        FastestIp = _fastestIp,
                        PrevFastestIp = _fastestIpPrevious,
                        PeerEndpoint = _fastestPeer
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

                InterfaceInfos[interfaceName].Gateway = interfaceGateway;
                InterfaceInfos[interfaceName].Peers = peers?.ToList();
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
