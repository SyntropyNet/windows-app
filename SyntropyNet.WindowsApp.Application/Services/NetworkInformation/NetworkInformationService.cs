using CodeCowboy.NetworkRoute;
using SyntropyNet.WindowsApp.Application.Contracts;
using SyntropyNet.WindowsApp.Application.Domain.Models.Messages;
using SyntropyNet.WindowsApp.Application.Exceptions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SyntropyNet.WindowsApp.Application.Services.NetworkInformation
{
    public class NetworkInformationService : INetworkInformationService
    {
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
                //Todo: An error "Channel closing in progress" occurs with code 232, how to handle it correctly
                Debug.WriteLine(ex.Message);
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

        public void AddRoute(string interfaceName, string ip, string mask, string gateway, int metric)
        {
            /*var process = new Process();

            var startinfo = new ProcessStartInfo("cmd", "/c " + $"route add {ip} mask {mask} {gateway} metric {metric}");
            startinfo.RedirectStandardOutput = true;
            startinfo.UseShellExecute = false;
            startinfo.CreateNoWindow = true;
            startinfo.RedirectStandardError = true;

            process.StartInfo = startinfo;

            process.Start();
            process.WaitForExit();*/

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
                NetworkAdaptor na = NicInterface.GetNetworkAdaptor(interfaceIndex);
                Ip4RouteEntry ip4RouteEntry = new Ip4RouteEntry()
                {
                    GatewayIP = System.Net.IPAddress.Parse(gateway),
                    SubnetMask = System.Net.IPAddress.Parse(mask),
                    DestinationIP = System.Net.IPAddress.Parse(ip),
                    InterfaceIndex = interfaceIndex,
                    Metric = metric,
                };

                Ip4RouteTable.CreateRoute(ip4RouteEntry);
                return;
            }

            throw new Exception($"Error adding route {ip}");
        }
    }
}
