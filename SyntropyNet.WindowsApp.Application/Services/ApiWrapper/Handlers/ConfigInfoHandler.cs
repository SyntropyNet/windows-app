using SyntropyNet.WindowsApp.Application.Domain.Models.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Websocket.Client;

namespace SyntropyNet.WindowsApp.Application.Services.ApiWrapper.Handlers
{
    public class ConfigInfoHandler : BaseHandler
    {
        private Thread mainTask;

        public ConfigInfoHandler(WebsocketClient client) : base(client)
        {
        }

        public void Start(ConfigInfoRequest request)
        {
            mainTask?.Abort();

            mainTask = new Thread(async () =>
            {
                //ToDo: Implement the logic for processing the request
            });

            mainTask.Start();
        }

        public void Interrupt()
        {
            mainTask?.Abort();
        }
    }
}
