using System;

namespace SyntropyNet.WindowsApp.Application.Helpers {
    public static class IpHelper {
        public static string StripPortNumber(string ipWithPort) {
            if (String.IsNullOrEmpty(ipWithPort)) {
                return null;
            }

            return ipWithPort.Split('/')[0];
        }
    }
}
