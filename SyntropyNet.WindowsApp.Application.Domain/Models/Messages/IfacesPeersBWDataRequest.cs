using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyntropyNet.WindowsApp.Application.Domain.Models.Messages
{
    public enum IfacesPeersBWDataRequestStatus
    {
        CONNECTED,
        WARNING,
        OFFLINE
    }

    public class IfacesPeersBWDataRequest : BaseMessage
    {
        public IfacesPeersBWDataRequest()
        {
            Id = $"Id{DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond}";
            Type = "IFACES_PEERS_BW_DATA";
        }

        public IEnumerable<IfacesPeersBWDataRequestData> Data { get; set; }
    }

    public class IfacesPeersBWDataRequestData
    {
        public IfacesPeersBWDataRequestData()
        {
            Peers = new List<IfacesPeersBWDataRequestPeer>();
        }
        public string Iface { get; set; }
        public string IfacePublicKey { get; set; }
        public IEnumerable<IfacesPeersBWDataRequestPeer> Peers { get; set; }
    }

    public class IfacesPeersBWDataRequestPeer
    {
        public IfacesPeersBWDataRequestPeer()
        {
            AllowedIps = new List<string>();
            Status = IfacesPeersBWDataRequestStatus.OFFLINE;
        }

        public string PublicKey { get; set; }
        public string LastHandshake { get; set; }
        public int KeepAliveInterval { get; set; }
        public IEnumerable<string> AllowedIps { get; set; }
        public string InternalIp { get; set; }
        public double? LatencyMs { get; set; }
        public double PacketLoss { get; set; }
        [JsonConverter(typeof(StringEnumConverter))]
        public IfacesPeersBWDataRequestStatus Status { get; set; }
        public long RxBytes { get; set; }
        public long TxBytes { get; set; }
        [JsonIgnore]
        public string Endpoint { get; set; }
    }
}
