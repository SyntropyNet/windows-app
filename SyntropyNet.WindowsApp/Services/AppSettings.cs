using log4net;
using SyntropyNet.WindowsApp.Application.Constants;
using SyntropyNet.WindowsApp.Application.Contracts;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;

namespace SyntropyNet.WindowsApp.Services
{
    public class AppSettings: IAppSettings
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(AppSettings));

        public AppSettings(IHttpRequestService httpRequestService)
        {
            const string queryString = "SELECT SerialNumber FROM Win32_OperatingSystem";

            string productId = (from ManagementObject managementObject in new ManagementObjectSearcher(queryString).Get()
                                from PropertyData propertyData in managementObject.Properties
                                where propertyData.Name == "SerialNumber"
                                select (string)propertyData.Value).FirstOrDefault();
            var extIp = string.Empty;
            try
            {
                extIp = httpRequestService.GetResponse(AppConstants.EXTERNAL_IP_URL);
                DeviceIp = extIp;
            }
            catch(Exception ex)
            {
                log.Error("Internet is not available", ex);
            }
            _deviceId = productId ?? $"{System.Environment.MachineName}-{extIp}";
            _deviceName = System.Environment.MachineName;
        }

        public string ControllerUrl => ConfigurationManager.AppSettings["ControllerUrl"];
        public string AgentVersion => ConfigurationManager.AppSettings["AgentVersion"];
        public string DeviceIp { get; set; }

        private string _deviceId = string.Empty;
        public string DeviceId 
        {
            get
            {
                return _deviceId;
            }
        }

        private string _deviceName = string.Empty;
        public string DeviceName
        {
            get
            {
                return _deviceName;
            }
        }

        public bool ModalWindowActivated { get; set; }
    }
}
