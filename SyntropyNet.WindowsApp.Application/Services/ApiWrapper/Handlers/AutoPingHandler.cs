using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SyntropyNet.WindowsApp.Application.Domain.Models.Messages;
using Websocket.Client;

namespace SyntropyNet.WindowsApp.Application.Services.ApiWrapper.Handlers
{
    public class AutoPingHandler: BaseHandler
    {
        private static int pingAmount = 5;

        private Thread mainTask;
        public AutoPingHandler(WebsocketClient client) : base(client)
        {
            
        }

        public void Start(AutoPingRequest request)
        {
            mainTask?.Abort();

            //if (!request.Data.Ips.Any())
            //{
            //    return;
            //}

            mainTask = new Thread(async () =>
            {
                while (true)
                {
                    var responseData = new AutoPingResponseData();
                    var results = new List<AutoPingResponseItem>();
                    foreach (var ip in request.Data.Ips)
                    {
                        results.Add(Ping(ip));
                    }

                    var response = new AutoPingResponse
                    {
                        Id = $"ID:{DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond}", 
                        Data = new AutoPingResponseData {Pings = results}
                    };

                    var message = JsonConvert.SerializeObject(response);
                    Debug.WriteLine($"auto ping: {message}");
                    Client.Send(message);

                    Thread.Sleep(TimeSpan.FromSeconds(request.Data.Interval));
                }

            });

            mainTask.Start();
        }

        public void Interrupt()
        {
            mainTask?.Abort();
        }

        private AutoPingResponseItem Ping(string ip)
        {
            var result = new AutoPingResponseItem();
            result.Ip = ip;
            var pingSender = new Ping();

            int successCount = 0;
            long summLatency = 0;
            for (var i = 0; i < pingAmount; i++)
            {
                var reply = pingSender.Send(ip);
               if (reply.Status != IPStatus.Success)
               {
                   result.PacketLoss++;
               }
               else
               {
                   successCount++;
                   summLatency += reply.RoundtripTime;
               }  
            }

            if (successCount > 0)
            {
                result.LatencyMs = summLatency / successCount;
            }

            return result;
        }
    }
}