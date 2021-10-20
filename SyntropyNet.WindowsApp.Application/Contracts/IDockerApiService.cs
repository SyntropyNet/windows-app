using SyntropyNet.WindowsApp.Application.Domain.Models.Messages;
using System.Collections.Generic;

namespace SyntropyNet.WindowsApp.Application.Contracts
{
    public interface IDockerApiService
    {
        IEnumerable<ContainerInfo> GetContainers();
    }
}
