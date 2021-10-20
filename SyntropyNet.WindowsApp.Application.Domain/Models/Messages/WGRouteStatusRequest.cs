using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyntropyNet.WindowsApp.Application.Domain.Models.Messages
{
    public class WGRouteStatusRequest : BaseMessage
    {
        public WGRouteStatusRequest()
        {
            Id = $"Id{DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond}";
            Type = "WG_ROUTE_STATUS";
            Data = new List<WGRouteStatusData>();
        }

        public List<WGRouteStatusData> Data { get; set; }

        /// <summary>
        /// Adds route status data. Will check if the ConnectionGroupId is unique, if not, will add Statuses collection to an existing data with such ConnectionGroupId.
        /// </summary>
        public void AddRouteStatusData(WGRouteStatusData data) {
            if (this.Data == null) {
                this.Data = new List<WGRouteStatusData> {
                    data
                };
            } else {
                WGRouteStatusData existingData = this.Data.FirstOrDefault(x => x.ConnectionGroupId == data.ConnectionGroupId);

                if (existingData != null) {
                    if (existingData.Statuses == null) {
                        existingData.Statuses = data.Statuses; 
                    } else {
                        existingData.Statuses.AddRange(data.Statuses);
                    }
                } else {
                    if (this.Data == null) {
                        this.Data = new List<WGRouteStatusData>();
                    }

                    this.Data.Add(data);
                }
            }
        }
    }

    public class WGRouteStatusData
    {
        public int ConnectionGroupId { get; set; }
        public int ConnectionId { get; set; }
        public List<WGRouteStatus> Statuses { get; set; }
    }

    public class WGRouteStatus
    {
        public string Status { get; set; }
        public string Ip { get; set; }
        public string Msg { get; set; }
    }
}
