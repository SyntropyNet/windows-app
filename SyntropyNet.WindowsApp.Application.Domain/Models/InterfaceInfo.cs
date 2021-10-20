using SyntropyNet.WindowsApp.Application.Domain.Models.WireGuard;
using System.Collections.Generic;

namespace SyntropyNet.WindowsApp.Application.Domain.Models {
    public class InterfaceInfo {
        public string Gateway { get; set; }
        public List<Peer> Peers { get; set; }
    }
}
