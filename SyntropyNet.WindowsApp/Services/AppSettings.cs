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
            }
            catch(Exception ex)
            {
                // ToDo :: log exception if internet is not available
            }
            _deviceId = productId ?? $"{System.Environment.MachineName}-{extIp}";
        }

        public string ControllerUrl => ConfigurationManager.AppSettings["ControllerUrl"];
        public string AgentVersion => ConfigurationManager.AppSettings["AgentVersion"];


        private string _deviceId = string.Empty;
        public string DeviceId 
        {
            get
            {
                return _deviceId;
            }
        }

        public bool ModalWindowActivated { get; set; }
    }
}
