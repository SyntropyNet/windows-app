using System;
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
        private Stopwatch _pingTimer;

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
            Interrupt();

            if (!request.Data.Ips.Any())
            {
                return;
            }

            mainTask = new Thread(async () =>
            {
                try
                {
                    while (true) {
                        bool processPing = false;

                        if (_pingTimer == null) {
                            _pingTimer = new Stopwatch();
                            _pingTimer.Start();
                            processPing = true;
                        } else if (!_pingTimer.IsRunning) {
                            _pingTimer.Start();
                        }

                        if (_pingTimer.ElapsedMilliseconds >= (request.Data.Interval * 1000)) {
                            _pingTimer.Restart();
                            processPing = true;
                        }

                        if (processPing) {
                            var responseData = new AutoPingResponseData();
                            var results = new List<AutoPingResponseItem>();

                            Parallel.ForEach(request.Data.Ips, x => results.Add(Ping(x)));
                            IEnumerable<AutoPingResponseItem> orderedResults = results.OrderByDescending(p => p.LatencyMs.HasValue)
                                .ThenBy(p => p.LatencyMs);

                            int resultsToTake = request.Data.ResponseLimit > 0 ? request.Data.ResponseLimit : 5; // Assign a default value if missed

                            if (resultsToTake >= orderedResults.Count()) {
                                results = orderedResults.ToList();
                            } else {
                                results = orderedResults.Take(resultsToTake).ToList();
                            }

                            var response = new AutoPingResponse {
                                Id = $"ID:{DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond}",
                                Data = new AutoPingResponseData { Pings = results }
                            };

                            var message = JsonConvert.SerializeObject(response,
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
                        }
                    }
                } 
                catch(Exception ex)
                {
                    if(!(ex is System.Threading.ThreadAbortException))
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
                            log.Error($"[Message: {ex2.Message}, stacktrace: {ex2.StackTrace}]", ex);
                        }
                    }
                }
            });

            mainTask.Start();
        }

        public void Interrupt()
        {
            mainTask?.Abort();
            _pingTimer?.Reset();
        }

        private AutoPingResponseItem Ping(string ip)
        {
            var result = new AutoPingResponseItem();
            result.Ip = ip;
            var pingSender = new Ping();

            var ipParse = IPAddress.Parse(ip);
            int successCount = 0;
            long summLatency = 0;
            for (var i = 0; i < pingAmount; i++)
            {
                try
                {
                    var reply = pingSender.Send(ipParse);
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
                catch (Exception ex)
                {
                    result.PacketLoss++;
                } 
            }

            if (successCount > 0)
            {
                result.LatencyMs = summLatency / successCount;
            }
            else
            {
                result.LatencyMs = null;
            }
            return result;
        }
    }
}