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
        string DeviceName { get; set; }
        string AgentToken { get; set; }
    }
}
