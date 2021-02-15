using System.Collections;
using System.Collections.Generic;

namespace SyntropyNet.WindowsApp.Application.Domain.Models.Messages
{
    public class AutoPingRequest: BaseMessage
    {
        public AutoPingRequestData Data { get; set; }
    }

    public class AutoPingRequestData
    {
        public IEnumerable<string> Ips { get; set; }
        public int Interval { get; set; }
        public int ResponseLimit { get; set; }
    }
}