using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyntropyNet.WindowsApp.Application.Domain.Models.Messages
{
    public class WGConfRequest : BaseMessage
    {
        public WGConfRequest()
        {
            Type = "WG_CONF";
            Data = new List<WGConfRequestData>();
        }

        public IEnumerable<WGConfRequestData> Data { get; set; }
    }

    public class WGConfRequestData
    {
        public string Fn { get; set; }
        public WGConfRequestArgs Args { get; set; }
        public WGConfRequestMetadata Metadata  { get; set; }
    }

    public class WGConfRequestArgs
    {
        public WGConfRequestArgs()
        {
            AllowedIps = new List<string>();
        }

        public string Ifname { get; set; }
        public string InternalIp { get; set; }
        public int? ListenPort { get; set; }
        public string PublicKey { get; set; }
        public IEnumerable<string> AllowedIps { get; set; }
        public string GwIpv4 { get; set; }
        public string EndpointIpv4 { get; set; }
        public int? EndpointPort { get; set; }
    }

    public class WGConfRequestMetadata
    {
        public WGConfRequestMetadata()
        {
            AllowedIpsInfo = new List<AllowedIpsInfo>();
        }

        public string NetworkId { get; set; }
        public string DeviceId { get; set; }
        public string DeviceName { get; set; }
        public string DevicePublicIpv4 { get; set; }
        public int ConnectionId { get; set; }
        public int ConnectionGroupId { get; set; }

        public IEnumerable<AllowedIpsInfo> AllowedIpsInfo { get; set; }
    }
}