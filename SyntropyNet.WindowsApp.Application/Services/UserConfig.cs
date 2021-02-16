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
        // ToDo:: temporary hardcode values until we have an AddToken window.
        public bool IsAuthenticated { get; set; } = true;
        public string DeviceName { get; set; } = "test";
        public string AgentToken { get; set; } = "jFJ4OvvgJmpEhkrggbeXq5VKkgmau8nN";
    }
}
