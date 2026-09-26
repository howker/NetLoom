using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using NetLoom.Application.Snmp;

namespace NetLoom.Protocols.Snmp
{
    internal static class SnmpBinaryValue
    {
        public static bool TryReadPayload(
            SnmpVariable variable,
            out byte[] payload)
        {
            if (variable == null)
            {
                payload = null;
                return false;
            }

            return TryReadBerPayload(
                variable.GetEncodedValue(),
                out payload);
        }

        public static bool TryReadBerPayload(
            byte[] encoded,
            out byte[] payload)
        {
            payload = null;

            if (encoded == null || encoded.Length < 2)
            {
                return false;
            }

            var position = 1;
            var firstLength = encoded[position++];
            int length;

            if ((firstLength & 0x80) == 0)
            {
                length = firstLength;
            }
            else
            {
                var lengthBytes =
                    firstLength & 0x7F;

                if (lengthBytes < 1 ||
                    lengthBytes > 4 ||
                    encoded.Length <
                        position + lengthBytes)
                {
                    return false;
                }

                length = 0;

                for (var i = 0; i < lengthBytes; i++)
                {
                    length =
                        (length << 8) |
                        encoded[position++];
                }
            }

            if (length < 0 ||
                encoded.Length != position + length)
            {
                return false;
            }

            payload = new byte[length];

            Buffer.BlockCopy(
                encoded,
                position,
                payload,
                0,
                length);

            return true;
        }

        public static string ReadLldpChassisId(
            SnmpVariable variable,
            int? subtype)
        {
            if (!subtype.HasValue)
            {
                return ReadTextOrHex(variable);
            }

            switch (subtype.Value)
            {
                case 4:
                    return ReadMacAddress(variable);

                case 5:
                    return ReadLldpNetworkAddress(variable);

                default:
                    return ReadTextOrHex(variable);
            }
        }

        public static string ReadLldpPortId(
            SnmpVariable variable,
            int? subtype)
        {
            if (!subtype.HasValue)
            {
                return ReadTextOrHex(variable);
            }

            switch (subtype.Value)
            {
                case 3:
                    return ReadMacAddress(variable);

                case 4:
                    return ReadLldpNetworkAddress(variable);

                default:
                    return ReadTextOrHex(variable);
            }
        }

        public static string ReadStpBridgeId(
            SnmpVariable variable)
        {
            byte[] payload;

            if (TryReadPayload(
                    variable,
                    out payload) &&
                payload.Length == 8)
            {
                return
                    FormatCompactHex(
                        payload,
                        0,
                        2) +
                    "." +
                    FormatCompactHex(
                        payload,
                        2,
                        6);
            }

            return NormalizeDisplay(variable);
        }

        public static string ReadStpPortId(
            SnmpVariable variable)
        {
            byte[] payload;

            if (TryReadPayload(
                    variable,
                    out payload) &&
                payload.Length == 2)
            {
                return FormatCompactHex(
                    payload,
                    0,
                    payload.Length);
            }

            return NormalizeDisplay(variable);
        }

        public static string ReadCiscoNetworkAddress(
            SnmpVariable variable,
            int? protocol)
        {
            byte[] payload;

            if (!TryReadPayload(
                variable,
                out payload))
            {
                return NormalizeDisplay(variable);
            }

            if (protocol == 1 &&
                payload.Length == 4)
            {
                return new IPAddress(
                    payload).ToString();
            }

            if (protocol == 20 &&
                payload.Length == 16)
            {
                return new IPAddress(
                    payload).ToString();
            }

            return FormatColonHex(payload);
        }

        public static string FormatColonHex(
            byte[] bytes)
        {
            if (bytes == null)
            {
                return null;
            }

            return string.Join(
                ":",
                bytes.Select(
                    value => value.ToString(
                        "X2",
                        CultureInfo.InvariantCulture)));
        }

        private static string ReadMacAddress(
            SnmpVariable variable)
        {
            byte[] payload;

            if (TryReadPayload(
                    variable,
                    out payload) &&
                payload.Length > 0)
            {
                return FormatColonHex(payload);
            }

            return NormalizeDisplay(variable);
        }

        private static string ReadLldpNetworkAddress(
            SnmpVariable variable)
        {
            byte[] payload;

            if (!TryReadPayload(
                    variable,
                    out payload) ||
                payload.Length < 2)
            {
                return NormalizeDisplay(variable);
            }

            var family = payload[0];

            if (family == 1 &&
                payload.Length == 5)
            {
                var address =
                    new byte[4];

                Buffer.BlockCopy(
                    payload,
                    1,
                    address,
                    0,
                    address.Length);

                return new IPAddress(
                    address).ToString();
            }

            if (family == 2 &&
                payload.Length == 17)
            {
                var address =
                    new byte[16];

                Buffer.BlockCopy(
                    payload,
                    1,
                    address,
                    0,
                    address.Length);

                return new IPAddress(
                    address).ToString();
            }

            var remainder =
                new byte[payload.Length - 1];

            Buffer.BlockCopy(
                payload,
                1,
                remainder,
                0,
                remainder.Length);

            return
                "AF" +
                family.ToString(
                    CultureInfo.InvariantCulture) +
                ":" +
                FormatColonHex(remainder);
        }

        private static string ReadTextOrHex(
            SnmpVariable variable)
        {
            byte[] payload;

            if (!TryReadPayload(
                variable,
                out payload))
            {
                return NormalizeDisplay(variable);
            }

            if (payload.Length == 0)
            {
                return null;
            }

            string text;

            try
            {
                text =
                    new UTF8Encoding(
                        false,
                        true)
                        .GetString(payload);
            }
            catch (DecoderFallbackException)
            {
                return FormatColonHex(payload);
            }

            if (text.Any(
                character =>
                    char.IsControl(character)))
            {
                return FormatColonHex(payload);
            }

            return string.IsNullOrWhiteSpace(text)
                ? FormatColonHex(payload)
                : text.Trim();
        }

        private static string NormalizeDisplay(
            SnmpVariable variable)
        {
            if (variable == null ||
                string.IsNullOrWhiteSpace(
                    variable.DisplayValue))
            {
                return null;
            }

            return variable.DisplayValue.Trim();
        }

        private static string FormatCompactHex(
            byte[] bytes,
            int offset,
            int count)
        {
            var builder =
                new StringBuilder(
                    count * 2);

            for (var i = 0; i < count; i++)
            {
                builder.Append(
                    bytes[offset + i]
                        .ToString(
                            "X2",
                            CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }
    }
}
