using SyntropyNet.WindowsApp.Application.Domain.Enums.WireGuard;
using SyntropyNet.WindowsApp.Application.Domain.Models.WireGuard;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyntropyNet.WindowsApp.Application.Contracts
{
    public interface IWGConfigService : IDisposable
    {
        bool ActivityState { get; }

        void RunWG();
        void StopWG();
        void ApplyModifiedConfigs();

        string GetPublicKey(WGInterfaceName interfaceName);
        string GetInterfaceName(WGInterfaceName interfaceName);
        int GetListenPort(WGInterfaceName interfaceName);

        Interface GetInterfaceSection(WGInterfaceName interfaceName);
        void SetInterfaceSection(WGInterfaceName interfaceName, Interface interfaceSection);
        
        IEnumerable<Peer> GetPeerSections(WGInterfaceName interfaceName);
        void SetPeerSections(WGInterfaceName interfaceName, IEnumerable<Peer> peers);

        IEnumerable<PeerDataFromPipe> GetPeersDataFromPipe(WGInterfaceName interfaceName);
        void Dispose();
    }
}
