using SyntropyNet.WindowsApp.Application.Contracts;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyntropyNet.WindowsApp.Services
{
    public class AppSettings: IAppSettings
    {
        public string ControllerUrl => ConfigurationManager.AppSettings["ControllerUrl"];
        public string AgentVersion => ConfigurationManager.AppSettings["AgentVersion"];

        public string DeviceId 
        {
            get
            {
                // ToDo:: check how we should generate DeviceId
                return "Sibers";
            }
        }
    }
}
