using System;

namespace NetLoom.Domain.Observations.Lldp
{
    public sealed class LldpRemoteNeighbor
    {
        public LldpRemoteNeighbor(
            long timeMark,
            int localPortNumber,
            int remoteIndex,
            int? chassisIdSubtype,
            string chassisId,
            int? portIdSubtype,
            string portId,
            string portDescription,
            string systemName,
            string systemDescription,
            string systemCapabilitiesSupported,
            string systemCapabilitiesEnabled,
            LldpLocalPort localPort)
        {
            if (timeMark < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(timeMark));
            }

            if (localPortNumber < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(localPortNumber));
            }

            if (remoteIndex < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(remoteIndex));
            }

            TimeMark = timeMark;
            LocalPortNumber = localPortNumber;
            RemoteIndex = remoteIndex;
            ChassisIdSubtype = chassisIdSubtype;
            ChassisId = chassisId;
            PortIdSubtype = portIdSubtype;
            PortId = portId;
            PortDescription = portDescription;
            SystemName = systemName;
            SystemDescription = systemDescription;
            SystemCapabilitiesSupported =
                systemCapabilitiesSupported;
            SystemCapabilitiesEnabled =
                systemCapabilitiesEnabled;
            LocalPort = localPort;
        }

        public long TimeMark { get; }

        public int LocalPortNumber { get; }

        public int RemoteIndex { get; }

        public int? ChassisIdSubtype { get; }

        public string ChassisId { get; }

        public int? PortIdSubtype { get; }

        public string PortId { get; }

        public string PortDescription { get; }

        public string SystemName { get; }

        public string SystemDescription { get; }

        public string SystemCapabilitiesSupported { get; }

        public string SystemCapabilitiesEnabled { get; }

        public LldpLocalPort LocalPort { get; }
    }
}
