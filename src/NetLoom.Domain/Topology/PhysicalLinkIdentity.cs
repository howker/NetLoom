using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace NetLoom.Domain.Topology
{
    public static class PhysicalLinkIdentity
    {
        public static string BuildLinkKey(
            Guid deviceAId,
            Guid? interfaceAId,
            Guid deviceBId,
            Guid? interfaceBId)
        {
            Guid canonicalDeviceAId;
            Guid? canonicalInterfaceAId;
            Guid canonicalDeviceBId;
            Guid? canonicalInterfaceBId;

            Canonicalize(
                deviceAId,
                interfaceAId,
                deviceBId,
                interfaceBId,
                out canonicalDeviceAId,
                out canonicalInterfaceAId,
                out canonicalDeviceBId,
                out canonicalInterfaceBId);

            var payload =
                EndpointToken(
                    canonicalDeviceAId,
                    canonicalInterfaceAId) +
                "|" +
                EndpointToken(
                    canonicalDeviceBId,
                    canonicalInterfaceBId);

            using (var sha256 = SHA256.Create())
            {
                var hash =
                    sha256.ComputeHash(
                        Encoding.UTF8.GetBytes(payload));

                var result =
                    new StringBuilder(
                        "plink-v1-",
                        9 + (hash.Length * 2));

                foreach (var value in hash)
                {
                    result.Append(
                        value.ToString(
                            "x2",
                            CultureInfo.InvariantCulture));
                }

                return result.ToString();
            }
        }

        public static void Canonicalize(
            Guid deviceAId,
            Guid? interfaceAId,
            Guid deviceBId,
            Guid? interfaceBId,
            out Guid canonicalDeviceAId,
            out Guid? canonicalInterfaceAId,
            out Guid canonicalDeviceBId,
            out Guid? canonicalInterfaceBId)
        {
            ValidateEndpoint(
                deviceAId,
                interfaceAId,
                nameof(deviceAId));

            ValidateEndpoint(
                deviceBId,
                interfaceBId,
                nameof(deviceBId));

            if (deviceAId == deviceBId)
            {
                throw new ArgumentException(
                    "Physical link devices must differ.");
            }

            if (CompareEndpoint(
                deviceAId,
                interfaceAId,
                deviceBId,
                interfaceBId) <= 0)
            {
                canonicalDeviceAId = deviceAId;
                canonicalInterfaceAId = interfaceAId;
                canonicalDeviceBId = deviceBId;
                canonicalInterfaceBId = interfaceBId;
            }
            else
            {
                canonicalDeviceAId = deviceBId;
                canonicalInterfaceAId = interfaceBId;
                canonicalDeviceBId = deviceAId;
                canonicalInterfaceBId = interfaceAId;
            }
        }

        private static int CompareEndpoint(
            Guid leftDeviceId,
            Guid? leftInterfaceId,
            Guid rightDeviceId,
            Guid? rightInterfaceId)
        {
            var result =
                string.CompareOrdinal(
                    leftDeviceId.ToString("N"),
                    rightDeviceId.ToString("N"));

            if (result != 0)
            {
                return result;
            }

            return string.CompareOrdinal(
                InterfaceToken(leftInterfaceId),
                InterfaceToken(rightInterfaceId));
        }

        private static string EndpointToken(
            Guid deviceId,
            Guid? interfaceId)
        {
            return
                deviceId.ToString("N") +
                ":" +
                InterfaceToken(interfaceId);
        }

        private static string InterfaceToken(
            Guid? interfaceId)
        {
            return interfaceId.HasValue
                ? interfaceId.Value.ToString("N")
                : "-";
        }

        private static void ValidateEndpoint(
            Guid deviceId,
            Guid? interfaceId,
            string parameterName)
        {
            if (deviceId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Physical link device id is required.",
                    parameterName);
            }

            if (interfaceId.HasValue &&
                interfaceId.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "Physical link interface id cannot be empty.",
                    parameterName);
            }
        }
    }
}
