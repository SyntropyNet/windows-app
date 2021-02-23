using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyntropyNet.WindowsApp.Application.Domain.Models.WireGuard
{
    public class TunnelSettings
    {
        public string FileLocation { get; }
        public string IntefaceName { get; }
        public TunnelSettings(string fileLocation, string interfaceName)
        {
            IntefaceName = interfaceName;
            FileLocation = fileLocation.TrimEnd('\\') + $"\\{IntefaceName}.conf";
        }
    }
}
