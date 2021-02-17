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
            return 123;
        }

        public string CreatePublicKey()
        {
            //Todo: Implement the CreatePublicKey method
            return "key";
        }
    }
}
