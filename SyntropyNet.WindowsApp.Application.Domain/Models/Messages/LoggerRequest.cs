using System;

namespace SyntropyNet.WindowsApp.Application.Domain.Models.Messages
{
    public class LoggerRequest : BaseMessage
    {
        public LoggerRequest()
        {
            Id = $"Id{DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond}";
            Type = "LOGGER";
        }

        public LoggerRequestData Data { get; set; }
    }

    public class LoggerRequestData
    {
        public string Severity { get; set; }
        public string Message { get; set; }
        public LoggerRequestMetadata Metadata { get; set; }
    }

    public class LoggerRequestMetadata
    {
        public string DeviceId { get; set; }
        public string DeviceName { get; set; }
        public string DevicePublicIpv4 { get; set; }
        public int ConnectionId { get; set; }
    }
}
