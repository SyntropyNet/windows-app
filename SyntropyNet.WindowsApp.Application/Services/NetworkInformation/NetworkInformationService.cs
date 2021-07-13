using CodeCowboy.NetworkRoute;
using log4net;
using SyntropyNet.WindowsApp.Application.Constants;
using SyntropyNet.WindowsApp.Application.Contracts;
using SyntropyNet.WindowsApp.Application.Domain.Events;
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

using CCIp4RouteEntry = CodeCowboy.NetworkRoute.Ip4RouteEntry;

namespace SyntropyNet.WindowsApp.Application.Services.NetworkInformation
{
    public class NetworkInformationService : INetworkInformationService
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(NetworkInformationService));

        private const int START_PORT = 1024;
        private const int MAX_PORT = 65535;

        public NetworkInformationService() {
            SdnRouter pinger = SdnRouter.Instance;
            pinger.FastestIpFound += _OnFastestIpFound;
        }

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
                    try { 
                        ifaceBWDataRequestData.Add(IfaceBWDataRequestData(ni));
                    }
                    catch (NetworkInformationException ex)
                    {
                        // There can be some interfaces which already does not exists due to VPN settings,
                        // it is an expected behavior, just log a debug message.
                        log.Debug(ex.Message, ex);
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error(ex.Message, ex);
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

        private void _OnFastestIpFound(object o, FastestRouteFoundEventArgs args) {
            if (args == null) {
                return;
            }

            string interfaceName = args.InterfaceName.ToString();

            if (RouteExists(args.Ip.ToString(), out CCIp4RouteEntry routeEntry)) {
                int interfaceIndex = _GetInterfaceIndexHelper(interfaceName);

                if (interfaceIndex == 0) {
                    throw new NotFoundInterfaceException(interfaceName);
                }

                IPAddress gateway = IPAddress.Parse(args.Gateway);

                if (routeEntry.InterfaceIndex == interfaceIndex && routeEntry.GatewayIP.Equals(gateway)) {
                    // ROUTE IS THE SAME => RETURN
                    return;
                }

                Ip4RouteEntry updatedEntry = new Ip4RouteEntry {
                    InterfaceIndex = interfaceIndex,
                    DestinationIP = routeEntry.DestinationIP,
                    GatewayIP = gateway,
                    SubnetMask = args.Mask,
                    Metric = (uint)args.Metric
                };

                UpdateRoute(updatedEntry);
            } else {
                AddRoute(interfaceName, args.Ip.ToString(), args.Mask.ToString(), args.Gateway, (uint)args.Metric);
            }
        }

        private int _GetInterfaceIndexHelper(string IFaceName) {
            int interfaceIndex = 0;
            var adaptors = NicInterface.GetAllNetworkAdaptor();

            foreach (var adaptor in adaptors) {
                if (adaptor.Name == IFaceName) {
                    interfaceIndex = adaptor.InterfaceIndex;
                }
            }

            return interfaceIndex;
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
                log.Error(ex.Message, ex);
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

        public void UpdateRoute(Ip4RouteEntry routeEntry) {
            int size = 0;
            IntPtr table = IntPtr.Zero;
            IntPtr rowPointer = IntPtr.Zero;

            try {
                // GET ROUTE TABLE
                int tableResult = NativeMethods.GetIpForwardTable(table, ref size, true);
                table = Marshal.AllocHGlobal(size);
                NativeMethods.IPForwardTable tableTyped = (NativeMethods.IPForwardTable)Marshal.PtrToStructure(table, typeof(NativeMethods.IPForwardTable));

                uint destination = BitConverter.ToUInt32(IPAddress.Parse(routeEntry.DestinationIP.ToString()).GetAddressBytes(), 0);
                bool routeFound = false;

                // FIND ROUTE
                for (int i = 0; i < tableTyped.Table.Length; i++) {
                    if (tableTyped.Table[i].dwForwardDest != destination) {
                        continue;
                    }

                    if (!routeFound) {
                        NativeMethods.MIB_IPFORWARDROW targetRow = tableTyped.Table[i];

                        uint newGateway = BitConverter.ToUInt32(IPAddress.Parse(routeEntry.GatewayIP.ToString()).GetAddressBytes(), 0);
                        uint newMask = BitConverter.ToUInt32(IPAddress.Parse(routeEntry.SubnetMask.ToString()).GetAddressBytes(), 0);
                        uint newIFaceIndex = Convert.ToUInt32(routeEntry.InterfaceIndex);

                        targetRow.dwForwardIfIndex = newIFaceIndex;
                        targetRow.dwForwardNextHop = newGateway;
                        targetRow.dwForwardMask = newMask;

                        // CREATE MODIFIED COPY
                        rowPointer = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(NativeMethods.MIB_IPFORWARDROW)));
                        Marshal.StructureToPtr(targetRow, rowPointer, false);

                        routeFound = true;
                    }

                    // DELETE OLD ROUTE
                    NativeMethods.DeleteIpForwardEntry(ref tableTyped.Table[i]);
                }

                // CREATE NEW MODIFIED ROUTE
                var status = NativeMethods.CreateIpForwardEntry(rowPointer);
            } catch {
            } finally {
                Marshal.FreeHGlobal(table);
                Marshal.FreeHGlobal(rowPointer);
            }
        }

        public bool RouteExists(string destinationIP, string gateway)
        {
            List<CodeCowboy.NetworkRoute.Ip4RouteEntry> routeTable = Ip4RouteTable.GetRouteTable();
            CodeCowboy.NetworkRoute.Ip4RouteEntry routeEntry = routeTable.Find(i => i.DestinationIP.ToString().Equals(destinationIP) && i.GatewayIP.ToString().Equals(gateway));
            if(destinationIP == "0.0.0.0")
            {
                log.Info($"RouteExistsDetails: {destinationIP}, {gateway}. Exists: {routeEntry != null}");
                log.Info($"RouteExistsDetails: {string.Join(";", routeTable.Select(x => $"{x.DestinationIP.ToString()}, {x.GatewayIP.ToString()}"))}");
                if(routeEntry == null)
                {
                    var extraRoute = routeTable.Find(i => i.DestinationIP.ToString().Equals(destinationIP,StringComparison.InvariantCultureIgnoreCase) && i.GatewayIP.ToString().Equals(gateway, StringComparison.InvariantCultureIgnoreCase));
                    if(extraRoute != null)
                    {
                        log.Info($"RouteExistsDetails: Extra route found");
                    }
                }
            }
            return (routeEntry != null);
        }

        public bool RouteExists(string destinationIP, string gateway, out CodeCowboy.NetworkRoute.Ip4RouteEntry routeEntry) {
            List<CodeCowboy.NetworkRoute.Ip4RouteEntry> routeTable = Ip4RouteTable.GetRouteTable();
            routeEntry = routeTable.Find(i => i.DestinationIP.ToString().Equals(destinationIP) && i.GatewayIP.ToString().Equals(gateway));
            
            return routeEntry != null;
        }

        public bool RouteExists(string destinationIP, out CodeCowboy.NetworkRoute.Ip4RouteEntry routeEntry) {
            List<CodeCowboy.NetworkRoute.Ip4RouteEntry> routeTable = Ip4RouteTable.GetRouteTable();
            routeEntry = routeTable.Find(i => i.DestinationIP.ToString().Equals(destinationIP));

            return routeEntry != null;
        }
    }
}
