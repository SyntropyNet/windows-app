using SyntropyNet.WindowsApp.Application.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyntropyNet.WindowsApp.Application.Domain.Models
{
    public class WSConnectionResponse
    {
        public WSConnectionState State { get; set; }
        public string Error { get; set; }
    }
}
