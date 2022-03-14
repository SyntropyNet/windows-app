using log4net;
using SyntropyNet.WindowsApp.Application.Constants;
using SyntropyNet.WindowsApp.Application.Contracts;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SyntropyNet.WindowsApp.Application.Services.NetworkInformation
{
    public class PublicIPChecker: IPublicIPChecker
    {
        public delegate void IpChanged(string newIp);
        public event IpChanged IpChangedEvent;

        private static readonly ILog _log = LogManager.GetLogger(typeof(SdnRouter));

        private readonly IAppSettings _appSettings;
        private readonly IHttpRequestService _httpRequestService;
        private bool _checkerStarted = false;
        private static object _checkerLock = new object();
        private int _timeoutDelayMs = 10000;
        private Thread runnerThread;
        private CancellationToken _CheckerCancelToken;
        private CancellationTokenSource _TokenSource;

        private static readonly HttpClient client = new HttpClient(){
            Timeout = new System.TimeSpan(0, 0, 5)
        };

        // Note: registered as singleton in IoC
        public PublicIPChecker(IAppSettings appSettings, IHttpRequestService httpRequestService)
        {
            _appSettings = appSettings;
            _httpRequestService = httpRequestService;
        }

        public void StartIPCheker()
        {
            if (!_checkerStarted)
            {
                lock (_checkerLock)
                {
                    if (!_checkerStarted)
                    {
                        _InitIPChekerProcess();
                        _checkerStarted = true;
                    }
                }
            }
        }

        public void StopIPCheker()
        {
            if (_CheckerCancelToken != null && _CheckerCancelToken.CanBeCanceled)
            {
                _TokenSource.Cancel();
            }
            if (runnerThread != null)
            {
                runnerThread.Abort();
                _checkerStarted = false;
            }
        }

        private void _InitIPChekerProcess()
        {
            _TokenSource = new CancellationTokenSource();
            _CheckerCancelToken = _TokenSource.Token;
            runnerThread = new Thread(async () => {
                Thread.CurrentThread.IsBackground = true;

                while (_CheckerCancelToken != null && !_CheckerCancelToken.IsCancellationRequested)
                {
                    // Call Ping logic
                    try { 
                        await _CheckPublicIP();
                    }
                    catch(Exception ex) { 
                    }
                    // Cooldown
                    Thread.Sleep(_timeoutDelayMs);
                }
            });

            runnerThread.Start();
        }

        private static char[] charsToTrim = { ' ', '\"', '\n' };
        private async Task _CheckPublicIP()
        {
            //var extIp = _httpRequestService.GetResponse(AppConstants.EXTERNAL_IP_URL, 5000);

            HttpResponseMessage response = await client.GetAsync(AppConstants.EXTERNAL_IP_URL);
            response.EnsureSuccessStatusCode();

            var data = await response.Content.ReadAsStringAsync();
            var extIp = data.Trim(charsToTrim);
            Debug.WriteLine($"[PUBLIC IP] IP: {extIp}");
            if (!extIp.Equals(_appSettings.DeviceIp))
            {
                _log.Info($"[PUBLIC IP] Public IP changed");
                Debug.WriteLine($"[PUBLIC IP] Public IP changed");
                _appSettings.DeviceIp = extIp;
                IpChangedEvent?.Invoke(extIp);
            }
        }
    }
}
