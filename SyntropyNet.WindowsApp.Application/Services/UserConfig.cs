using SyntropyNet.WindowsApp.Application.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyntropyNet.WindowsApp.Application.Services
{
    public class UserConfig: IUserConfig
    {
        public bool IsAuthenticated { get; set; }
        public string TokenName { get; set; }
        public string AgentToken { get; set; }

        public void Authenticate(string deviceName, string agentToken)
        {
            IsAuthenticated = true;
            TokenName = deviceName;
            AgentToken = agentToken;
        }

        public void Quit()
        {
            IsAuthenticated = false;
            TokenName = string.Empty;
            AgentToken = string.Empty;
        }
    }
}
