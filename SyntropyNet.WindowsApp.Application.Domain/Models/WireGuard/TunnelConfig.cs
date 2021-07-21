using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyntropyNet.WindowsApp.Application.Domain.Models.WireGuard
{
    public class TunnelConfig
    {
        public string Name { get; set; }
        public string PublicKey { get; set; }

        public Interface Interface { get; set; }
        public IEnumerable<Peer> Peers { get; set; }
    }

    public class Interface
    {
        public string PrivateKey { get; set; }
        public int ListenPort { get; set; }
        public IEnumerable<string> Address { get; set; }
    }

    public class Peer
    {
        public string PublicKey { get; set; }
        public IEnumerable<string> AllowedIPs { get; set; }
        public string Endpoint { get; set; }
        public int ConnectionId { get; set; }
        public bool Equals(Peer peer)
        {
            if (peer.PublicKey != PublicKey)
                return false;
            if (peer.Endpoint != Endpoint)
                return false;

            var list1 = AllowedIPs ?? new List<string>();
            var list2 = peer.AllowedIPs ?? new List<string>();
            if (!list1.SequenceEqual(list2))
                return false;

            return true;
        }
    }
}
