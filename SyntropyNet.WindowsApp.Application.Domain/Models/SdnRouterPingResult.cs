using SyntropyNet.WindowsApp.Application.Domain.Enums.WireGuard;

namespace SyntropyNet.WindowsApp.Application.Domain.Models {
    public class SdnRouterPingResult {
        private int _hashCode = 0;

        public bool PacketLoss { get; set; }
        public long Latency { get; set; }
        public string Ip { get; set; }
        public string Peer { get; set; }
        public string Gateway { get; set; }
        public int ConnectionId { get; set; }
        public WGInterfaceName InterfaceName { get; set; }

        public int Hash {
            get {
                if (_hashCode == 0) {
                    string[] keyParts = new string[] {
                        Ip,
                        Peer,
                        Gateway,
                        InterfaceName.ToString(),
                        ConnectionId.ToString()
                    };

                    string keyString = string.Join("", keyParts);
                    _hashCode = keyString.GetHashCode();
                }

                return _hashCode;
            }
        }
    }
}
