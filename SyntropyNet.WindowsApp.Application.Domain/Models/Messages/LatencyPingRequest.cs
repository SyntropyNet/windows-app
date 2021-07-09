using SyntropyNet.WindowsApp.Application.Domain.Enums.WireGuard;

namespace SyntropyNet.WindowsApp.Application.Domain.Models.Messages {
    public class LatencyPingRequest {
        public WGInterfaceName InterfaceName { get; set; }
        public string Ip { get; set; }
        public string InterfaceGateway { get; set; }
        public string PeerEndpoint { get; set; }
    }
}
