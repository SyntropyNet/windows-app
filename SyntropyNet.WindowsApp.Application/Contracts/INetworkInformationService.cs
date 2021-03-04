using SyntropyNet.WindowsApp.Application.Domain.Models.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyntropyNet.WindowsApp.Application.Contracts
{
    public interface INetworkInformationService
    {
        IEnumerable<IfaceBWDataRequestData> GetInformNetworkInterface();
    }
}
