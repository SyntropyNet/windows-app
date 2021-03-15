using Newtonsoft.Json;
using SyntropyNet.WindowsApp.Application.Constants;
using SyntropyNet.WindowsApp.Application.Contracts;
using SyntropyNet.WindowsApp.Application.Domain.Models.Messages;
using SyntropyNet.WindowsApp.Application.Services.HttpRequest;
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
        public static void Send(WebsocketClient client, log4net.Core.Level level, string deviceId, string deviceName, string publicIp, string message)
        {
            LoggerRequest loggerRequest = new LoggerRequest
            {
                Data = new LoggerRequestData
                {
                    Severity = level.ToString(),
                    Message = message,
                    Metadata = new LoggerRequestMetadata
                    {
                        DeviceId = deviceId,
                        DeviceName = deviceName,
                        DevicePublicIpv4 = publicIp,
                        //ToDo: what should be in ConnectionId
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
