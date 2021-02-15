using System.Collections.Generic;

namespace SyntropyNet.WindowsApp.Application.Domain.Models.Messages
{
    public class AutoPingResponse: BaseMessage
    {
        public AutoPingResponse()
        {
            Type = "AUTO_PING";
        }

        public AutoPingResponseData Data { get; set; }
    }

    public class AutoPingResponseData
    {
        public IEnumerable<AutoPingResponseItem> Pings { get; set; }
    }

    public class AutoPingResponseItem
    {
        public string Ip { get; set; }
        public long LatencyMs { get; set; }
        public double PacketLoss { get; set; }
    }
}