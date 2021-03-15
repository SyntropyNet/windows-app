﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SyntropyNet.WindowsApp.Application.Constants;
using SyntropyNet.WindowsApp.Application.Contracts;
using SyntropyNet.WindowsApp.Application.Domain.Models.Messages;
using SyntropyNet.WindowsApp.Application.Helpers;
using Websocket.Client;
using System.Configuration;
using log4net;

namespace SyntropyNet.WindowsApp.Application.Services.ApiWrapper.Handlers
{
    public class AutoPingHandler: BaseHandler
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(AutoPingHandler));

        private static int pingAmount = 5;
        private readonly IAppSettings _appSettings;
        private readonly IHttpRequestService _httpRequestService;
        private readonly bool DebugLogger;

        private Thread mainTask;
        public AutoPingHandler(
            WebsocketClient client, 
            IAppSettings appSettings,
            IHttpRequestService httpRequestService) 
        : base(client)
        {
            DebugLogger = Convert.ToBoolean(ConfigurationManager.AppSettings.Get("DebugLogger"));

            _appSettings = appSettings;
            _httpRequestService = httpRequestService;
        }

        public void Start(AutoPingRequest request)
        {
            mainTask?.Abort();

            if (!request.Data.Ips.Any())
            {
                return;
            }

            mainTask = new Thread(async () =>
            {
                try
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
                            Data = new AutoPingResponseData { Pings = results }
                        };

                        var message = JsonConvert.SerializeObject(response,
                            JsonSettings.GetSnakeCaseNamingStrategy());
                        Debug.WriteLine($"auto ping: {message}");
                        Client.Send(message);

                        if (DebugLogger)
                            LoggerRequestHelper.Send(
                                Client,
                                log4net.Core.Level.Debug,
                                _appSettings.DeviceId,
                                _appSettings.DeviceName,
                                _httpRequestService.GetResponse(AppConstants.EXTERNAL_IP_URL),
                                message);

                        //await Task.Delay(TimeSpan.FromSeconds(request.Data.Interval));
                        Thread.Sleep(TimeSpan.FromSeconds(request.Data.Interval));
                    }
                } 
                catch(Exception ex)
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