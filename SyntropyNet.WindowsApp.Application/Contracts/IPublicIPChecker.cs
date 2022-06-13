
using static SyntropyNet.WindowsApp.Application.Services.NetworkInformation.PublicIPChecker;

namespace SyntropyNet.WindowsApp.Application.Contracts
{
    public interface IPublicIPChecker
    {
        void StartIPCheker();
        void StopIPCheker();
        event IpChanged IpChangedEvent;
    }
}
