using System;

namespace NetLoom.Domain.Observations.Lldp
{
    public sealed class LldpLocalPort
    {
        public LldpLocalPort(
            int localPortNumber,
            int? portIdSubtype,
            string portId,
            string portDescription)
        {
            if (localPortNumber < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(localPortNumber));
            }

            LocalPortNumber = localPortNumber;
            PortIdSubtype = portIdSubtype;
            PortId = portId;
            PortDescription = portDescription;
        }

        public int LocalPortNumber { get; }

        public int? PortIdSubtype { get; }

        public string PortId { get; }

        public string PortDescription { get; }
    }
}
