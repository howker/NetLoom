using NetLoom.Application.Snmp;

namespace NetLoom.Protocols.Snmp.Transport
{
    public static class SnmpFailureClassifier
    {
        public static SnmpTransportFailure ClassifyReport(
            string firstVariableOid)
        {
            switch (firstVariableOid?.TrimStart('.'))
            {
                case "1.3.6.1.6.3.15.1.1.1.0":
                case "1.3.6.1.6.3.15.1.1.3.0":
                case "1.3.6.1.6.3.15.1.1.5.0":
                case "1.3.6.1.6.3.15.1.1.6.0":
                    return SnmpTransportFailure.Authentication;
                default:
                    return SnmpTransportFailure.Protocol;
            }
        }

        public static SnmpTransportFailure ClassifyErrorStatus(
            int errorStatus)
        {
            return errorStatus == 6 || errorStatus == 16
                ? SnmpTransportFailure.Authentication
                : SnmpTransportFailure.Protocol;
        }
    }
}
