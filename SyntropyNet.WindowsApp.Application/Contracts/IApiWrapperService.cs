using SyntropyNet.WindowsApp.Application.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SyntropyNet.WindowsApp.Application.Services.ApiWrapper.ApiWrapperService;

namespace SyntropyNet.WindowsApp.Application.Contracts
{
    public interface IApiWrapperService
    {
        event ServicesUpdated ServicesUpdatedEvent;
        void Run(Action<WSConnectionResponse> callback);
        void Stop();
    }
}

