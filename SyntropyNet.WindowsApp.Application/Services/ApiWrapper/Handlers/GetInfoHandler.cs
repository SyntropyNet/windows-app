using Newtonsoft.Json;
using SyntropyNet.WindowsApp.Application.Constants;
using SyntropyNet.WindowsApp.Application.Contracts;
using SyntropyNet.WindowsApp.Application.Domain.Models.Messages;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using Websocket.Client;

namespace SyntropyNet.WindowsApp.Application.Services.ApiWrapper.Handlers
{
    class GetInfoHandler : BaseHandler
    {
        private readonly IHttpRequestService _httpRequestService;
        private Thread mainTask;

        public GetInfoHandler(WebsocketClient client, IHttpRequestService httpRequestService) 
            : base(client)
        {
            _httpRequestService = httpRequestService;
        }

        public void Start(GetInfoRequest request)
        {
            mainTask?.Abort();

            mainTask = new Thread(async () =>
            {
                var responseData = new GetInfoResponseData();

                responseData.AgentProvider = GetAgentProvider();
                responseData.ServiceStatus = GetServiceStatus();
                responseData.AgentTags = GetAgentTags();
                responseData.ExternalIp = GetExternalIp();
                responseData.ContainerInfo = GetContainerInfo();

                var response = new GetInfoResponse
                {
                    Id = request.Id,
                    Data = responseData,
                    Type = request.Type
                };

                var message = JsonConvert.SerializeObject(response);
                Debug.WriteLine($"auto ping: {message}");
                Client.Send(message);
            });

            mainTask.Start();
        }
        public void Interrupt()
        {
            mainTask?.Abort();
        }

        private IEnumerable<ContainerInfo> GetContainerInfo()
        {
            //ToDo: Need to implement the GetContainerInfo method
            return null;
        }

        private string GetExternalIp()
        {
            return _httpRequestService.GetResponse(AppConstants.EXTERNAL_IP_URL);
        }

        private IEnumerable<string> GetAgentTags()
        {
            //ToDo: Need to implement the GetAgentTags method
            return null;
        }

        private bool GetServiceStatus()
        {
            return Convert.ToBoolean(
                Environment.GetEnvironmentVariable("SYNTROPY_SERVICES_STATUS"));
        }

        private int? GetAgentProvider()
        {
            bool isParsable = int.TryParse(
                Environment.GetEnvironmentVariable("SYNTROPY_PROVIDER"), out int result);
            if (isParsable)
                return result;

            return null;
        }
    }
}
