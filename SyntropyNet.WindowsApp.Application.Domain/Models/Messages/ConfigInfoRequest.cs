using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyntropyNet.WindowsApp.Application.Domain.Models.Messages
{
    public class ConfigInfoRequest : BaseMessage
    {
        public ConfigInfoRequest()
        {
            Type = "CONFIG_INFO";
        }

        public ConfigInfoRequestData Data { get; set; }
    }

    public class ConfigInfoRequestData
    {
        public ConfigInfoRequestData()
        {
            Vpn = new List<VpnConfig>();
        }

        public int AgentId { get; set; }
        public Network Network { get; set; }
        public IEnumerable<VpnConfig> Vpn { get; set; }
    }

    public class Network
    {
        public NetworkRules Public { get; set; }
        public NetworkRules Sdn1 { get; set; }
        public NetworkRules Sdn2 { get; set; }
        public NetworkRules Sdn3 { get; set; }
    }

    public class NetworkRules
    {
        public string InternalIp { get; set; }
        public string PublicKey { get; set; }
        public int? ListenPort { get; set; }
    }

    public class VpnConfig
    {
        public string Fn { get; set; }
        public VpnArgs Args { get; set; }
        public VpnMetadata Metadata { get; set; }
    }

    public class VpnArgs
    {
        public string Ifname { get; set; }
        public string InternalIp { get; set; }
        public IEnumerable<string> AllowedIps { get; set; }
        public string EndpointIpv4 { get; set; }
        public int EndpointPort { get; set; }
        public string PublicKey { get; set; }
        public string GwIpv4 { get; set; }
    }

    public class VpnMetadata
    {
        public int NetworkId { get; set; }
        public string DeviceId { get; set; }
        public string DeviceName { get; set; }
        public string DevicePublicIpv4 { get; set; }
        public int ConnectionId { get; set; }
        public IEnumerable<AllowedIpsInfo> AllowedIpsInfo { get; set; }
    }

    public class AllowedIpsInfo
    {
        public string AgentServiceName { get; set; }
        public IEnumerable<int> AgentServiceTcpPorts { get; set; }
        public IEnumerable<int> AgentServiceUdpPorts { get; set; }
        public string AgentServiceSubnetIp { get; set; }
    }
}
