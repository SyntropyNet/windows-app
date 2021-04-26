using SyntropyNet.WindowsApp.Application.Domain.Enums.WireGuard;
using SyntropyNet.WindowsApp.Application.Domain.Models.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyntropyNet.WindowsApp.Application.Comparers
{
    public class VpnConfigComparer : IComparer<VpnConfig>
    {
        public int Compare(VpnConfig a, VpnConfig b)
        {
            if (a != null && b != null )
            {
                if (a.Args.Ifname == b.Args.Ifname)
                {
                    // Mandatory as some sort algorithms require Compare(a, b) and Compare(b, a) to be consitent
                    return 0;
                }
                var ifa = (WGInterfaceName)Enum.Parse(typeof(WGInterfaceName), a.Args.Ifname, true);
                var ifb = (WGInterfaceName)Enum.Parse(typeof(WGInterfaceName), b.Args.Ifname, true);
                return Comparer<WGInterfaceName>.Default.Compare(ifa, ifb);
            }

            if (a == null || b == null)
            {
                if (ReferenceEquals(a, b))
                {
                    return 0;
                }
                return a == null ? -1 : 1;
            }
            return Comparer<string>.Default.Compare(a.Args.Ifname, b.Args.Ifname);
        }
    }

    public class WGConfRequestDataComparer : IComparer<WGConfRequestData>
    {
        public int Compare(WGConfRequestData a, WGConfRequestData b)
        {
            if (a != null && b != null 
                && a.Args != null && b.Args != null
                && !string.IsNullOrEmpty(a.Args.Ifname) && !string.IsNullOrEmpty(b.Args.Ifname))
            {
                if (a.Args.Ifname == b.Args.Ifname)
                {
                    // Mandatory as some sort algorithms require Compare(a, b) and Compare(b, a) to be consitent
                    return 0;
                }
                var ifa = (WGInterfaceName)Enum.Parse(typeof(WGInterfaceName), a.Args.Ifname, true);
                var ifb = (WGInterfaceName)Enum.Parse(typeof(WGInterfaceName), b.Args.Ifname, true);
                return Comparer<WGInterfaceName>.Default.Compare(ifa, ifb);
            }

            if (a == null || b == null)
            {
                if (ReferenceEquals(a, b))
                {
                    return 0;
                }
                return a == null ? -1 : 1;
            }
            return Comparer<WGConfRequestData>.Default.Compare(a, b);
        }
    }
}
