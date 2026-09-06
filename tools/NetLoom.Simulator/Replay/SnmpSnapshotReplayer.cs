using System;
using NetLoom.Domain.Observations;
using NetLoom.Protocols.Snmp.Arp;
using NetLoom.Protocols.Snmp.Cdp;
using NetLoom.Protocols.Snmp.Fdb;
using NetLoom.Protocols.Snmp.Lldp;

namespace NetLoom.Simulator.Replay
{
    public sealed class SnmpSnapshotReplayer
    {
        private readonly RawSnmpSnapshotCodec
            _codec;

        public SnmpSnapshotReplayer()
            : this(new RawSnmpSnapshotCodec())
        {
        }

        public SnmpSnapshotReplayer(
            RawSnmpSnapshotCodec codec)
        {
            _codec =
                codec ??
                throw new ArgumentNullException(
                    nameof(codec));
        }

        public SnmpSnapshotReplayResult Replay(
            RawSnmpSnapshot snapshot)
        {
            var raw =
                _codec.BuildObservation(
                    snapshot);

            switch (raw.Observation.Kind)
            {
                case ObservationKind.Lldp:
                    var lldp =
                        new LldpObservationParser()
                            .Parse(raw);

                    return new SnmpSnapshotReplayResult(
                        raw,
                        lldp,
                        lldp.Neighbors.Count);

                case ObservationKind.Cdp:
                    var cdp =
                        new CdpObservationParser()
                            .Parse(raw);

                    return new SnmpSnapshotReplayResult(
                        raw,
                        cdp,
                        cdp.Neighbors.Count);

                case ObservationKind.Fdb:
                    var fdb =
                        new FdbObservationParser()
                            .Parse(raw);

                    return new SnmpSnapshotReplayResult(
                        raw,
                        fdb,
                        fdb.Entries.Count);

                case ObservationKind.Arp:
                    var arp =
                        new ArpObservationParser()
                            .Parse(raw);

                    return new SnmpSnapshotReplayResult(
                        raw,
                        arp,
                        arp.Entries.Count);

                default:
                    throw new NotSupportedException(
                        "Observation kind " +
                        raw.Observation.Kind +
                        " has no production raw parser.");
            }
        }
    }
}
