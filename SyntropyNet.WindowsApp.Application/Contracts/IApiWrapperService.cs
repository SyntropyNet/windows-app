using SyntropyNet.WindowsApp.Application.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyntropyNet.WindowsApp.Application.Contracts
{
    public interface IApiWrapperService
    {
        void Run(Action<WSConnectionResponse> callback);
        void Stop();
    }
}

