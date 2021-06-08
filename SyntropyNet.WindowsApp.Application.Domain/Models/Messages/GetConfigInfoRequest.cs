using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyntropyNet.WindowsApp.Application.Domain.Models.Messages
{
    public class GetConfigInfoRequest : BaseMessage
    {
        public GetConfigInfoRequest()
        {
            Id = $"Id{DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond}";
            Type = "GET_CONFIG_INFO";
            Data = new List<GetConfigInfoRequestData>();
        }

        public IEnumerable<GetConfigInfoRequestData> Data { get; set; }
    }

    public class GetConfigInfoRequestData { }
}
