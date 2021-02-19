using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyntropyNet.WindowsApp.Application.Domain.Models.Messages
{
    public class WGConfResponse : BaseMessage
    {
        public WGConfResponse()
        {
            Type = "WG_CONF";
        }

        public WGConfResponseData Data { get; set; }
    }

    public class WGConfResponseData
    {
        public WGConfResponseData()
        {
            Fn = "create_interface";
        }

        public string Fn { get; set; }
        public string Ifname { get; set; }
        public string PublicKey { get; set; }
        public int ListenPort { get; set; }
        public string InternalIp { get; set; }
    }
}
