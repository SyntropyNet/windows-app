using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyntropyNet.WindowsApp.Application.Domain.Models.WireGuard
{
    public class PeerDataFromPipe
    {
        public int KeepAliveInterval { get; set; }
        public string LastHandshake { get; set; }
        public string Endpoint { get; set; }
        public long RxBytes { get; set; }
        public long TxBytes { get; set; }
    }
}
