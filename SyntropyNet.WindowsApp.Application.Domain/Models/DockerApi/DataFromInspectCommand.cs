using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyntropyNet.WindowsApp.Application.Domain.Models.DockerApi
{
    public class DataFromInspectCommand
    {
        public IEnumerable<string> Networks { get; set; }
        public IEnumerable<string> Ips { get; set; }
        public IEnumerable<string> Ports { get; set; }
    }
}
