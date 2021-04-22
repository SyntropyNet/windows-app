using CodeCowboy.NetworkRoute;
using log4net;
using SyntropyNet.WindowsApp.Application.Contracts;
using SyntropyNet.WindowsApp.Application.Domain.Models.Messages;
using SyntropyNet.WindowsApp.Application.Exceptions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SyntropyNet.WindowsApp.Application.Services.NetworkInformation
{
    public class NetworkInformationService : INetworkInformationService
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(NetworkInformationService));

        private const int START_PORT = 1024;
        private const int MAX_PORT = 65535;
        public IEnumerable<IfaceBWDataRequestData> GetInformNetworkInterface()
        {
            var ifaceBWDataRequestData = new List<IfaceBWDataRequestData>();

            if (!NetworkInterface.GetIsNetworkAvailable())
                return ifaceBWDataRequestData;

            try
            {
                NetworkInterface[] interfaces
                    = NetworkInterface.GetAllNetworkInterfaces();

                foreach (NetworkInterface ni in interfaces)
                {
                    ifaceBWDataRequestData.Add(IfaceBWDataRequestData(ni));
                }
            }
            catch (NetworkInformationException ex)
            {
                log.Error(ex.Message);
            }

            return ifaceBWDataRequestData;
        }

        private IfaceBWDataRequestData IfaceBWDataRequestData(NetworkInterface ni, int interval = 10000)
        {
            long txBytes = ni.GetIPStatistics().BytesSent;
            long rxBytes = ni.GetIPStatistics().BytesReceived;

            long txDropped = ni.GetIPStatistics().OutgoingPacketsDiscarded;
            long txErrors = ni.GetIPStatistics().OutgoingPacketsWithErrors;
            long txPackets = ni.GetIPStatistics().UnicastPacketsSent;

            long rxDropped = ni.GetIPStatistics().IncomingPacketsDiscarded;
            long rxErrors = ni.GetIPStatistics().IncomingPacketsWithErrors;
            long rxPackets = ni.GetIPStatistics().UnicastPacketsReceived;

            Thread.Sleep(interval);

            long txBytesAfter = ni.GetIPStatistics().BytesSent;
            long rxBytesAfter = ni.GetIPStatistics().BytesReceived;

            long txDroppedAfter = ni.GetIPStatistics().OutgoingPacketsDiscarded;
            long txErrorsAfter = ni.GetIPStatistics().OutgoingPacketsWithErrors;
            long txPacketsAfter = ni.GetIPStatistics().UnicastPacketsSent;

            long rxDroppedAfter = ni.GetIPStatistics().IncomingPacketsDiscarded;
            long rxErrorsAfter = ni.GetIPStatistics().IncomingPacketsWithErrors;
            long rxPacketsAfter = ni.GetIPStatistics().UnicastPacketsReceived;

            return new IfaceBWDataRequestData
            {
                Iface = ni.Name,
                TxSpeedMbsps = Math.Round((txBytesAfter - txBytes) / 10000000.0, 4),
                RxSpeedMbsps = Math.Round((rxBytesAfter - rxBytes) / 10000000.0, 4),
                TxDropped = txDroppedAfter - txDropped,
                TxErrors = txErrorsAfter - txErrors,
                TxPackets = txPacketsAfter - txPackets,
                RxDropped = rxDroppedAfter - rxDropped,
                RxErrors = rxErrorsAfter - rxErrors,
                RxPackets = rxPacketsAfter - rxPackets,
                Interval = interval
            };
        }

        public int GetNextFreePort(IEnumerable<int> exceptPort = null)
        {
            var range = Enumerable.Range(START_PORT, MAX_PORT);
            var portsInUse =
                    from p in range
                    join used in System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().GetActiveUdpListeners()
                on p equals used.Port
                    select p;

            if (exceptPort != null)
                portsInUse = portsInUse.Union(exceptPort);

            var FirstFreeUDPPortInRange = range.Except(portsInUse).FirstOrDefault();

            if (FirstFreeUDPPortInRange > 0)
            {
                return FirstFreeUDPPortInRange;
            }
            else
            {
                // No Free Ports
                throw new NoFreePortException($"No Free Port in range {START_PORT} - {MAX_PORT}");
            }
        }

        public bool CheckPing(string ip, int timeout = 1000)
        {
            try { 
                Ping pingSender = new Ping();
                PingReply reply = pingSender.Send(ip, timeout);

                var status = reply.Status;
                if (status == IPStatus.Success)
                    return true;
                }
            catch
            {
                return false;
            }

            return false;
        }

        public void AddRoute(string interfaceName, string ip, string mask, string gateway, uint metric)
        {
            int interfaceIndex = 0;
            var adaptors = NicInterface.GetAllNetworkAdaptor();
            
            foreach (var adaptor in adaptors)
            {
                if(adaptor.Name == interfaceName)
                {
                    interfaceIndex = adaptor.InterfaceIndex;
                }
            }
            if(interfaceIndex != 0)
            {
                Ip4RouteEntry ip4RouteEntry = new Ip4RouteEntry()
                {
                    GatewayIP = System.Net.IPAddress.Parse(gateway),
                    SubnetMask = System.Net.IPAddress.Parse(mask),
                    DestinationIP = System.Net.IPAddress.Parse(ip),
                    InterfaceIndex = interfaceIndex,
                    Metric = metric,
                };

                CreateRoute(ip4RouteEntry);
                return;
            }

            throw new Exception($"Error adding route {ip}");
        }

        public void DeleteRoute(string interfaceName, string ip, string mask, string gateway, int metric)
        {
            int interfaceIndex = 0;
            var adaptors = NicInterface.GetAllNetworkAdaptor();

            foreach (var adaptor in adaptors)
            {
                if (adaptor.Name == interfaceName)
                {
                    interfaceIndex = adaptor.InterfaceIndex;
                }
            }
            if (interfaceIndex != 0)
            {
                CodeCowboy.NetworkRoute.Ip4RouteEntry ip4RouteEntry = new CodeCowboy.NetworkRoute.Ip4RouteEntry()
                {
                    GatewayIP = System.Net.IPAddress.Parse(gateway),
                    SubnetMask = System.Net.IPAddress.Parse(mask),
                    DestinationIP = System.Net.IPAddress.Parse(ip),
                    InterfaceIndex = interfaceIndex,
                    Metric = metric
                };

                Ip4RouteTable.DeleteRoute(ip4RouteEntry);
                return;
            }

            throw new Exception($"Error deleting route {ip}");
        }

        public bool IsLocalIpAddress(string host)
        {
            try
            {
                IPAddress[] hostIPs = Dns.GetHostAddresses(host);
                IPAddress[] localIPs = Dns.GetHostAddresses(Dns.GetHostName());

                foreach (IPAddress hostIP in hostIPs)
                {
                    // is localhost
                    if (IPAddress.IsLoopback(hostIP)) return true;
                    // is local address
                    foreach (IPAddress localIP in localIPs)
                    {
                        if (hostIP.Equals(localIP)) return true;
                    }
                }
            }
            catch(Exception ex) 
            {
                log.Error(ex.Message);
            }
            return false;
        }

        private void CreateRoute(Ip4RouteEntry routeEntry)
        {
            var route = new NativeMethods.MIB_IPFORWARDROW
            {
                dwForwardDest = BitConverter.ToUInt32(IPAddress.Parse(routeEntry.DestinationIP.ToString()).GetAddressBytes(), 0),
                dwForwardMask = BitConverter.ToUInt32(IPAddress.Parse(routeEntry.SubnetMask.ToString()).GetAddressBytes(), 0),
                dwForwardNextHop = BitConverter.ToUInt32(IPAddress.Parse(routeEntry.GatewayIP.ToString()).GetAddressBytes(), 0),
                dwForwardMetric1 = routeEntry.Metric,
                dwForwardType = Convert.ToUInt32(3), //Default to 3
                dwForwardProto = Convert.ToUInt32(3), //Default to 3
                dwForwardAge = 0,
                dwForwardIfIndex = Convert.ToUInt32(routeEntry.InterfaceIndex)
            };

            IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(NativeMethods.MIB_IPFORWARDROW)));
            try
            {
                Marshal.StructureToPtr(route, ptr, false);
                var status = NativeMethods.CreateIpForwardEntry(ptr);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }

        }
    }
}
