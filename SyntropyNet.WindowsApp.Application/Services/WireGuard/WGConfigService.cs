using SyntropyNet.WindowsApp.Application.Contracts;
using SyntropyNet.WindowsApp.Application.Domain.Models.Messages;
using System;

namespace SyntropyNet.WindowsApp.Application.Services.WireGuard
{
    public class WGConfigService : IWGConfigService
    {
        public bool CheckListenPort(int port)
        {
            //Todo: Implement the CheckListenPort method
            return true;
        }

        public bool CheckPrivateKey(string key)
        {
            //Todo: Implement the CheckPrivateKey method
            return true;
        }

        public int CreateListenPort()
        {
            //Todo: Implement the CreateListenPort method
            return 63834;
        }

        public string CreatePublicKey()
        {
            //Todo: Implement the CreatePublicKey method
            return "V1Iezqb8ohwC+lkqvahQH1tDpreRWKTh162ggwJmN34=";
        }

        public string GetIfName()
        {
            //Todo: Implement the GetIfName method
            return "Tunnel";
        }
    }
}
