using log4net;
using Newtonsoft.Json;
using SyntropyNet.WindowsApp.Application.Contracts;
using SyntropyNet.WindowsApp.Application.Domain.Events;
using SyntropyNet.WindowsApp.Application.Domain.Models.Messages;
using SyntropyNet.WindowsApp.Application.Helpers;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Websocket.Client;

namespace SyntropyNet.WindowsApp.Application.Services.ApiWrapper.Handlers {
    public class IfacesPeersActiveDataHandler : BaseHandler {
        private static readonly ILog log = LogManager.GetLogger(typeof(IfacesPeersBWDataHandler));

        private readonly bool DebugLogger;
        private readonly INetworkInformationService _networkInformationService;
        private readonly IAppSettings _appSettings;

        public IfacesPeersActiveDataHandler(
            WebsocketClient client,
            INetworkInformationService networkInformationService,
            IAppSettings appSettings) : base(client) {

            _networkInformationService = networkInformationService;
            _appSettings = appSettings;
            DebugLogger = Convert.ToBoolean(ConfigurationManager.AppSettings.Get("DebugLogger"));
        }

        public void Start() {
            _networkInformationService.RerouteEvent += _SendMessageAfterReroute;
        }

        public void Stop() {
            _networkInformationService.RerouteEvent -= _SendMessageAfterReroute;
        }

        private void _SendMessageAfterReroute(object sender, RerouteEventArgs args) {
            IfacesPeersActiveDataRequest request = new IfacesPeersActiveDataRequest() {
                Data = new List<IfacesPeersActiveDataRequestData> { 
                    new IfacesPeersActiveDataRequestData { 
                        ConnectionId = args.ConnectionId,
                        Timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss")
                    }
                }
            };

            var message = JsonConvert.SerializeObject(request,
                            JsonSettings.GetSnakeCaseNamingStrategy());
            Client.Send(message);

            if (DebugLogger)
                LoggerRequestHelper.Send(
                    Client,
                    log4net.Core.Level.Debug,
                    _appSettings.DeviceId,
                    _appSettings.DeviceName,
                    _appSettings.DeviceIp,
                    message);
        }
    }
}
