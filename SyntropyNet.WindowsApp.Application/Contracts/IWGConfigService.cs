using SyntropyNet.WindowsApp.Application.Domain.Enums.WireGuard;
using SyntropyNet.WindowsApp.Application.Domain.Models.WireGuard;
using SyntropyNet.WindowsApp.Application.Services.WireGuard;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyntropyNet.WindowsApp.Application.Contracts
{
    public interface IWGConfigService : IDisposable
    {
        //event
        event Action<object, WGConfigServiceEventArgs> CreateInterfaceEvent;
        event Action<object, WGConfigServiceEventArgs> ErrorCreateInterfaceEvent;
        bool ActivityState { get; }

        void CreateInterfaces(bool updateKeys = false);
        void StopWG();
        void ApplyModifiedConfigs();
        void SetPeersThroughPipe(WGInterfaceName interfaceName, IEnumerable<Peer> peers);
        void DeletePeersThroughPipe(WGInterfaceName interfaceName, Peer peer);

        string GetPublicKey(WGInterfaceName interfaceName);
        string GetInterfaceName(WGInterfaceName interfaceName);
        int GetListenPort(WGInterfaceName interfaceName);

        Interface GetInterfaceSection(WGInterfaceName interfaceName);
        void SetInterfaceSection(WGInterfaceName interfaceName, Interface interfaceSection);
        
        IEnumerable<Peer> GetPeerSections(WGInterfaceName interfaceName);
        void SetPeerSections(WGInterfaceName interfaceName, IEnumerable<Peer> peers);

        IEnumerable<PeerDataFromPipe> GetPeersDataFromPipe(WGInterfaceName interfaceName);
        WGInterfaceName GetWGInterfaceNameFromString(string name);
        void RemoveInterface(WGInterfaceName interfaceName);
        void Dispose();
    }
}
