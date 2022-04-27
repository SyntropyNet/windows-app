using SyntropyNet.WindowsApp.Application.Domain.Events;
using SyntropyNet.WindowsApp.Application.Domain.Models.Messages;
using System.Collections.Generic;

namespace SyntropyNet.WindowsApp.Application.Contracts {
    public delegate void RerouteHandler(object sender, RerouteEventArgs eventArgs);

    public interface INetworkInformationService
    {
        IEnumerable<IfaceBWDataRequestData> GetInformNetworkInterface();
        int GetNextFreePort(IEnumerable<int> exceptPort = null);
        bool CheckPing(string ip, int timeout = 1000);
        void AddRoute(string interfaceName, string ip, string mask, string gateway, uint metric);
        bool IsLocalIpAddress(string host);
        void DeleteRoute(string interfaceName, string ip, string mask, string gateway, int metric);
        bool RouteExists(string destinationIP, string gateway);
        void GetDefaultInterface();
        event RerouteHandler RerouteEvent;
    }
}
