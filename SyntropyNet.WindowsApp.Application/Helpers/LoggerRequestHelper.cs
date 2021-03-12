using Newtonsoft.Json;
using SyntropyNet.WindowsApp.Application.Contracts;
using SyntropyNet.WindowsApp.Application.Domain.Models.Messages;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Websocket.Client;

namespace SyntropyNet.WindowsApp.Application.Helpers
{
    public static class LoggerRequestHelper
    {
        public static void Send(WebsocketClient client, IAppSettings appSettings, string message)
        {
            LoggerRequest loggerRequest = new LoggerRequest
            {
                Data = new LoggerRequestData
                {
                    Severity = log4net.Core.Level.Debug.ToString(),
                    Message = message,
                    Metadata = new LoggerRequestMetadata
                    {
                        DeviceId = appSettings.DeviceId,
                        DeviceName = appSettings.DeviceName,
                        //ToDo: what should be in DevicePublicIpv4 and ConnectionId
                        DevicePublicIpv4 = "",
                        ConnectionId = 0
                    }
                }
            };

            var request = JsonConvert.SerializeObject(loggerRequest,
                JsonSettings.GetSnakeCaseNamingStrategy());
            Debug.WriteLine($"Logger: {request}");
            client.Send(request);
        }
    }
}
