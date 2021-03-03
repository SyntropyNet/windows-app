﻿using System;
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
        }
        public int? AgentProvider { get; set; }
        public bool ServiceStatus { get; set; }
        public IEnumerable<string> AgentTags { get; set; }
        public string ExternalIp { get; set; }
        public IEnumerable<ContainerInfo> ContainerInfo { get; set; }
    }

    public class ContainerInfo
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
    }
}
