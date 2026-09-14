using System;
using System.Collections.Generic;
using System.Linq;
using NetLoom.Application.Monitoring.Interfaces;
using NetLoom.Application.Snmp;

namespace NetLoom.Protocols.Snmp.Interfaces
{
    public sealed class SnmpInterfaceStatusCollector :
        IInterfaceCollector
    {
        private const string IfAdminStatus =
            "1.3.6.1.2.1.2.2.1.7";

        private const string IfOperStatus =
            "1.3.6.1.2.1.2.2.1.8";

        private const string IfInDiscards =
            "1.3.6.1.2.1.2.2.1.13";

        private const string IfInErrors =
            "1.3.6.1.2.1.2.2.1.14";

        private const string IfOutDiscards =
            "1.3.6.1.2.1.2.2.1.19";

        private const string IfOutErrors =
            "1.3.6.1.2.1.2.2.1.20";

        private const string IfCounterDiscontinuityTime =
            "1.3.6.1.2.1.31.1.1.1.19";

        private readonly ISnmpTransport _transport;
        private readonly Func<DateTime> _utcNow;

        public SnmpInterfaceStatusCollector(
            ISnmpTransport transport,
            Func<DateTime> utcNow = null)
        {
            _transport =
                transport ??
                throw new ArgumentNullException(
                    nameof(transport));

            _utcNow =
                utcNow ??
                (() => DateTime.UtcNow);
        }

        public IReadOnlyList<InterfaceMonitoringSnapshot> Collect(
            InterfaceCollectionRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(
                    nameof(request));
            }

            var builders =
                new Dictionary<int, Builder>();

            Apply(
                builders,
                Walk(
                    request,
                    IfAdminStatus),
                IfAdminStatus,
                true);

            Apply(
                builders,
                Walk(
                    request,
                    IfOperStatus),
                IfOperStatus,
                false);

            ApplyCounter(
                builders,
                Walk(
                    request,
                    IfInDiscards),
                IfInDiscards,
                (builder, value) =>
                    builder.InDiscards = value);

            ApplyCounter(
                builders,
                Walk(
                    request,
                    IfInErrors),
                IfInErrors,
                (builder, value) =>
                    builder.InErrors = value);

            ApplyCounter(
                builders,
                Walk(
                    request,
                    IfOutDiscards),
                IfOutDiscards,
                (builder, value) =>
                    builder.OutDiscards = value);

            ApplyCounter(
                builders,
                Walk(
                    request,
                    IfOutErrors),
                IfOutErrors,
                (builder, value) =>
                    builder.OutErrors = value);

            ApplyCounter(
                builders,
                Walk(
                    request,
                    IfCounterDiscontinuityTime),
                IfCounterDiscontinuityTime,
                (builder, value) =>
                    builder.CounterDiscontinuityTimeTicks =
                        value);

            var capturedUtc =
                _utcNow();

            if (capturedUtc.Kind !=
                DateTimeKind.Utc)
            {
                throw new InvalidOperationException(
                    "Interface collector clock must return UTC.");
            }

            return builders
                .OrderBy(
                    pair => pair.Key)
                .Select(
                    pair =>
                        new InterfaceMonitoringSnapshot(
                            request.DeviceId,
                            pair.Key,
                            pair.Value.AdminStatus,
                            pair.Value.OperStatus,
                            capturedUtc,
                            pair.Value.InErrors,
                            pair.Value.OutErrors,
                            pair.Value.InDiscards,
                            pair.Value.OutDiscards,
                            pair.Value.CounterDiscontinuityTimeTicks))
                .ToArray();
        }

        private IReadOnlyList<SnmpVariable> Walk(
            InterfaceCollectionRequest request,
            string oid)
        {
            return _transport.Walk(
                new SnmpWalkRequest(
                    request.Address,
                    request.Port,
                    request.Version,
                    request.Credentials,
                    oid,
                    request.TimeoutMilliseconds,
                    request.RetryCount,
                    request.MaxRepetitions));
        }

        private static void Apply(
            IDictionary<int, Builder> builders,
            IEnumerable<SnmpVariable> variables,
            string columnOid,
            bool admin)
        {
            foreach (var variable in variables)
            {
                var ifIndex =
                    SnmpInterfaceStatusParser.ParseIfIndex(
                        variable.Oid,
                        columnOid);

                if (!ifIndex.HasValue)
                {
                    continue;
                }

                Builder builder;

                if (!builders.TryGetValue(
                    ifIndex.Value,
                    out builder))
                {
                    builder =
                        new Builder();

                    builders.Add(
                        ifIndex.Value,
                        builder);
                }

                var status =
                    SnmpInterfaceStatusParser.ParseStatus(
                        variable.DisplayValue);

                if (admin)
                {
                    builder.AdminStatus =
                        status;
                }
                else
                {
                    builder.OperStatus =
                        status;
                }
            }
        }

        private static void ApplyCounter(
            IDictionary<int, Builder> builders,
            IEnumerable<SnmpVariable> variables,
            string columnOid,
            Action<Builder, uint?> apply)
        {
            foreach (var variable in variables)
            {
                var ifIndex =
                    SnmpInterfaceStatusParser.ParseIfIndex(
                        variable.Oid,
                        columnOid);

                if (!ifIndex.HasValue)
                {
                    continue;
                }

                Builder builder;

                if (!builders.TryGetValue(
                    ifIndex.Value,
                    out builder))
                {
                    builder =
                        new Builder();

                    builders.Add(
                        ifIndex.Value,
                        builder);
                }

                apply(
                    builder,
                    SnmpInterfaceStatusParser.ParseCounter32(
                        variable.DisplayValue));
            }
        }

        private sealed class Builder
        {
            public int? AdminStatus { get; set; }

            public int? OperStatus { get; set; }

            public uint? InErrors { get; set; }

            public uint? OutErrors { get; set; }

            public uint? InDiscards { get; set; }

            public uint? OutDiscards { get; set; }

            public uint? CounterDiscontinuityTimeTicks
            {
                get;
                set;
            }
        }
    }
}
