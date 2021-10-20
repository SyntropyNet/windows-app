using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyntropyNet.WindowsApp.Application.Domain.Models.Messages
{
    public class IfaceBWDataRequest : BaseMessage
    {
        public IfaceBWDataRequest()
        {
            Id = $"Id{DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond}";
            Type = "IFACES_BW_DATA";
            Data = new List<IfaceBWDataRequestData>();
        }

        public IEnumerable<IfaceBWDataRequestData> Data { get; set; }
    }

    public class IfaceBWDataRequestData
    {
        public string Iface { get; set; }
        public double RxSpeedMbsps { get; set; }
        public double TxSpeedMbsps { get; set; }
        public long TxDropped { get; set; }
        public long RxDropped { get; set; }
        public long TxErrors { get; set; }
        public long RxErrors { get; set; }
        public long TxPackets { get; set; }
        public long RxPackets { get; set; }
        public int Interval { get; set; }
    }
}
