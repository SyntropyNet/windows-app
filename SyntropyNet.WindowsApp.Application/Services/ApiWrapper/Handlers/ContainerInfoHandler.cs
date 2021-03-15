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
    public class ContainerInfoHandler : BaseHandler
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ContainerInfoHandler));

        private readonly bool DebugLogger;
        private static int REFRESH_INFO = 15000;
        private readonly IDockerApiService _dockerApiService;
        private readonly IAppSettings _appSettings;
        private readonly IHttpRequestService _httpRequestService;

        private IEnumerable<ContainerInfo> ContainerInfoList { get; set; }

        private Thread mainTask;
        public ContainerInfoHandler(
            WebsocketClient client, 
            IDockerApiService dockerApiService,
            IAppSettings appSettings,
            IHttpRequestService httpRequestService) 
            : base(client)
        {
            DebugLogger = Convert.ToBoolean(ConfigurationManager.AppSettings.Get("DebugLogger"));

            _dockerApiService = dockerApiService;
            ContainerInfoList = new List<ContainerInfo>();
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

                            if (DebugLogger)
                                LoggerRequestHelper.Send(
                                    Client,
                                    log4net.Core.Level.Debug,
                                    _appSettings.DeviceId,
                                    _appSettings.DeviceName,
                                    _httpRequestService.GetResponse(AppConstants.EXTERNAL_IP_URL),
                                    message);
                        }

                        //await Task.Delay(REFRESH_INFO);
                        Thread.Sleep(REFRESH_INFO);
                    }
                }
                catch (Exception ex)
                {
                    try
                    {
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
