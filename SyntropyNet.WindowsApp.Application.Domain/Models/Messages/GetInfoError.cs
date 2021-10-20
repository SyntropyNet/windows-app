using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyntropyNet.WindowsApp.Application.Domain.Models.Messages
{
    public class GetInfoError : BaseMessage
    {
        public GetInfoError()
        {
            Type = "GET_INFO";
        }

        public GetInfoErrorData Error { get; set; }
    }

    public class GetInfoErrorData
    {
        public string Messages { get; set; }
        public string Stacktrace { get; set; }
    }
}
