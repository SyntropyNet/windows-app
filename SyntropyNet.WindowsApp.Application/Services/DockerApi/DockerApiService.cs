using Newtonsoft.Json;
using SyntropyNet.WindowsApp.Application.Contracts;
using SyntropyNet.WindowsApp.Application.Domain.Models.DockerApi;
using SyntropyNet.WindowsApp.Application.Domain.Models.Messages;
using SyntropyNet.WindowsApp.Application.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyntropyNet.WindowsApp.Application.Services.DockerApi
{
    public class DockerApiService : IDockerApiService
    {
        private const string DATATEMPLATE_FOR_PS_COMMAND = "\"{\\\"agent_container_id\\\":{{json .ID }}, \\\"agent_container_name\\\": {{json .Names }}, \\\"agent_container_state\\\": {{json .State }}, \\\"agent_container_uptime\\\": {{json .Status }}}";
        private const string DATATEMPLATE_FOR_INSPECT_COMMAND = "\"{\\\"Networks\\\":[{{range $p, $conf := .NetworkSettings.Networks}}{{json $p}},{{end}}],\\\"Ips\\\":[{{range .NetworkSettings.Networks}}{{json .IPAddress}},{{end}}],\\\"Ports\\\":[{{range $p, $conf := .NetworkSettings.Ports}}{{json $p}},{{end}}]}";
        private const string DATATEMPLATE_FOR_NETWORK_INSPECT_COMMAND = "\"[{{range .IPAM.Config}}{{json .Subnet}},{{end}}]\"";

        public IEnumerable<ContainerInfo> GetContainers()
        {
            List<ContainerInfo> containers = new List<ContainerInfo>();
            containers = GetDataContainersFromPSCommand(containers);
            containers = GetDataContainersFromInspectCommand(containers);
            containers = GetSubnetsNetworkData(containers);

            return containers;
        }

        private List<ContainerInfo> GetDataContainersFromPSCommand(List<ContainerInfo> containers)
        {
            var process = new Process();

            var startinfo = new ProcessStartInfo("cmd", "/c " + $"docker ps --all --no-trunc --format '{DATATEMPLATE_FOR_PS_COMMAND}'");
            startinfo.RedirectStandardOutput = true;
            startinfo.UseShellExecute = false;
            startinfo.CreateNoWindow = true;
            startinfo.RedirectStandardError = true;

            process.StartInfo = startinfo;
            process.OutputDataReceived += (s, e) =>
            {
                Process p = s as Process;
                if (p == null)
                    return;
                if (string.IsNullOrEmpty(e.Data))
                    return;

                ContainerInfo containerInfo =
                    JsonConvert.DeserializeObject<ContainerInfo>(e.Data.Trim('\''), JsonSettings.GetSnakeCaseNamingStrategy());
                containers.Add(containerInfo);

            };
            process.Start();
            process.BeginOutputReadLine();
            process.WaitForExit();

            return containers;
        }

        private List<ContainerInfo> GetDataContainersFromInspectCommand(List<ContainerInfo> containers)
        {
            foreach (var container in containers)
            {
                var process = new Process();

                var startinfo = new ProcessStartInfo("cmd", "/c " + $"docker inspect {container.AgentContainerId} --format '{DATATEMPLATE_FOR_INSPECT_COMMAND}'");
                startinfo.RedirectStandardOutput = true;
                startinfo.UseShellExecute = false;
                startinfo.CreateNoWindow = true;
                startinfo.RedirectStandardError = true;

                process.StartInfo = startinfo;
                process.OutputDataReceived += (s, e) =>
                {
                    Process p = s as Process;
                    if (p == null)
                        return;
                    if (string.IsNullOrEmpty(e.Data))
                        return;

                    DataFromInspectCommand dataFromInspectCommand =
                        JsonConvert.DeserializeObject<DataFromInspectCommand>(e.Data.Trim('\''), JsonSettings.GetSnakeCaseNamingStrategy());

                    container.AgentContainerNetworks = dataFromInspectCommand.Networks;
                    container.AgentContainerIps = dataFromInspectCommand.Ips;
                    foreach (var port in dataFromInspectCommand.Ports)
                    {
                        string[] subs = new string[2];
                        subs = port.Split('/');

                        if (subs[1] == "tcp")
                        {
                            var tcp = container.AgentContainerPorts.Tcp.ToList();
                            tcp.Add(Convert.ToInt32(subs[0]));
                            container.AgentContainerPorts.Tcp = tcp;
                        }
                        else if (subs[1] == "udp")
                        {
                            var udp = container.AgentContainerPorts.Udp.ToList();
                            udp.Add(Convert.ToInt32(subs[0]));
                            container.AgentContainerPorts.Tcp = udp;
                        }
                    }
                };
                process.Start();
                process.BeginOutputReadLine();
                process.WaitForExit();
            }

            return containers;
        }

        private List<ContainerInfo> GetSubnetsNetworkData(List<ContainerInfo> containers)
        {
            foreach (var container in containers)
            {
                var process = new Process();

                var startinfo = new ProcessStartInfo("cmd", "/c " + $"docker network inspect {String.Join(" ", container.AgentContainerNetworks)} --format '{DATATEMPLATE_FOR_NETWORK_INSPECT_COMMAND}'");
                startinfo.RedirectStandardOutput = true;
                startinfo.UseShellExecute = false;
                startinfo.CreateNoWindow = true;
                startinfo.RedirectStandardError = true;

                process.StartInfo = startinfo;
                process.OutputDataReceived += (s, e) =>
                {
                    Process p = s as Process;
                    if (p == null)
                        return;
                    if (string.IsNullOrEmpty(e.Data))
                        return;
                    var data = e.Data.Trim('\'');
                    List<string> subnets =
                        JsonConvert.DeserializeObject<List<string>>(e.Data.Trim('\''));
                    var sub = container.AgentContainerSubnets.ToList();
                    container.AgentContainerSubnets = sub.Union(subnets).ToList();
                };
                process.Start();
                process.BeginOutputReadLine();
                process.WaitForExit();
            }

            return containers;
        }
    }
}
