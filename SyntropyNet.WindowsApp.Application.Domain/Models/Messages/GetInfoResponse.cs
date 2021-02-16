using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyntropyNet.WindowsApp.Application.Domain.Models.Messages
{
    public class GetInfoResponse : BaseMessage
    {
        public GetInfoResponse()
        {
            Type = "GET_INFO";
        }

        public GetInfoResponseData Data { get; set; }
    }

    public class GetInfoResponseData
    {
        public int? AgentProvider { get; set; }
        public bool ServiceStatus { get; set; }
        public IEnumerable<string> AgentTags { get; set; }
        public string ExternalIp { get; set; }
        public IEnumerable<BaseNetworkInfo> NetworkInfo { get; set; }
        public IEnumerable<ContainerInfo> ContainerInfo { get; set; }
    }

    public abstract class BaseNetworkInfo { }

    public class AgentNetworkInfo : BaseNetworkInfo
    {
        public string AgentNetworkId { get; set; }
        public string AgentNetworkName { get; set; }
        public IEnumerable<string> AgentNetworkSubnets { get; set; }
    }

    public class DockerNetworkInfo : BaseNetworkInfo
    {
        public string DockerNetworkId { get; set; }
        public string DockerNetworkName { get; set; }
        public IEnumerable<string> DockerNetworkSubnets { get; set; }
    }

    public class ContainerInfo
    {
        public string AgentContainerId { get; set; }
        public string AgentContainerName { get; set; }
        public IEnumerable<string> AgentContainerNetworks { get; set; }
        public IEnumerable<string> AgentContainerIps { get; set; }
        public IEnumerable<string> AgentContainerSubnets { get; set; }
        public AgentContainerPorts AgentContainerPorts { get; set; }
        public string AgentContainerState { get; set; }
        public string AgentContainerUptime { get; set; }
    }

    public class AgentContainerPorts
    {
        public IEnumerable<int> Udp { get; set; }
        public IEnumerable<int> Tcp { get; set; }
    }
}
