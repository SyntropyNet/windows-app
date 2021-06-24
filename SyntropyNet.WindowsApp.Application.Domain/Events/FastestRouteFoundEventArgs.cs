using SyntropyNet.WindowsApp.Application.Domain.Enums.WireGuard;
using System;

namespace SyntropyNet.WindowsApp.Application.Domain.Events {
    public class FastestRouteFoundEventArgs : EventArgs {
        public WGInterfaceName InterfaceName { get; set; }
        public string Ip { get; set; }
        public string Gateway { get; set; }
        public string Mask { get; set; }
        public int Metric { get; set; }
    }
}
