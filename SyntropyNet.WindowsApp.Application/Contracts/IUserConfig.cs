using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyntropyNet.WindowsApp.Application.Contracts
{
    public interface IUserConfig
    {
        bool IsAuthenticated { get; set; }
        string TokenName { get; set; }
        string AgentToken { get; set; }

        void Authenticate(string deviceName, string agentToken);
        void Quit();
    }
}
