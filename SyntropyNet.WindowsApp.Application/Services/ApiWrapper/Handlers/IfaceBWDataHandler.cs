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
    public class IfaceBWDataHandler : BaseHandler
    {
        private static int REFRESH_INFO = 10000;
        private readonly INetworkInformationService _networkInformationService;

        private Thread mainTask;
        public IfaceBWDataHandler(WebsocketClient client, INetworkInformationService networkInformationService) : base(client)
        {
            _networkInformationService = networkInformationService;
        }

        public void Start()
        {
            mainTask?.Abort();

            mainTask = new Thread(async () =>
            {
                while (true)
                {
                    var ifaceBWDataRequest = new IfaceBWDataRequest
                    {
                        Data = _networkInformationService.GetInformNetworkInterface()
                    };

                    var message = JsonConvert.SerializeObject(ifaceBWDataRequest,
                        JsonSettings.GetSnakeCaseNamingStrategy());
                    Debug.WriteLine($"IFACES_BW_DATA: {message}");
                    Client.Send(message);

                    await Task.Delay(REFRESH_INFO);
                }

            });

            mainTask.Start();
        }

        public void Interrupt()
        {
            mainTask?.Abort();
        }
    }
}
