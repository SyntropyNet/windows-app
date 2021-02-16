using Newtonsoft.Json;
using SyntropyNet.WindowsApp.Application.Domain.Models.Messages;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
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
            mainTask?.Abort();

            mainTask = new Thread(async () =>
            {
                var responseData = new GetInfoResponseData();

                responseData.AgentProvider = GetAgentProvider();
                responseData.ServiceStatus = GetServiceStatus();
                responseData.AgentTags = GetAgentTags();
                responseData.ExternalIp = GetExternalIp();
                responseData.NetworkInfo = GetNetworkInfo();
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

        private IEnumerable<BaseNetworkInfo> GetNetworkInfo()
        {
            //ToDo: Need to implement the GetNetworkInfo method
            return null;
        }

        private string GetExternalIp()
        {
            // ToDo:: url hardcode values
            var request = WebRequest.CreateHttp("https://ip.syntropystack.com/");
            var response = request.GetResponseAsync().GetAwaiter().GetResult();

            string data = "";
            using (Stream stream = response.GetResponseStream())
            {
                using (StreamReader reader = new StreamReader(stream))
                {
                    data = reader.ReadLine();
                }
            }

            return data.Trim('\"');
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
