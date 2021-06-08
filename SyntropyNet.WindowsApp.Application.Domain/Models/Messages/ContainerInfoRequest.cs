using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyntropyNet.WindowsApp.Application.Domain.Models.Messages
{
    public class ContainerInfoRequest : BaseMessage
    {
        public ContainerInfoRequest()
        {
            Id = $"Id{DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond}";
            Type = "CONTAINER_INFO";
            Data = new List<ContainerInfo>();
        }

        public IEnumerable<ContainerInfo> Data { get; set; }
    }
}
