using Newtonsoft.Json;
using SyntropyNet.WindowsApp.Application.Constants;
using SyntropyNet.WindowsApp.Application.Contracts;
using SyntropyNet.WindowsApp.Application.Domain.Models.Messages;
using SyntropyNet.WindowsApp.Application.Helpers;
using SyntropyNet.WindowsApp.Application.Services.DockerApi;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using Websocket.Client;

namespace SyntropyNet.WindowsApp.Application.Services.ApiWrapper.Handlers
{
    class GetInfoHandler : BaseHandler
    {
        private readonly bool DebugLogger;
        private readonly IHttpRequestService _httpRequestService;
        private readonly IDockerApiService _dockerApiService;
        private readonly IAppSettings _appSettings;
        private Thread mainTask;

        public GetInfoHandler(WebsocketClient client, 
            IHttpRequestService httpRequestService,
            IDockerApiService dockerApiService,
            IAppSettings appSettings) 
            : base(client)
        {
            DebugLogger = Convert.ToBoolean(ConfigurationManager.AppSettings.Get("DebugLogger"));

            _httpRequestService = httpRequestService;
            _dockerApiService = dockerApiService;
            _appSettings = appSettings;
        }

        public void Start(GetInfoRequest request)
        {
            mainTask?.Abort();

            mainTask = new Thread(async () =>
            {
                var responseData = new GetInfoResponseData();

                responseData.AgentProvider = GetAgentProvider();
                responseData.ServiceStatus = GetServiceStatus();
                responseData.ExternalIp = GetExternalIp();
                responseData.ContainerInfo = GetContainerInfo();

                var response = new GetInfoResponse
                {
                    Id = request.Id,
                    Data = responseData,
                };

                var message = JsonConvert.SerializeObject(response, 
                    JsonSettings.GetSnakeCaseNamingStrategy());
                Debug.WriteLine($"Get info: {message}");
                Client.Send(message);

                if (DebugLogger)
                    LoggerRequestHelper.Send(Client, _appSettings, log4net.Core.Level.Debug, message);
            });

            mainTask.Start();
        }
        public void Interrupt()
        {
            mainTask?.Abort();
        }

        private IEnumerable<ContainerInfo> GetContainerInfo()
        {
            var test = _dockerApiService.GetContainers();
            return _dockerApiService.GetContainers();
        }

        private string GetExternalIp()
        {
            return _httpRequestService.GetResponse(AppConstants.EXTERNAL_IP_URL);
        }

        private bool GetServiceStatus()
        {
            return Convert.ToBoolean(ConfigurationManager.AppSettings.Get("SYNTROPY_SERVICES_STATUS"));
        }

        private int? GetAgentProvider()
        {
            bool isParsable = int.TryParse(
                ConfigurationManager.AppSettings.Get("SYNTROPY_PROVIDER"), out int result);
            if (isParsable)
                return result;

            return null;
        }
    }
}
