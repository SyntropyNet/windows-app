using System.Collections.Generic;
using System.Linq;

namespace SyntropyNet.WindowsApp.Application.Domain.Helpers {
    public static class IpHelpers {
        /// <summary>
        /// Returns Ip addresses which appear more than once
        /// </summary>
        /// <param name="ips"></param>
        /// <returns></returns>
        public static IEnumerable<string> GetCommonIps(IEnumerable<IEnumerable<string>> allowedIps) {
            return allowedIps.Skip(1)
                             .Aggregate(
                                new HashSet<string>(allowedIps.First()),
                                (h, e) => { h.IntersectWith(e); return h; }
                             );
        }
    }
}
