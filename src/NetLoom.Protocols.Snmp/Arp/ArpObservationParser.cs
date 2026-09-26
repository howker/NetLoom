using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using NetLoom.Application.Observations;
using NetLoom.Application.Observations.Arp;
using NetLoom.Application.Snmp;
using NetLoom.Domain.Observations.Arp;
using NetLoom.Protocols.Snmp;

namespace NetLoom.Protocols.Snmp.Arp
{
    public sealed class ArpObservationParser
        : IArpObservationParser
    {
        private const string ModernPhysAddress =
            "1.3.6.1.2.1.4.35.1.4";

        private const string ModernType =
            "1.3.6.1.2.1.4.35.1.6";

        private const string ModernState =
            "1.3.6.1.2.1.4.35.1.7";

        private const string LegacyPhysAddress =
            "1.3.6.1.2.1.4.22.1.2";

        private const string LegacyType =
            "1.3.6.1.2.1.4.22.1.4";

        public ArpObservation Parse(
            SnmpObservation snmpObservation)
        {
            if (snmpObservation == null)
            {
                throw new ArgumentNullException(
                    nameof(snmpObservation));
            }

            var entries =
                new Dictionary<ArpKey, Builder>();

            foreach (var variable in
                snmpObservation.Variables)
            {
                ApplyModern(
                    variable,
                    ModernPhysAddress,
                    entries,
                    (builder, value) =>
                        builder.PhysicalAddress =
                            ReadPhysicalAddress(value));

                ApplyModern(
                    variable,
                    ModernType,
                    entries,
                    (builder, value) =>
                        builder.Type =
                            ParseInt(value.DisplayValue));

                ApplyModern(
                    variable,
                    ModernState,
                    entries,
                    (builder, value) =>
                        builder.State =
                            ParseInt(value.DisplayValue));

                ApplyLegacy(
                    variable,
                    LegacyPhysAddress,
                    entries,
                    (builder, value) =>
                        builder.PhysicalAddress =
                            ReadPhysicalAddress(value));

                ApplyLegacy(
                    variable,
                    LegacyType,
                    entries,
                    (builder, value) =>
                        builder.Type =
                            ParseInt(value.DisplayValue));
            }

            return new ArpObservation(
                snmpObservation.Observation,
                entries
                    .OrderBy(pair => pair.Key.IfIndex)
                    .ThenBy(pair => pair.Key.IpAddress)
                    .Select(
                        pair => pair.Value.Build(pair.Key))
                    .ToArray());
        }

        private static void ApplyModern(
            SnmpVariable variable,
            string rootOid,
            IDictionary<ArpKey, Builder> entries,
            Action<Builder, SnmpVariable> apply)
        {
            ArpKey key;

            if (!TryParseModernIndex(
                variable.Oid,
                rootOid,
                out key))
            {
                return;
            }

            apply(
                GetBuilder(entries, key),
                variable);
        }

        private static void ApplyLegacy(
            SnmpVariable variable,
            string rootOid,
            IDictionary<ArpKey, Builder> entries,
            Action<Builder, SnmpVariable> apply)
        {
            ArpKey key;

            if (!TryParseLegacyIndex(
                variable.Oid,
                rootOid,
                out key))
            {
                return;
            }

            apply(
                GetBuilder(entries, key),
                variable);
        }

        private static Builder GetBuilder(
            IDictionary<ArpKey, Builder> entries,
            ArpKey key)
        {
            Builder builder;

            if (!entries.TryGetValue(key, out builder))
            {
                builder = new Builder();
                entries.Add(key, builder);
            }

            return builder;
        }

        private static bool TryParseModernIndex(
            string oid,
            string rootOid,
            out ArpKey key)
        {
            key = default(ArpKey);

            int[] parts;

            if (!TryGetSuffix(
                oid,
                rootOid,
                out parts) ||
                parts.Length < 4)
            {
                return false;
            }

            var ifIndex = parts[0];
            var addressType = parts[1];
            var length = parts[2];

            if (ifIndex < 1 ||
                length < 1 ||
                parts.Length != length + 3)
            {
                return false;
            }

            byte[] addressBytes;

            if (!TryGetBytes(
                parts,
                3,
                length,
                out addressBytes))
            {
                return false;
            }

            string ipAddress;

            if (addressType == 1 &&
                addressBytes.Length == 4)
            {
                ipAddress =
                    new IPAddress(addressBytes).ToString();
            }
            else if (addressType == 2 &&
                     addressBytes.Length == 16)
            {
                ipAddress =
                    new IPAddress(addressBytes).ToString();
            }
            else
            {
                return false;
            }

            key = new ArpKey(
                ifIndex,
                addressType,
                ipAddress,
                ArpTableKind.IpNetToPhysical);

            return true;
        }

        private static bool TryParseLegacyIndex(
            string oid,
            string rootOid,
            out ArpKey key)
        {
            key = default(ArpKey);

            int[] parts;

            if (!TryGetSuffix(
                oid,
                rootOid,
                out parts) ||
                parts.Length != 5 ||
                parts[0] < 1)
            {
                return false;
            }

            byte[] addressBytes;

            if (!TryGetBytes(
                parts,
                1,
                4,
                out addressBytes))
            {
                return false;
            }

            key = new ArpKey(
                parts[0],
                1,
                new IPAddress(addressBytes).ToString(),
                ArpTableKind.IpNetToMedia);

            return true;
        }

        private static bool TryGetSuffix(
            string oid,
            string rootOid,
            out int[] parts)
        {
            parts = null;

            if (string.IsNullOrWhiteSpace(oid))
            {
                return false;
            }

            var normalized = oid.TrimStart('.');
            var prefix = rootOid + ".";

            if (!normalized.StartsWith(
                prefix,
                StringComparison.Ordinal))
            {
                return false;
            }

            var tokens =
                normalized
                    .Substring(prefix.Length)
                    .Split('.');

            var result = new int[tokens.Length];

            for (var i = 0; i < tokens.Length; i++)
            {
                if (!int.TryParse(
                    tokens[i],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out result[i]) ||
                    result[i] < 0)
                {
                    return false;
                }
            }

            parts = result;
            return true;
        }

        private static bool TryGetBytes(
            int[] values,
            int offset,
            int count,
            out byte[] result)
        {
            result = new byte[count];

            for (var i = 0; i < count; i++)
            {
                var value = values[offset + i];

                if (value < 0 || value > 255)
                {
                    result = null;
                    return false;
                }

                result[i] = (byte)value;
            }

            return true;
        }

        private static string ReadPhysicalAddress(
            SnmpVariable variable)
        {
            byte[] payload;

            if (SnmpBinaryValue.TryReadPayload(
                variable,
                out payload) &&
                payload.Length > 0)
            {
                return SnmpBinaryValue.FormatColonHex(
                    payload);
            }

            return NormalizeDisplayedAddress(
                variable.DisplayValue);
        }

        private static string NormalizeDisplayedAddress(
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var hex = new List<char>();

            foreach (var character in value)
            {
                if ((character >= '0' &&
                     character <= '9') ||
                    (character >= 'A' &&
                     character <= 'F') ||
                    (character >= 'a' &&
                     character <= 'f'))
                {
                    hex.Add(
                        char.ToUpperInvariant(character));
                }
            }

            if (hex.Count < 2 ||
                hex.Count % 2 != 0)
            {
                return value.Trim();
            }

            var bytes =
                new byte[hex.Count / 2];

            for (var i = 0; i < bytes.Length; i++)
            {
                byte parsed;

                if (!byte.TryParse(
                    new string(
                        new[]
                        {
                            hex[i * 2],
                            hex[i * 2 + 1]
                        }),
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture,
                    out parsed))
                {
                    return value.Trim();
                }

                bytes[i] = parsed;
            }

            return SnmpBinaryValue.FormatColonHex(
                bytes);
        }

        private static int? ParseInt(
            string value)
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

        private struct ArpKey
            : IEquatable<ArpKey>
        {
            public ArpKey(
                int ifIndex,
                int addressType,
                string ipAddress,
                ArpTableKind tableKind)
            {
                IfIndex = ifIndex;
                AddressType = addressType;
                IpAddress = ipAddress;
                TableKind = tableKind;
            }

            public int IfIndex { get; }

            public int AddressType { get; }

            public string IpAddress { get; }

            public ArpTableKind TableKind { get; }

            public bool Equals(ArpKey other)
            {
                return
                    IfIndex == other.IfIndex &&
                    AddressType == other.AddressType &&
                    TableKind == other.TableKind &&
                    string.Equals(
                        IpAddress,
                        other.IpAddress,
                        StringComparison.OrdinalIgnoreCase);
            }

            public override bool Equals(object obj)
            {
                return obj is ArpKey &&
                    Equals((ArpKey)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = IfIndex;
                    hash = (hash * 397) ^ AddressType;
                    hash = (hash * 397) ^ (int)TableKind;
                    hash = (hash * 397) ^
                        StringComparer.OrdinalIgnoreCase
                            .GetHashCode(IpAddress);

                    return hash;
                }
            }
        }

        private sealed class Builder
        {
            public string PhysicalAddress { get; set; }

            public int? Type { get; set; }

            public int? State { get; set; }

            public ArpEntry Build(ArpKey key)
            {
                return new ArpEntry(
                    key.IfIndex,
                    key.AddressType,
                    key.IpAddress,
                    PhysicalAddress,
                    Type,
                    State,
                    key.TableKind);
            }
        }
    }
}
