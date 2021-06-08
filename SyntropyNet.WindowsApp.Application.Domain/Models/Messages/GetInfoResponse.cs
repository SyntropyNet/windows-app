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
        public GetInfoResponseData()
        {
            AgentTags = new List<string>();
            ContainerInfo = new List<ContainerInfo>();
            NetworkInfo = new List<string>();
        }
        public int? AgentProvider { get; set; }
        public bool ServiceStatus { get; set; }
        public IEnumerable<string> AgentTags { get; set; }
        public IEnumerable<string> NetworkInfo { get; set; }
        public string ExternalIp { get; set; }
        public IEnumerable<ContainerInfo> ContainerInfo { get; set; }
    }

    public class ContainerInfo : IEquatable<ContainerInfo>
    {
        public ContainerInfo()
        {
            AgentContainerNetworks = new List<string>();
            AgentContainerIps = new List<string>();
            AgentContainerSubnets = new List<string>();
            AgentContainerPorts = new AgentContainerPorts();
        }

        public string AgentContainerId { get; set; }
        public string AgentContainerName { get; set; }
        public IEnumerable<string> AgentContainerNetworks { get; set; }
        public IEnumerable<string> AgentContainerIps { get; set; }
        public IEnumerable<string> AgentContainerSubnets { get; set; }
        public AgentContainerPorts AgentContainerPorts { get; set; }
        public string AgentContainerState { get; set; }
        public string AgentContainerUptime { get; set; }

        public bool Equals(ContainerInfo containerInfo)
        {
            if (containerInfo.AgentContainerId != AgentContainerId)
                return false;
            if (containerInfo.AgentContainerName != AgentContainerName)
                return false;
            if (!containerInfo.AgentContainerNetworks.SequenceEqual(AgentContainerNetworks))
                return false;
            if (!containerInfo.AgentContainerIps.SequenceEqual(AgentContainerIps))
                return false;
            if (!containerInfo.AgentContainerSubnets.SequenceEqual(AgentContainerSubnets))
                return false;
            if (!containerInfo.AgentContainerPorts.Equals(AgentContainerPorts))
                return false;
            return true;
        }
    }

    public class AgentContainerPorts
    {
        public AgentContainerPorts()
        {
            Udp = new List<int>();
            Tcp = new List<int>();
        }

        public IEnumerable<int> Udp { get; set; }
        public IEnumerable<int> Tcp { get; set; }

        public bool Equals(AgentContainerPorts agentContainerPorts)
        {
            if (!agentContainerPorts.Udp.SequenceEqual(Udp))
                return false;
            if (!agentContainerPorts.Udp.SequenceEqual(Udp))
                return false;

            return true;
        }
    }
}
