namespace SyntropyNet.WindowsApp.Application.Domain.Models.Messages {
    public class LatencyPingResponse : LatencyPingRequest {
        public long Latency { get; set; }
        public bool Success { get; set; }

        public LatencyPingResponse(LatencyPingRequest request) {
            this.InterfaceName = request.InterfaceName;
            this.InterfaceGateway = request.InterfaceGateway;
            this.PeerEndpoint = request.PeerEndpoint;
            this.ConnectionId = request.ConnectionId;
            this.Ip = request.Ip;
        }
    }
}
