using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyntropyNet.WindowsApp.Application.Constants.WireGuard
{
    public static class TunnelConfigConstants
    {
        public const string PUBLIC_KEY = "PublicKey = ";
        public const string PRIVATE_KEY = "PrivateKey = ";
        public const string LISTEN_PORT = "ListenPort = ";
        public const string ADDRESS = "Address = ";
        public const string ALLOWED_IPs = "AllowedIPs = ";
        public const string ENDPOINT = "Endpoint = ";
    }
}
