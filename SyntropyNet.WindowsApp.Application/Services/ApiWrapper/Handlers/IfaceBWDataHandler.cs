using Newtonsoft.Json;
using SyntropyNet.WindowsApp.Application.Constants;
using SyntropyNet.WindowsApp.Application.Contracts;
using SyntropyNet.WindowsApp.Application.Domain.Models.Messages;
using SyntropyNet.WindowsApp.Application.Helpers;
using System;
using System.Collections.Generic;
using System.Configuration;
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
        private readonly bool DebugLogger;
        private static int REFRESH_INFO = 10000;
        private readonly INetworkInformationService _networkInformationService;
        private readonly IAppSettings _appSettings;
        private readonly IHttpRequestService _httpRequestService;

        private Thread mainTask;
        public IfaceBWDataHandler(
            WebsocketClient client,
            INetworkInformationService networkInformationService,
            IAppSettings appSettings,
            IHttpRequestService httpRequestService) 
            : base(client)
        {
            DebugLogger = Convert.ToBoolean(ConfigurationManager.AppSettings.Get("DebugLogger"));

            _networkInformationService = networkInformationService;
            _appSettings = appSettings;
            _httpRequestService = httpRequestService;
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

                    if (DebugLogger)
                        LoggerRequestHelper.Send(
                            Client,
                            log4net.Core.Level.Debug,
                            _appSettings.DeviceId,
                            _appSettings.DeviceName,
                            _httpRequestService.GetResponse(AppConstants.EXTERNAL_IP_URL),
                            message);

                    //await Task.Delay(REFRESH_INFO);
                    Thread.Sleep(REFRESH_INFO);
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
