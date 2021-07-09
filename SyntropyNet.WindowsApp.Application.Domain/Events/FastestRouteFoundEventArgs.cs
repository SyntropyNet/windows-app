using SyntropyNet.WindowsApp.Application.Domain.Enums.WireGuard;
using System;
using System.Net;

namespace SyntropyNet.WindowsApp.Application.Domain.Events {
    public class FastestRouteFoundEventArgs : EventArgs {
        public WGInterfaceName InterfaceName { get; set; }
        public IPAddress Ip { get; set; }
        public string Gateway { get; set; }
        public IPAddress Mask { get; set; }
        public int Metric { get; set; }
        public string PeerEndpoint { get; set; }
        public string FastestIp { get; set; }
        public string PrevFastestIp { get; set; }
    }
}
