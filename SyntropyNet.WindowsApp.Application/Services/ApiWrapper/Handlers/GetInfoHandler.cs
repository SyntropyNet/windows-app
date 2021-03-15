using log4net;
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
        private static readonly ILog log = LogManager.GetLogger(typeof(GetInfoHandler));

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
                try
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
                        LoggerRequestHelper.Send(
                            Client,
                            log4net.Core.Level.Debug,
                            _appSettings.DeviceId,
                            _appSettings.DeviceName,
                            _httpRequestService.GetResponse(AppConstants.EXTERNAL_IP_URL),
                            message);
                }
                catch(Exception ex)
                {
                    try
                    {
                        var errorMsg = new GetInfoError
                        {
                            Id = request.Id,
                            Error = new GetInfoErrorData
                            {
                                Messages = ex.Message,
                                Stacktrace = ex.StackTrace
                            }
                        };

                        var message = JsonConvert.SerializeObject(errorMsg,
                            JsonSettings.GetSnakeCaseNamingStrategy());
                        Debug.WriteLine($"'GET_INFO' error: {message}");
                        Client.Send(message);

                        LoggerRequestHelper.Send(
                            Client,
                            log4net.Core.Level.Error,
                            _appSettings.DeviceId,
                            _appSettings.DeviceName,
                            _httpRequestService.GetResponse(AppConstants.EXTERNAL_IP_URL),
                            $"[Message: {ex.Message}, stacktrace: {ex.StackTrace}]");
                    }
                    catch (Exception ex2)
                    {
                        log.Error($"[Message: {ex2.Message}, stacktrace: {ex2.StackTrace}]");
                    }
                }
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
