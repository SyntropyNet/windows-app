using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyntropyNet.WindowsApp.Application.Domain.Models.Messages
{
    public class UpdateAgentConfigRequest<DataModel> : BaseMessage
    {
        public UpdateAgentConfigRequest()
        {
            Type = "UPDATE_AGENT_CONFIG";
        }

        public DataModel Data { get; set; }
    }

    public class CreateInterface
    {
        public string Fn { get; set; }

        public CreateInterface()
        {
            Fn = "create_interface";
        }

        public CreateInterfaceData Data { get; set; }
    }

    public class CreateInterfaceData
    {
        public string Ifname { get; set; }
        public string PublicKey { get; set; }
        public string InternalIp { get; set; }
        public int ListenPort { get; set; }
    }
}
