using System;
using System.Collections.Generic;
using System.Net;

namespace NetLoom.Application.Lookup
{
    public static class AddressTextNormalizer
    {
        public static string NormalizeMacCompact(
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var result =
                new char[12];

            var index = 0;

            foreach (var character in value)
            {
                var isHex =
                    (character >= '0' &&
                     character <= '9') ||
                    (character >= 'A' &&
                     character <= 'F') ||
                    (character >= 'a' &&
                     character <= 'f');

                if (!isHex)
                {
                    continue;
                }

                if (index >= result.Length)
                {
                    return null;
                }

                result[index++] =
                    char.ToUpperInvariant(character);
            }

            return index == result.Length
                ? new string(result)
                : null;
        }

        public static string NormalizeMacColon(
            string value)
        {
            var compact =
                NormalizeMacCompact(value);

            if (compact == null)
            {
                return null;
            }

            return
                compact.Substring(0, 2) + ":" +
                compact.Substring(2, 2) + ":" +
                compact.Substring(4, 2) + ":" +
                compact.Substring(6, 2) + ":" +
                compact.Substring(8, 2) + ":" +
                compact.Substring(10, 2);
        }

        public static IReadOnlyList<string>
            MacSearchForms(
                string value)
        {
            var compact =
                NormalizeMacCompact(value);

            if (compact == null)
            {
                return new string[0];
            }

            var colon =
                NormalizeMacColon(compact);

            var hyphen =
                colon.Replace(":", "-");

            var dotted =
                compact.Substring(0, 4) + "." +
                compact.Substring(4, 4) + "." +
                compact.Substring(8, 4);

            return new[]
            {
                colon,
                colon.ToLowerInvariant(),
                hyphen,
                hyphen.ToLowerInvariant(),
                compact,
                compact.ToLowerInvariant(),
                dotted,
                dotted.ToLowerInvariant()
            };
        }

        public static string NormalizeIp(
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            IPAddress address;

            return IPAddress.TryParse(
                value.Trim(),
                out address)
                    ? address.ToString()
                    : null;
        }
    }
}