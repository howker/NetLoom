using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using NetLoom.Application.Snmp;
using NetLoom.Domain.Access;

namespace NetLoom.Application.Discovery
{
    public enum SnmpProfileCheckKind
    {
        Availability, System, IfMib, LldpMib, BridgeMib, QBridgeMib
    }

    public enum SnmpProfileCheckStatus
    {
        Ok, Partial, Absent, Failed, NotChecked
    }

    public sealed class SnmpProfileCheckItem
    {
        public SnmpProfileCheckItem(SnmpProfileCheckKind kind, SnmpProfileCheckStatus status,
            int? count = null, long? milliseconds = null, SnmpTransportFailure? failure = null)
        {
            Kind = kind;
            Status = status;
            Count = count;
            Milliseconds = milliseconds;
            Failure = failure;
        }

        public SnmpProfileCheckKind Kind { get; }
        public SnmpProfileCheckStatus Status { get; }
        public int? Count { get; }
        public long? Milliseconds { get; }
        public SnmpTransportFailure? Failure { get; }
    }

    public sealed class SnmpProfileCheckReport
    {
        public SnmpProfileCheckReport(IEnumerable<SnmpProfileCheckItem> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            Items = Array.AsReadOnly(items.ToArray());
        }

        public IReadOnlyList<SnmpProfileCheckItem> Items { get; }
    }

    public sealed class SnmpProfileCheckRequest
    {
        public SnmpProfileCheckRequest(IPAddress address, int port, SnmpVersion version,
            SnmpCredentials credentials, int timeoutMilliseconds, int retryCount,
            int maxRepetitions, int icmpTimeoutMilliseconds = 500)
        {
            if (address == null) throw new ArgumentNullException(nameof(address));
            if (port < 1 || port > 65535) throw new ArgumentOutOfRangeException(nameof(port));
            if (!Enum.IsDefined(typeof(SnmpVersion), version)) throw new ArgumentOutOfRangeException(nameof(version));
            if (credentials == null) throw new ArgumentNullException(nameof(credentials));
            if (timeoutMilliseconds < 1) throw new ArgumentOutOfRangeException(nameof(timeoutMilliseconds));
            if (retryCount < 0) throw new ArgumentOutOfRangeException(nameof(retryCount));
            if (maxRepetitions < 1) throw new ArgumentOutOfRangeException(nameof(maxRepetitions));
            if (icmpTimeoutMilliseconds < 1) throw new ArgumentOutOfRangeException(nameof(icmpTimeoutMilliseconds));
            Address = address;
            Port = port;
            Version = version;
            Credentials = credentials;
            TimeoutMilliseconds = timeoutMilliseconds;
            RetryCount = retryCount;
            MaxRepetitions = maxRepetitions;
            IcmpTimeoutMilliseconds = icmpTimeoutMilliseconds;
        }

        public IPAddress Address { get; }
        public int Port { get; }
        public SnmpVersion Version { get; }
        public SnmpCredentials Credentials { get; }
        public int TimeoutMilliseconds { get; }
        public int RetryCount { get; }
        public int MaxRepetitions { get; }
        public int IcmpTimeoutMilliseconds { get; }
    }

    public sealed class SnmpProfileChecker
    {
        private const string SysName = "1.3.6.1.2.1.1.5.0";
        private const string SysObjectId = "1.3.6.1.2.1.1.2.0";
        private const string IfIndex = "1.3.6.1.2.1.2.2.1.1";
        private const string LldpRemSysName = "1.0.8802.1.1.2.1.4.1.1.9";
        private const string LldpLocChassisId = "1.0.8802.1.1.2.1.3.2.0";
        private const string Dot1dBaseNumPorts = "1.3.6.1.2.1.17.1.2.0";
        private const string Dot1qNumVlans = "1.3.6.1.2.1.17.7.1.1.4.0";
        private const string Dot1qVlanStaticName = "1.3.6.1.2.1.17.7.1.4.3.1.1";
        private readonly ISnmpTransport _transport;
        private readonly INetworkDiscoveryProbe _probe;

        public SnmpProfileChecker(ISnmpTransport transport, INetworkDiscoveryProbe probe)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _probe = probe ?? throw new ArgumentNullException(nameof(probe));
        }

        public SnmpProfileCheckReport Check(SnmpProfileCheckRequest request, CancellationToken token)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            token.ThrowIfCancellationRequested();
            var items = new List<SnmpProfileCheckItem>();
            var watch = Stopwatch.StartNew();
            var reachable = _probe.IsIcmpReachable(request.Address, request.IcmpTimeoutMilliseconds, token);
            watch.Stop();
            token.ThrowIfCancellationRequested();
            items.Add(new SnmpProfileCheckItem(SnmpProfileCheckKind.Availability,
                reachable ? SnmpProfileCheckStatus.Ok : SnmpProfileCheckStatus.Failed,
                milliseconds: watch.ElapsedMilliseconds));

            var system = CheckItem(SnmpProfileCheckKind.System, () =>
            {
                var values = Get(request, token, SysName, SysObjectId);
                return new SnmpProfileCheckItem(SnmpProfileCheckKind.System,
                    PresenceStatus(HasValue(values, SysName), HasValue(values, SysObjectId)));
            });
            items.Add(system);
            if (system.Status == SnmpProfileCheckStatus.Failed)
            {
                foreach (var kind in new[] { SnmpProfileCheckKind.IfMib, SnmpProfileCheckKind.LldpMib,
                    SnmpProfileCheckKind.BridgeMib, SnmpProfileCheckKind.QBridgeMib })
                    items.Add(new SnmpProfileCheckItem(kind, SnmpProfileCheckStatus.NotChecked));
                return new SnmpProfileCheckReport(items);
            }

            items.Add(CheckItem(SnmpProfileCheckKind.IfMib, () =>
            {
                var count = Walk(request, token, IfIndex).Count;
                return new SnmpProfileCheckItem(SnmpProfileCheckKind.IfMib,
                    count > 0 ? SnmpProfileCheckStatus.Ok : SnmpProfileCheckStatus.Absent, count);
            }));
            items.Add(CheckItem(SnmpProfileCheckKind.LldpMib, () =>
            {
                var count = Walk(request, token, LldpRemSysName).Count;
                var supported = count > 0 || HasValue(Get(request, token, LldpLocChassisId), LldpLocChassisId);
                return new SnmpProfileCheckItem(SnmpProfileCheckKind.LldpMib,
                    supported ? SnmpProfileCheckStatus.Ok : SnmpProfileCheckStatus.Absent, count);
            }));
            items.Add(CheckItem(SnmpProfileCheckKind.BridgeMib, () =>
                new SnmpProfileCheckItem(SnmpProfileCheckKind.BridgeMib,
                    HasValue(Get(request, token, Dot1dBaseNumPorts), Dot1dBaseNumPorts)
                        ? SnmpProfileCheckStatus.Ok : SnmpProfileCheckStatus.Absent)));
            items.Add(CheckItem(SnmpProfileCheckKind.QBridgeMib, () =>
            {
                var scalar = HasValue(Get(request, token, Dot1qNumVlans), Dot1qNumVlans);
                var names = Walk(request, token, Dot1qVlanStaticName).Count > 0;
                return new SnmpProfileCheckItem(SnmpProfileCheckKind.QBridgeMib, PresenceStatus(scalar, names));
            }));
            token.ThrowIfCancellationRequested();
            return new SnmpProfileCheckReport(items);
        }

        private static SnmpProfileCheckItem CheckItem(SnmpProfileCheckKind kind, Func<SnmpProfileCheckItem> check)
        {
            try { return check(); }
            catch (SnmpTransportException error)
            {
                // В отчёт попадает только категория ошибки, без сообщения транспорта и секрета.
                return new SnmpProfileCheckItem(kind, SnmpProfileCheckStatus.Failed, failure: error.Failure);
            }
        }

        private IReadOnlyList<SnmpVariable> Get(SnmpProfileCheckRequest request, CancellationToken token,
            params string[] oids)
        {
            token.ThrowIfCancellationRequested();
            var values = _transport.Get(new SnmpGetRequest(request.Address, request.Port, request.Version,
                request.Credentials, oids, request.TimeoutMilliseconds, request.RetryCount));
            token.ThrowIfCancellationRequested();
            return values;
        }

        private IReadOnlyList<SnmpVariable> Walk(SnmpProfileCheckRequest request, CancellationToken token, string oid)
        {
            token.ThrowIfCancellationRequested();
            var values = _transport.Walk(new SnmpWalkRequest(request.Address, request.Port, request.Version,
                request.Credentials, oid, request.TimeoutMilliseconds, request.RetryCount, request.MaxRepetitions));
            token.ThrowIfCancellationRequested();
            return values.Where(value => value.Oid.TrimStart('.').StartsWith(oid + ".", StringComparison.Ordinal)
                && IsPresent(value)).ToArray();
        }

        private static bool HasValue(IEnumerable<SnmpVariable> values, string oid)
        {
            return values.Any(value => value.Oid.TrimStart('.') == oid && IsPresent(value));
        }

        private static bool IsPresent(SnmpVariable value)
        {
            // Теги исключений SNMP: noSuchObject, noSuchInstance, endOfMibView.
            return value.TypeCode != 0x80 && value.TypeCode != 0x81 && value.TypeCode != 0x82
                && !string.IsNullOrWhiteSpace(value.DisplayValue);
        }

        private static SnmpProfileCheckStatus PresenceStatus(bool first, bool second)
        {
            return first && second ? SnmpProfileCheckStatus.Ok
                : first || second ? SnmpProfileCheckStatus.Partial : SnmpProfileCheckStatus.Absent;
        }
    }
}
