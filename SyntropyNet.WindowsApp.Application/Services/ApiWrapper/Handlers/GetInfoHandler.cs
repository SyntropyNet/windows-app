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
    class GetInfoHandler : BaseHandler
    {
        private Thread mainTask;

        public GetInfoHandler(WebsocketClient client) : base(client)
        {
        }

        public void Start(GetInfoRequest request)
        {
            // ToDo: create a response for the handler;
        }

        public void Interrupt()
        {
            mainTask?.Abort();
        }
    }
}
