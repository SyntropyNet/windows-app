using System;

namespace SyntropyNet.WindowsApp.Application.Domain.Events {
    public class RerouteEventArgs : EventArgs {
        public int ConnectionId { get; set; }
    }
}
