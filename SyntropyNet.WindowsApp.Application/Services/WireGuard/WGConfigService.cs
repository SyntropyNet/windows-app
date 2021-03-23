using log4net;
using SyntropyNet.WindowsApp.Application.Constants.WireGuard;
using SyntropyNet.WindowsApp.Application.Contracts;
using SyntropyNet.WindowsApp.Application.Domain.Enums.WireGuard;
using SyntropyNet.WindowsApp.Application.Domain.Models.WireGuard;
using SyntropyNet.WindowsApp.Application.Exceptions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SyntropyNet.WindowsApp.Application.Services.WireGuard
{
    public class WGConfigService : IWGConfigService
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(WGConfigService));

        //event
        public event Action<object, WGConfigServiceEventArgs> CreateInterfaceEvent;
        public event Action<object, WGConfigServiceEventArgs> ErrorCreateInterfaceEvent;

        //Services
        private readonly INetworkInformationService _networkService;

        //Interfaces
        private TunnelConfig PublicInterface { get; set; }
        private TunnelConfig SDN1Interface { get; set; }
        private TunnelConfig SDN2Interface { get; set; }
        private TunnelConfig SDN3Interface { get; set; }

        public WGConfigService(INetworkInformationService networkService)
        {
            _networkService = networkService;
        }

        public bool ActivityState
        {
            get
            {
                foreach (WGInterfaceName interfaceName in Enum.GetValues(typeof(WGInterfaceName)))
                {
                    var tunnelName = Path.GetFileNameWithoutExtension(GetPathToInterfaceConfig(interfaceName));
                    var shortName = String.Format("WireGuardTunnel${0}", tunnelName);

                    var scm = Win32.OpenSCManager(null, null, Win32.ScmAccessRights.QueryLockStatus);
                    if (scm == IntPtr.Zero)
                    {
                        Win32.CloseServiceHandle(scm);
                        return false;
                    }

                    var service = Win32.OpenService(scm, shortName, Win32.ServiceAccessRights.QueryStatus);
                    if (service == IntPtr.Zero)
                    {
                        Win32.CloseServiceHandle(service);
                        return false;
                    }

                    var serviceStatus = new Win32.ServiceStatus();
                    Win32.QueryServiceStatus(service, serviceStatus);

                    if (serviceStatus.dwCurrentState == Win32.ServiceState.Running)
                    {
                        Win32.CloseServiceHandle(service);
                    }
                    else
                    {
                        Win32.CloseServiceHandle(service);
                        return false;
                    }
                }

                return true;
            }
        }

        public void CreateInterfaces()
        {
            CreateInterface(WGInterfaceName.SYNTROPY_PUBLIC);
            CreateInterface(WGInterfaceName.SYNTROPY_SDN1);
            CreateInterface(WGInterfaceName.SYNTROPY_SDN2);
            CreateInterface(WGInterfaceName.SYNTROPY_SDN3);

            //ApplyModifiedConfigs();
        }

        public void StopWG()
        {
            var t1 = new Task(() =>
                Remove(GetPathToInterfaceConfig(WGInterfaceName.SYNTROPY_PUBLIC), true)
            );
            var t2 = new Task(() =>
                Remove(GetPathToInterfaceConfig(WGInterfaceName.SYNTROPY_SDN1), true)
            );
            var t3 = new Task(() =>
                Remove(GetPathToInterfaceConfig(WGInterfaceName.SYNTROPY_SDN2), true)
            );
            var t4 = new Task(() =>
                Remove(GetPathToInterfaceConfig(WGInterfaceName.SYNTROPY_SDN3), true)
            );

            t1.Start();
            t2.Start();
            t3.Start();
            t4.Start();

            Task.WaitAll(t1, t2, t3, t4);
        }

        public string GetPublicKey(WGInterfaceName interfaceName)
        {
            TunnelConfig interfaceConfig = GetHowName(interfaceName);
            return interfaceConfig.PublicKey;
        }

        public string GetInterfaceName(WGInterfaceName interfaceName)
        {
            TunnelConfig interfaceConfig = GetHowName(interfaceName);
            return interfaceConfig.Name;
        }

        public int GetListenPort(WGInterfaceName interfaceName)
        {
            TunnelConfig interfaceConfig = GetHowName(interfaceName);
            return interfaceConfig.Interface.ListenPort;
        }

        public void SetInterfaceSection(WGInterfaceName interfaceName, Interface interfaceSection)
        {
            TunnelConfig interfaceConfig = GetHowName(interfaceName);
            interfaceConfig.Interface = interfaceSection;

            SetInterfaceConfig(interfaceName, interfaceConfig.Interface, interfaceConfig.Peers);
        }

        public Interface GetInterfaceSection(WGInterfaceName interfaceName)
        {
            TunnelConfig interfaceConfig = GetHowName(interfaceName);

            return interfaceConfig.Interface;
        }

        public void SetPeerSections(WGInterfaceName interfaceName, IEnumerable<Peer> peers)
        {
            TunnelConfig interfaceConfig = GetHowName(interfaceName);
            interfaceConfig.Peers = peers;

            SetInterfaceConfig(interfaceName, interfaceConfig.Interface, interfaceConfig.Peers);
        }

        public IEnumerable<Peer> GetPeerSections(WGInterfaceName interfaceName)
        {
            TunnelConfig interfaceConfig = GetHowName(interfaceName);

            if (interfaceConfig.Peers == null)
                return new List<Peer>();

            return interfaceConfig.Peers;
        }

        public void ApplyModifiedConfigs()
        {
            var t1 = new Task(() =>
                Add(GetPathToInterfaceConfig(WGInterfaceName.SYNTROPY_PUBLIC), true)
            );
            var t2 = new Task(() =>
                Add(GetPathToInterfaceConfig(WGInterfaceName.SYNTROPY_SDN1), true)
            );
            var t3 = new Task(() =>
                Add(GetPathToInterfaceConfig(WGInterfaceName.SYNTROPY_SDN2), true)
            );
            var t4 = new Task(() =>
                Add(GetPathToInterfaceConfig(WGInterfaceName.SYNTROPY_SDN3), true)
            );

            t1.Start();
            t2.Start();
            t3.Start();
            t4.Start();

            Task.WaitAll(t1, t2, t3, t4);
        }

        private string Base64ToHex(string str)
        {
            byte[] bytes = Convert.FromBase64String(str);
            return BitConverter.ToString(bytes).Replace("-", "").ToLower();
        }

        public void SetPeersThroughPipe(WGInterfaceName interfaceName)
        {
            TunnelConfig interfaceConfig = GetHowName(interfaceName);
            StringBuilder set = new StringBuilder("set=1\n");
            set.Append("replace_peers=true\n");

            if (interfaceConfig.Peers != null && interfaceConfig.Peers.Count() > 0)
            {
                foreach (var peer in interfaceConfig.Peers)
                {
                    set.Append($"public_key={Base64ToHex(peer.PublicKey)}\n");
                    set.Append("replace_allowed_ips=true\n");
                    foreach (var allowedIp in peer.AllowedIPs)
                    {
                        set.Append($"allowed_ip={allowedIp}\n");
                    }
                    set.Append($"persistent_keepalive_interval=15\n");

                    string address = peer.Endpoint.Remove(peer.Endpoint.LastIndexOf(@":"));
                    string port = peer.Endpoint.Substring(peer.Endpoint.LastIndexOf(@":") + 1);
                    string endpoint = $"{GetIpInRequiredFormatRecord(address)}:{port}";
                    set.Append($"endpoint={endpoint}\n");
                }
            }
            set.Append("\n");

            StreamReader reader = null;
            NamedPipeClientStream stream = null;

            try
            {
                stream = GetPipe(GetPathToInterfaceConfig(interfaceName));
                stream.Connect();
                reader = new StreamReader(stream);
            }
            catch { }

            try
            {
                var pipe = Encoding.UTF8.GetBytes(set.ToString());
                stream.Write(pipe, 0, pipe.Length);

                while (true)
                {
                    var line = reader.ReadLine();
                    if (line == "")
                        break;

                    if (line.StartsWith("errno="))
                    {
                        var error = line.Substring(6);
                        if (error == "0")
                            break;
                        else
                            new Exception("Update error wg interface");
                    }
                }
            }
            catch { }
            finally
            {
                if (stream != null && stream.IsConnected)
                    stream.Close();
            }
        }

        private void CreateInterface(WGInterfaceName interfaces)
        {
            var expectPort = GetUsedPorts();
            Keypair keypair = Keypair.Generate();
            int listenPort = _networkService.GetNextFreePort(expectPort);

            var tunnelConfig = new TunnelConfig
            {
                Name = interfaces.ToString(),
                PublicKey = keypair.Public,
                Interface = new Interface
                {
                    PrivateKey = keypair.Private,
                    ListenPort = listenPort
                }
            };

            if (interfaces == WGInterfaceName.SYNTROPY_PUBLIC)
                PublicInterface = tunnelConfig;
            else if (interfaces == WGInterfaceName.SYNTROPY_SDN1)
                SDN1Interface = tunnelConfig;
            else if (interfaces == WGInterfaceName.SYNTROPY_SDN2)
                SDN2Interface = tunnelConfig;
            else if (interfaces == WGInterfaceName.SYNTROPY_SDN3)
                SDN3Interface = tunnelConfig;
            else
                throw new NotFoundInterfaceException();

            SetInterfaceConfig(interfaces, tunnelConfig.Interface, null);
        }

        private List<int> GetUsedPorts()
        {
            var expectPort = new List<int>();
            
            if (PublicInterface?.Interface?.ListenPort != null
                && PublicInterface.Interface.ListenPort > 0)
                expectPort.Add(PublicInterface.Interface.ListenPort);
            if (SDN1Interface?.Interface?.ListenPort != null
                && SDN1Interface.Interface.ListenPort > 0)
                expectPort.Add(SDN1Interface.Interface.ListenPort);
            if (SDN2Interface?.Interface?.ListenPort != null
                && SDN2Interface.Interface.ListenPort > 0)
                expectPort.Add(SDN2Interface.Interface.ListenPort);
            if (SDN3Interface?.Interface?.ListenPort != null
                && SDN3Interface.Interface.ListenPort > 0)
                expectPort.Add(SDN3Interface.Interface.ListenPort);

            return expectPort;
        }

        public void RemoveInterface(WGInterfaceName interfaceName)
        {
            CreateInterface(interfaceName);
        }

        private void SetInterfaceConfig(WGInterfaceName interfaceName, Interface interfaceSection, IEnumerable<Peer> peerSection)
        {
            StringBuilder configString = new StringBuilder();

            configString.AppendLine("[Interface]");
            if (interfaceSection != null)
            {
                configString.AppendLine(
                    TunnelConfigConstants.PRIVATE_KEY + interfaceSection.PrivateKey);
                configString.AppendLine(
                    TunnelConfigConstants.LISTEN_PORT + interfaceSection.ListenPort);
                if (interfaceSection.Address != null && interfaceSection.Address.Count() > 0)
                    configString.AppendLine(
                        TunnelConfigConstants.ADDRESS + String.Join(",", interfaceSection.Address));
            }

            if (peerSection != null)
            {
                foreach (var item in peerSection)
                {
                    string address = item.Endpoint.Remove(item.Endpoint.LastIndexOf(@":"));
                    string port = item.Endpoint.Substring(item.Endpoint.LastIndexOf(@":") + 1);
                    string endpoint = $"{GetIpInRequiredFormatRecord(address)}:{port}";

                    configString.AppendLine("[Peer]")
                        .AppendLine(
                            TunnelConfigConstants.PUBLIC_KEY + item.PublicKey)
                        .AppendLine(
                            TunnelConfigConstants.ALLOWED_IPs + String.Join(",", item.AllowedIPs))
                        .AppendLine(
                            TunnelConfigConstants.ENDPOINT + endpoint)
                        .AppendLine(
                            TunnelConfigConstants.PERSISTEN_KEEPALIVE);
                }
            }

            using (StreamWriter sw = new StreamWriter(GetPathToInterfaceConfig(interfaceName), false, Encoding.Default))
            {
                sw.Write(configString);
            }
        }

        private string GetIpInRequiredFormatRecord(string input)
        {
            IPAddress address;
            if (IPAddress.TryParse(input, out address))
            {
                switch (address.AddressFamily)
                {
                    case System.Net.Sockets.AddressFamily.InterNetwork:
                        return input;
                    case System.Net.Sockets.AddressFamily.InterNetworkV6:
                        return $"[{input}]";
                }
            }

            throw new InvalidIpFormatException($"Ip {input} is invalid");
        }

        private TunnelConfig GetInterfaceConfig(WGInterfaceName interfaceName)
        {
            bool interfaceSection = false;
            bool peerSection = false;

            Interface interfaceSectionData = new Interface();
            List<Peer> peerSectionsData = new List<Peer>();
            Peer peer = null;

            TunnelConfig interfaceConfig = GetHowName(interfaceName);

            using (StreamReader sr = new StreamReader(GetPathToInterfaceConfig(interfaceName), System.Text.Encoding.Default))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    if (line == "[Interface]")
                    {
                        interfaceSection = true;
                        peerSection = false;
                    }

                    if (line == "[Peer]")
                    {
                        interfaceSection = false;
                        peerSection = true;

                        if (peer == null)
                            peer = new Peer();
                        else
                        {
                            peerSectionsData.Add(peer);
                            peer = new Peer();
                        }
                    }

                    if (interfaceSection)
                    {
                        if (line.Contains(TunnelConfigConstants.PRIVATE_KEY))
                        {
                            interfaceSectionData.PrivateKey = line.Replace(TunnelConfigConstants.PRIVATE_KEY, "");
                        }
                        else if (line.Contains(TunnelConfigConstants.LISTEN_PORT))
                        {
                            interfaceSectionData.ListenPort = Convert.ToInt32(line.Replace(TunnelConfigConstants.LISTEN_PORT, ""));
                        }
                        else if (line.Contains(TunnelConfigConstants.ADDRESS))
                        {
                            interfaceSectionData.Address =
                                line.Replace(TunnelConfigConstants.ADDRESS, "").Split(',').ToList();
                        }
                    }

                    if (peerSection)
                    {
                        if (line.Contains(TunnelConfigConstants.PUBLIC_KEY))
                        {
                            peer.PublicKey = line.Replace(TunnelConfigConstants.PUBLIC_KEY, "");
                        }
                        else if (line.Contains(TunnelConfigConstants.ALLOWED_IPs))
                        {
                            peer.AllowedIPs =
                                line.Replace(TunnelConfigConstants.ALLOWED_IPs, "").Split(',').ToList();
                        }
                        else if (line.Contains(TunnelConfigConstants.ENDPOINT))
                        {
                            peer.Endpoint = line.Replace(TunnelConfigConstants.ENDPOINT, "");
                        }
                    }
                }
                if (peer != null)
                    peerSectionsData.Add(peer);
            }

            interfaceConfig.Interface = interfaceSectionData;
            interfaceConfig.Peers = peerSectionsData;
            return interfaceConfig;
        }

        private TunnelConfig GetHowName(WGInterfaceName interfaceName)
        {
            if (interfaceName == WGInterfaceName.SYNTROPY_PUBLIC)
                return PublicInterface;
            else if (interfaceName == WGInterfaceName.SYNTROPY_SDN1)
                return SDN1Interface;
            else if (interfaceName == WGInterfaceName.SYNTROPY_SDN2)
                return SDN2Interface;
            else if (interfaceName == WGInterfaceName.SYNTROPY_SDN3)
                return SDN3Interface;
            else
                throw new NotFoundInterfaceException();
        }

        private string GetPathToInterfaceConfig(WGInterfaceName nameInterface)
        {
            return $"{WireGuardConstants.CONFIG_FILE_LOCATION}/{nameInterface}.conf";
        }

        [DllImport("tunnel.dll", EntryPoint = "WireGuardTunnelService", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool Run([MarshalAs(UnmanagedType.LPWStr)] string configFile);

        public static NamedPipeClientStream GetPipe(string configFile)
        {
            var pipepath = "ProtectedPrefix\\Administrators\\WireGuard\\" + Path.GetFileNameWithoutExtension(configFile);
            return new NamedPipeClientStream(pipepath);
        }

        private void Add(string configFile, bool ephemeral)
        {
            var tunnelName = Path.GetFileNameWithoutExtension(configFile);
            var shortName = String.Format("WireGuardTunnel${0}", tunnelName);
            var longName = String.Format("{0}: {1}", WireGuardConstants.NAME_WIN_SERVICE, tunnelName);
            var exeName = Process.GetCurrentProcess().MainModule.FileName;
            var pathAndArgs = String.Format("\"{0}\" /service \"{1}\" {2}", exeName, configFile, Process.GetCurrentProcess().Id); //TODO: This is not the proper way to escape file args.

            var scm = Win32.OpenSCManager(null, null, Win32.ScmAccessRights.AllAccess);

            try
            {
                if (scm == IntPtr.Zero)
                    throw new Win32Exception(Marshal.GetLastWin32Error());

                var service = Win32.OpenService(scm, shortName, Win32.ServiceAccessRights.AllAccess);
                if (service != IntPtr.Zero)
                {
                    Win32.CloseServiceHandle(service);
                    Remove(configFile, true);
                }
                service = Win32.CreateService(scm, shortName, longName, Win32.ServiceAccessRights.AllAccess, Win32.ServiceType.Win32OwnProcess, Win32.ServiceStartType.Demand, Win32.ServiceError.Normal, pathAndArgs, null, IntPtr.Zero, "Nsi\0TcpIp", null, null);
                for (int i = 1; i <= 100; i++)
                {
                    if (service == IntPtr.Zero)
                    {
                        Win32.CloseServiceHandle(service);
                        Thread.Sleep(1000);
                        service = Win32.CreateService(scm, shortName, longName, Win32.ServiceAccessRights.AllAccess, Win32.ServiceType.Win32OwnProcess, Win32.ServiceStartType.Demand, Win32.ServiceError.Normal, pathAndArgs, null, IntPtr.Zero, "Nsi\0TcpIp", null, null);
                        continue;
                    }

                    break;
                }
                if (service == IntPtr.Zero)                    
                    throw new Win32Exception(Marshal.GetLastWin32Error());

                try
                {
                    var sidType = Win32.ServiceSidType.Unrestricted;
                    if (!Win32.ChangeServiceConfig2(service, Win32.ServiceConfigType.SidInfo, ref sidType))
                        throw new Win32Exception(Marshal.GetLastWin32Error());

                    var description = new Win32.ServiceDescription { lpDescription = WireGuardConstants.DESCRIPTION_WIN_SERVICE };
                    if (!Win32.ChangeServiceConfig2(service, Win32.ServiceConfigType.Description, ref description))
                        throw new Win32Exception(Marshal.GetLastWin32Error());

                    if (!Win32.StartService(service, 0, null))
                        throw new Win32Exception(Marshal.GetLastWin32Error());

                    if (ephemeral && !Win32.DeleteService(service))
                        throw new Win32Exception(Marshal.GetLastWin32Error());

                    var serviceStatus = new Win32.ServiceStatus();
                    bool createdInterface = false;
                    for (int i = 0; i < 60; i++)
                    {
                        Win32.QueryServiceStatus(service, serviceStatus);

                        if (serviceStatus.dwCurrentState == Win32.ServiceState.Running)
                        {
                            createdInterface = true;
                            break;
                        }
                        else if (serviceStatus.dwCurrentState == Win32.ServiceState.Stopped)
                            break;

                        Thread.Sleep(1000);
                    }
                    if (createdInterface)
                    {
                        log.Info($"[WG_CONF] - Creating interface {tunnelName}");
                        CreateInterfaceEvent?.Invoke(this, new WGConfigServiceEventArgs(GetHowName(GetWGInterfaceNameFromString(tunnelName))));
                    }
                    else
                    {
                        log.Error($"[WG_CONF] - Error creating interface {tunnelName}");
                        ErrorCreateInterfaceEvent?.Invoke(this, new WGConfigServiceEventArgs(GetHowName(GetWGInterfaceNameFromString(tunnelName))));
                    }
                }
                finally
                {
                    Win32.CloseServiceHandle(service);
                }
            }
            catch (Exception ex)
            {
                Win32.CloseServiceHandle(scm);
                log.Error($"[WG_CONF] - Error creating WinService {tunnelName}");
                return;
            }
            finally
            {
                Win32.CloseServiceHandle(scm);
            }
        }

        public static void Remove(string configFile, bool waitForStop)
        {
            var tunnelName = Path.GetFileNameWithoutExtension(configFile);
            var shortName = String.Format("WireGuardTunnel${0}", tunnelName);

            var scm = Win32.OpenSCManager(null, null, Win32.ScmAccessRights.AllAccess);

            try
            {
                if (scm == IntPtr.Zero)
                    throw new Win32Exception(Marshal.GetLastWin32Error());

                var service = Win32.OpenService(scm, shortName, Win32.ServiceAccessRights.AllAccess);
                if (service == IntPtr.Zero)
                {
                    Win32.CloseServiceHandle(service);
                    return;
                }
                try
                {
                    var serviceStatus = new Win32.ServiceStatus();
                    Win32.ControlService(service, Win32.ServiceControl.Stop, serviceStatus);

                    for (int i = 0; waitForStop && i < 180 && Win32.QueryServiceStatus(service, serviceStatus) && serviceStatus.dwCurrentState != Win32.ServiceState.Stopped; ++i)
                        Thread.Sleep(1000);

                    if (!Win32.DeleteService(service) && Marshal.GetLastWin32Error() != 0x00000430)
                        throw new Win32Exception(Marshal.GetLastWin32Error());

                    log.Info($"[WG_CONF] - Deleting WinService {tunnelName}");
                }
                finally
                {
                    Win32.CloseServiceHandle(service);
                }
            }
            catch (Exception ex)
            {
                Win32.CloseServiceHandle(scm);
                log.Info($"[WG_CONF] - Error deleting Winservice {tunnelName}");
                return;
            }
            finally
            {
                Win32.CloseServiceHandle(scm);
            }
        }

        public IEnumerable<PeerDataFromPipe> GetPeersDataFromPipe(WGInterfaceName interfaceName)
        {
            List<PeerDataFromPipe> peersDataFromPipe = new List<PeerDataFromPipe>();

            StreamReader reader = null;
            NamedPipeClientStream stream = null;
            while (ActivityState)
            {
                try
                {
                    stream = GetPipe(GetPathToInterfaceConfig(interfaceName));
                    stream.Connect();
                    reader = new StreamReader(stream);
                    break;
                }
                catch { }
                Thread.Sleep(100);
            }

            PeerDataFromPipe peerDataFromPipe = null;
            try
            {
                var pipe = Encoding.UTF8.GetBytes("get=1\n\n");
                if (stream == null)
                {
                    return peersDataFromPipe;
                }

                stream.Write(pipe, 0, pipe.Length);

                ulong rx = 0, tx = 0;
                while (ActivityState)
                {
                    var line = reader.ReadLine();
                    if (line == null)
                        break;
                    line = line.Trim();
                    if (line.Length == 0)
                        break;
                    if (line.StartsWith("public_key="))
                    {
                        if (peerDataFromPipe == null)
                        {
                            peerDataFromPipe = new PeerDataFromPipe();
                            continue;
                        }

                        peersDataFromPipe.Add(peerDataFromPipe);
                        peerDataFromPipe = new PeerDataFromPipe();
                    }

                    if (line.StartsWith("rx_bytes="))
                        peerDataFromPipe.RxBytes = long.Parse(line.Substring(9));
                    else if (line.StartsWith("tx_bytes="))
                        peerDataFromPipe.TxBytes = long.Parse(line.Substring(9));
                    else if (line.StartsWith("last_handshake_time_sec="))
                        peerDataFromPipe.LastHandshake = line.Substring(24);
                    else if (line.StartsWith("persistent_keepalive_interval="))
                        peerDataFromPipe.KeepAliveInterval = int.Parse(line.Substring(30));
                    else if (line.StartsWith("endpoint="))
                        peerDataFromPipe.Endpoint = line.Substring(9);
                }
                if (peerDataFromPipe != null)
                    peersDataFromPipe.Add(peerDataFromPipe);
            }
            catch { }
            finally
            {
                if (stream != null && stream.IsConnected)
                    stream.Close();
            }

            return peersDataFromPipe;
        }

        public WGInterfaceName GetWGInterfaceNameFromString(string name)
        {
            if (name == WGInterfaceName.SYNTROPY_PUBLIC.ToString())
                return WGInterfaceName.SYNTROPY_PUBLIC;
            else if (name == WGInterfaceName.SYNTROPY_SDN1.ToString())
                return WGInterfaceName.SYNTROPY_SDN1;
            else if (name == WGInterfaceName.SYNTROPY_SDN2.ToString())
                return WGInterfaceName.SYNTROPY_SDN2;
            else if (name == WGInterfaceName.SYNTROPY_SDN3.ToString())
                return WGInterfaceName.SYNTROPY_SDN3;
            else
                throw new NotFoundInterfaceException($"Interface with name '{name}' - not found.");
        }

        public void Dispose()
        {
            StopWG();
        }
    }

    public class WGConfigServiceEventArgs
    {
        public TunnelConfig Interface { get; }

        public WGConfigServiceEventArgs(TunnelConfig Interface)
        {
            this.Interface = Interface;
        }
    }
}
