using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyntropyNet.WindowsApp.Application.Contracts
{
    public interface IAppSettings
    {
        string ControllerUrl { get; }
        string AgentVersion { get; }
        string DeviceId { get; }

        bool ModalWindowActivated { get; set; }
    }
}
