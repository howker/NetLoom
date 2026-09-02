using System;

namespace NetLoom.Application.Snmp
{
    public sealed class SnmpTransportException : Exception
    {
        public SnmpTransportException(
            SnmpTransportFailure failure,
            string message,
            Exception innerException)
            : base(message, innerException)
        {
            Failure = failure;
        }

        public SnmpTransportFailure Failure { get; }
    }
}
