using SyntropyNet.WindowsApp.Application.Domain.Constants;

namespace SyntropyNet.WindowsApp.Application.Domain.Models.Messages {
    public class SetSettingsRequest : BaseMessage {
        public SetSettingsRequest() {
            Type = MessageTypes.SetSettings;
            Data = new ReroutingthresholdData();
        }

        public ReroutingthresholdData Data { get; set; }
    }

    public class ReroutingthresholdData {
        public ReroutingthresholdData() {
            ReroutingThreshold = new Reroutingthreshold();
        }

        public Reroutingthreshold ReroutingThreshold { get; set; }
    }

    public class Reroutingthreshold {
        public float LatencyDiff { get; set; }
        public float LatencyRatio { get; set; }
    }
}
