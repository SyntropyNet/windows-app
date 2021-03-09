using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyntropyNet.WindowsApp.Application.Domain.Models.Messages
{
    public class WGConfError : BaseMessage
    {
        public WGConfError()
        {
            Type = "WG_CONF";
        }

        public WGConfErrorData Error { get; set; }
    }

    public class WGConfErrorData
    {
        public string Message { get; set; }
        public string Stacktrace { get; set; }
    }
}
