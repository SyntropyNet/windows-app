using Newtonsoft.Json;
using SyntropyNet.WindowsApp.Application.Contracts;
using SyntropyNet.WindowsApp.Application.Domain.Models.Messages;
using SyntropyNet.WindowsApp.Application.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Websocket.Client;

namespace SyntropyNet.WindowsApp.Application.Services.ApiWrapper.Handlers
{
    public class ContainerInfoHandler : BaseHandler
    {
        private static int REFRESH_INFO = 15000;
        private readonly IDockerApiService _dockerApiService;
        private IEnumerable<ContainerInfo> ContainerInfoList { get; set; }

        private Thread mainTask;
        public ContainerInfoHandler(WebsocketClient client, IDockerApiService dockerApiService) : base(client)
        {
            _dockerApiService = dockerApiService;
            ContainerInfoList = new List<ContainerInfo>();
        }

        public void Start()
        {
            mainTask?.Abort();

            mainTask = new Thread(async () =>
            {
                while (true)
                {
                    var newContainerInfoList = _dockerApiService.GetContainers();
                    if (!CompareContainers(newContainerInfoList))
                    {
                        ContainerInfoList = newContainerInfoList;

                        var containerInfoRequest = new ContainerInfoRequest
                        {
                            Data = ContainerInfoList
                        };

                        var message = JsonConvert.SerializeObject(containerInfoRequest,
                            JsonSettings.GetSnakeCaseNamingStrategy());
                        Debug.WriteLine($"Updated info containers: {message}");
                        Client.Send(message);
                    }

                    Thread.Sleep(REFRESH_INFO);
                }

            });

            mainTask.Start();
        }

        private bool CompareContainers(IEnumerable<ContainerInfo> newContainerList)
        {
            return ContainerInfoList.SequenceEqual(newContainerList);
        }

        public void Interrupt()
        {
            mainTask?.Abort();
        }
    }
}
