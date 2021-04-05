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
        int GetNextFreePort(IEnumerable<int> exceptPort = null);
        bool CheckPing(string ip, int timeout = 1000);
        void AddRoute(string interfaceName, string ip, string mask, string gateway, int metric);
    }
}
