using log4net;
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
        private static readonly ILog log = LogManager.GetLogger(typeof(IfaceBWDataHandler));

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
                try
                {
                    while (true)
                    {
                        var ifaceBWDataRequest = new IfaceBWDataRequest
                        {
                            Data = _networkInformationService.GetInformNetworkInterface()
                        };

                        var message = JsonConvert.SerializeObject(ifaceBWDataRequest,
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

                        Thread.Sleep(REFRESH_INFO);
                    }
                }
                catch(Exception ex)
                {
                    if (!(ex is System.Threading.ThreadAbortException))
                    {
                        try
                        {
                            LoggerRequestHelper.Send(
                                Client,
                                log4net.Core.Level.Error,
                                _appSettings.DeviceId,
                                _appSettings.DeviceName,
                                _appSettings.DeviceIp,
                                $"[Message: {ex.Message}, stacktrace: {ex.StackTrace}]");
                        }
                        catch (Exception ex2)
                        {
                            log.Error($"[Message: {ex2.Message}, stacktrace: {ex2.StackTrace}]");
                        }
                    }
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
