using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyntropyNet.WindowsApp.Application.Domain.Models.Messages
{
    public class GetInfoRequest : BaseMessage
    {
        public GetInfoRequestData Data { get; set; }
    }

    public class GetInfoRequestData { }
}
