using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyntropyNet.WindowsApp.Application.Domain.Models.Messages {
    public class IfacesPeersActiveDataRequest : BaseMessage {
        public IfacesPeersActiveDataRequest() {
            Id = $"Id{DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond}";
            Type = "IFACES_PEERS_ACTIVE_DATA";
        }
        public IEnumerable<IfacesPeersActiveDataRequestData> Data { get; set; }
    }

    public class IfacesPeersActiveDataRequestData {
        public IfacesPeersActiveDataRequestData() {
        }

        public int ConnectionId { get; set; }
        public string Timestamp { get; set; }
    }
}
