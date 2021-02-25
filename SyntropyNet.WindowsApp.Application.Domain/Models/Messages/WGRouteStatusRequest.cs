using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyntropyNet.WindowsApp.Application.Domain.Models.Messages
{
    public class WGRouteStatusRequest : BaseMessage
    {
        public WGRouteStatusRequest()
        {
            Id = $"Id{DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond}";
            Type = "WG_ROUTE_STATUS";
        }

        public WGRouteStatusData Data { get; set; }
    }

    public class WGRouteStatusData
    {
        public int ConnectionId { get; set; }
        public string PublicKey { get; set; }
        public IEnumerable<WGRouteStatus> Statuses { get; set; }
    }

    public class WGRouteStatus
    {
        public string Status { get; set; }
        public string Ip { get; set; }
        public string Msg { get; set; }
    }
}
