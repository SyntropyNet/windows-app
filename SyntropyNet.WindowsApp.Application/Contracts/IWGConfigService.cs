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
        string InterfaceName { get; }
        string PublicKey { get; }
        bool ActivityState { get; }
        void RunWG();
        void StopWG();
        Interface GetInterface();
        IEnumerable<Peer> GetPeers();
        void SetInterface(Interface @interface);
        void SetPeers(IEnumerable<Peer> peers);
        void ApplyChange();
        void CreateConfig();
        void RemoveConfig();
        string PathToConfigFile();
    }
}
