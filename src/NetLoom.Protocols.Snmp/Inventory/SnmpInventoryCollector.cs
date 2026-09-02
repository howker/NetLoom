using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NetLoom.Application.Inventory;
using NetLoom.Application.Snmp;

namespace NetLoom.Protocols.Snmp.Inventory
{
    public sealed class SnmpInventoryCollector
    {
        private const string SysDescr =
            "1.3.6.1.2.1.1.1.0";

        private const string SysObjectId =
            "1.3.6.1.2.1.1.2.0";

        private const string SysUpTime =
            "1.3.6.1.2.1.1.3.0";

        private const string SysContact =
            "1.3.6.1.2.1.1.4.0";

        private const string SysName =
            "1.3.6.1.2.1.1.5.0";

        private const string SysLocation =
            "1.3.6.1.2.1.1.6.0";

        private const string IfIndex =
            "1.3.6.1.2.1.2.2.1.1";

        private const string IfDescr =
            "1.3.6.1.2.1.2.2.1.2";

        private const string IfType =
            "1.3.6.1.2.1.2.2.1.3";

        private const string IfPhysAddress =
            "1.3.6.1.2.1.2.2.1.6";

        private const string IfAdminStatus =
            "1.3.6.1.2.1.2.2.1.7";

        private const string IfOperStatus =
            "1.3.6.1.2.1.2.2.1.8";

        private const string IfName =
            "1.3.6.1.2.1.31.1.1.1.1";

        private const string IfHighSpeed =
            "1.3.6.1.2.1.31.1.1.1.15";

        private const string IfAlias =
            "1.3.6.1.2.1.31.1.1.1.18";

        private readonly ISnmpTransport _transport;

        public SnmpInventoryCollector(ISnmpTransport transport)
        {
            _transport = transport ??
                throw new ArgumentNullException(nameof(transport));
        }

        public InventorySnapshot Collect(
            InventoryCollectionRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var system = _transport.Get(
                new SnmpGetRequest(
                    request.Address,
                    request.Port,
                    request.Version,
                    request.Credentials,
                    new[]
                    {
                        SysDescr,
                        SysObjectId,
                        SysUpTime,
                        SysContact,
                        SysName,
                        SysLocation
                    },
                    request.TimeoutMilliseconds,
                    request.RetryCount));

            var systemValues =
                ToOidDictionary(system);

            var interfaces =
                new Dictionary<int, InterfaceBuilder>();

            Apply(
                request,
                IfIndex,
                interfaces,
                (builder, value) => { });

            Apply(
                request,
                IfDescr,
                interfaces,
                (builder, value) =>
                    builder.Description = value);

            Apply(
                request,
                IfType,
                interfaces,
                (builder, value) =>
                    builder.IfType = ParseInt(value));

            Apply(
                request,
                IfPhysAddress,
                interfaces,
                (builder, value) =>
                    builder.PhysicalAddress = value);

            Apply(
                request,
                IfAdminStatus,
                interfaces,
                (builder, value) =>
                    builder.AdminStatus = ParseInt(value));

            Apply(
                request,
                IfOperStatus,
                interfaces,
                (builder, value) =>
                    builder.OperStatus = ParseInt(value));

            ApplyOptional(
                request,
                IfName,
                interfaces,
                (builder, value) =>
                    builder.Name = value);

            ApplyOptional(
                request,
                IfHighSpeed,
                interfaces,
                (builder, value) =>
                    builder.HighSpeedMbps = ParseLong(value));

            ApplyOptional(
                request,
                IfAlias,
                interfaces,
                (builder, value) =>
                    builder.Alias = value);

            return new InventorySnapshot(
                request.Address,
                GetValue(systemValues, SysName),
                GetValue(systemValues, SysDescr),
                GetValue(systemValues, SysObjectId),
                GetValue(systemValues, SysContact),
                GetValue(systemValues, SysLocation),
                GetValue(systemValues, SysUpTime),
                interfaces.Values
                    .OrderBy(item => item.IfIndex)
                    .Select(item => item.Build())
                    .ToArray());
        }

        private void ApplyOptional(
            InventoryCollectionRequest request,
            string rootOid,
            IDictionary<int, InterfaceBuilder> interfaces,
            Action<InterfaceBuilder, string> apply)
        {
            try
            {
                Apply(
                    request,
                    rootOid,
                    interfaces,
                    apply);
            }
            catch (SnmpTransportException exception)
            {
                if (exception.Failure !=
                    SnmpTransportFailure.Protocol)
                {
                    throw;
                }
            }
        }
        private void Apply(
            InventoryCollectionRequest request,
            string rootOid,
            IDictionary<int, InterfaceBuilder> interfaces,
            Action<InterfaceBuilder, string> apply)
        {
            var variables = _transport.Walk(
                new SnmpWalkRequest(
                    request.Address,
                    request.Port,
                    request.Version,
                    request.Credentials,
                    rootOid,
                    request.TimeoutMilliseconds,
                    request.RetryCount,
                    request.MaxRepetitions));

            foreach (var variable in variables)
            {
                var ifIndex = TryGetIndex(
                    rootOid,
                    variable.Oid);

                if (!ifIndex.HasValue)
                {
                    continue;
                }

                InterfaceBuilder builder;

                if (!interfaces.TryGetValue(
                    ifIndex.Value,
                    out builder))
                {
                    builder =
                        new InterfaceBuilder(ifIndex.Value);

                    interfaces.Add(
                        ifIndex.Value,
                        builder);
                }

                apply(
                    builder,
                    variable.DisplayValue);
            }
        }

        private static IDictionary<string, string>
            ToOidDictionary(
                IEnumerable<SnmpVariable> variables)
        {
            var result =
                new Dictionary<string, string>(
                    StringComparer.Ordinal);

            foreach (var variable in variables)
            {
                result[NormalizeOid(variable.Oid)] =
                    variable.DisplayValue;
            }

            return result;
        }

        private static string GetValue(
            IDictionary<string, string> values,
            string oid)
        {
            string value;

            return values.TryGetValue(
                NormalizeOid(oid),
                out value)
                ? value
                : null;
        }

        private static int? TryGetIndex(
            string rootOid,
            string oid)
        {
            var root = NormalizeOid(rootOid);
            var current = NormalizeOid(oid);
            var prefix = root + ".";

            if (!current.StartsWith(
                prefix,
                StringComparison.Ordinal))
            {
                return null;
            }

            int value;

            return int.TryParse(
                current.Substring(prefix.Length),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out value)
                ? value
                : (int?)null;
        }

        private static int? ParseInt(string value)
        {
            int parsed;

            return int.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out parsed)
                ? parsed
                : (int?)null;
        }

        private static long? ParseLong(string value)
        {
            long parsed;

            return long.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out parsed)
                ? parsed
                : (long?)null;
        }

        private static string NormalizeOid(string oid)
        {
            return oid == null
                ? string.Empty
                : oid.Trim().TrimStart('.');
        }

        private sealed class InterfaceBuilder
        {
            public InterfaceBuilder(int ifIndex)
            {
                IfIndex = ifIndex;
            }

            public int IfIndex { get; }

            public string Description { get; set; }

            public string Name { get; set; }

            public string Alias { get; set; }

            public int? IfType { get; set; }

            public string PhysicalAddress { get; set; }

            public int? AdminStatus { get; set; }

            public int? OperStatus { get; set; }

            public long? HighSpeedMbps { get; set; }

            public InventoryInterface Build()
            {
                return new InventoryInterface(
                    IfIndex,
                    Description,
                    Name,
                    Alias,
                    IfType,
                    PhysicalAddress,
                    AdminStatus,
                    OperStatus,
                    HighSpeedMbps);
            }
        }
    }
}
