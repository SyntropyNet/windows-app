using SyntropyNet.WindowsApp.Application.Constants.WireGuard;
using SyntropyNet.WindowsApp.Application.Contracts;
using SyntropyNet.WindowsApp.Application.Domain.Models.WireGuard;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SyntropyNet.WindowsApp.Application.Services.WireGuard
{

    public class WGConfigService : IWGConfigService
    {
        private readonly TunnelSettings _tunnelSettings;

        public WGConfigService(TunnelSettings tunnelSettings)
        {
            _tunnelSettings = tunnelSettings;
            InterfaceName = _tunnelSettings.IntefaceName;
            GenerateNewConfig();
            Add(_tunnelSettings.FileLocation, true);
        }

        public string PublicKey { get; private set; }
        public string InterfaceName { get; }
        public bool ActivityState { 
            get
            {
                var tunnelName = Path.GetFileNameWithoutExtension(_tunnelSettings.FileLocation);
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
                    return true;
                }
                else
                {
                    Win32.CloseServiceHandle(service);
                    return false;
                }
            }
        }

        public void RunWG()
        {
            Add(_tunnelSettings.FileLocation, true);
        }

        public void StopWG()
        {
            Remove(_tunnelSettings.FileLocation, true);
        } 

        public void SetInterface(Interface @interface)
        {
            TunnelConfig config = GetTunnelConfig();
            config.Interface = @interface;

            SetTunnelConfig(config);
        }

        public Interface GetInterface()
        {
            TunnelConfig config = GetTunnelConfig();

            return config.Interface;
        }

        public void SetPeers(IEnumerable<Peer> peers)
        {
            TunnelConfig config = GetTunnelConfig();
            config.Peers = peers;

            SetTunnelConfig(config);
        }

        public void ApplyChange()
        {
            Remove(_tunnelSettings.FileLocation, true);
            Add(_tunnelSettings.FileLocation, true);
        }

        public void CreateConfig()
        {
            GenerateNewConfig();
            Add(_tunnelSettings.FileLocation, true);
        }

        public void RemoveConfig()
        {
            Remove(_tunnelSettings.FileLocation, true);
            File.Delete(_tunnelSettings.FileLocation);
        }

        public string PathToConfigFile()
        {
            return _tunnelSettings.FileLocation;
        }

        public IEnumerable<Peer> GetPeers()
        {
            TunnelConfig config = GetTunnelConfig();

            return config.Peers;
        }

        private void SetTunnelConfig(TunnelConfig config)
        {
            StringBuilder configString = new StringBuilder();

            configString.AppendLine("[Interface]");
            if (config.Interface != null)
            {
                configString.AppendLine(
                    TunnelConfigConstants.PRIVATE_KEY + config.Interface.PrivateKey);
                configString.AppendLine(
                    TunnelConfigConstants.LISTEN_PORT + config.Interface.ListenPort);
                if (config.Interface.Address != null && config.Interface.Address.Count() > 0)
                    configString.AppendLine(
                        TunnelConfigConstants.ADDRESS + String.Join(",", config.Interface.Address));
            }

            if (config.Peers != null)
            {
                foreach (var item in config.Peers)
                {
                    configString.AppendLine("[Peer]")
                        .AppendLine(
                            TunnelConfigConstants.PUBLIC_KEY + item.PublicKey)
                        .AppendLine(
                            TunnelConfigConstants.ALLOWED_IPs + String.Join(",", item.AllowedIPs))
                        .AppendLine(
                            TunnelConfigConstants.ENDPOINT + item.Endpoint)
                        .AppendLine(
                            TunnelConfigConstants.PERSISTEN_KEEPALIVE);
                }
            }

            using (StreamWriter sw = new StreamWriter(_tunnelSettings.FileLocation, false, Encoding.Default))
            {
                sw.Write(configString);
            }

        }

        private TunnelConfig GetTunnelConfig()
        {
            bool interfaceSection = false;
            bool peerSection = false;

            Interface @interface = new Interface();
            List<Peer> peers = new List<Peer>();
            Peer peer = null;

            using (StreamReader sr = new StreamReader(_tunnelSettings.FileLocation, System.Text.Encoding.Default))
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
                            peers.Add(peer);
                            peer = new Peer();
                        }
                    }

                    if (interfaceSection)
                    {
                        if (line.Contains(TunnelConfigConstants.PRIVATE_KEY))
                        {
                            @interface.PrivateKey = line.Replace(TunnelConfigConstants.PRIVATE_KEY, "");
                        }
                        else if (line.Contains(TunnelConfigConstants.LISTEN_PORT))
                        {
                            @interface.ListenPort = Convert.ToInt32(line.Replace(TunnelConfigConstants.LISTEN_PORT, ""));
                        }
                        else if (line.Contains(TunnelConfigConstants.ADDRESS))
                        {
                            @interface.Address =
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
                    peers.Add(peer);
            }

            return new TunnelConfig
            {
                Interface = @interface,
                Peers = peers
            };
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
            if (scm == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error());
            try
            {
                var service = Win32.OpenService(scm, shortName, Win32.ServiceAccessRights.AllAccess);
                if (service != IntPtr.Zero)
                {
                    Win32.CloseServiceHandle(service);
                    Remove(configFile, true);
                }
                service = Win32.CreateService(scm, shortName, longName, Win32.ServiceAccessRights.AllAccess, Win32.ServiceType.Win32OwnProcess, Win32.ServiceStartType.Demand, Win32.ServiceError.Normal, pathAndArgs, null, IntPtr.Zero, "Nsi\0TcpIp", null, null);
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
                }
                finally
                {
                    Win32.CloseServiceHandle(service);
                }
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
            if (scm == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error());
            try
            {
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
                }
                finally
                {
                    Win32.CloseServiceHandle(service);
                }
            }
            finally
            {
                Win32.CloseServiceHandle(scm);
            }
        }

        private void GenerateNewConfig()
        {
            Keypair keypair = Keypair.Generate();
            PublicKey = keypair.Public;

            Interface @interface = new Interface
            {
                PrivateKey = keypair.Private,
                //ToDo: hardcode port
                ListenPort = 61173
            };
            var tunnelConfig = new TunnelConfig
            {
                Interface = @interface,
            };
            SetTunnelConfig(tunnelConfig);
        }

        public IEnumerable<PeerDataFromPipe> GetPeersDataFromPipe()
        {
            List<PeerDataFromPipe> peersDataFromPipe = new List<PeerDataFromPipe>();

            StreamReader reader = null;
            NamedPipeClientStream stream = null;
            while (ActivityState)
            {
                try
                {
                    stream = GetPipe(_tunnelSettings.FileLocation);
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
                        if(peerDataFromPipe == null)
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
                if (stream.IsConnected)
                    stream.Close();
            }

            return peersDataFromPipe;
        }

        public void Dispose()
        {
            Remove(File.ReadAllText(_tunnelSettings.FileLocation), false);
        }
    }
}
