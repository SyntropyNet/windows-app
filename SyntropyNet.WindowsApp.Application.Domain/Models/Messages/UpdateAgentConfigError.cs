using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyntropyNet.WindowsApp.Application.Domain.Models.Messages
{
    public class UpdateAgentConfigError : BaseMessage
    {
        public UpdateAgentConfigError()
        {
            Id = $"Id{DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond}";
            Type = "UPDATE_AGENT_CONF";
        }

        public UpdateAgentConfigErrorData Error { get; set; }
    }

    public class UpdateAgentConfigErrorData
    {
        public UpdateAgentConfigErrorData()
        {
            Type = "ALREADY_IN_USE";
        }
        public string Type { get; set; }
        public string Message { get; set; }
    }
}
