using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyntropyNet.WindowsApp.Application.Models
{
    public class ServiceModel
    {
        public string PeerUid { get; set; }
        public string Name { get; set; }
        public string Ip { get; set; }
        public string Port { get; set; }

        public string Address => $"{Ip}:{Port}";
    }
}
