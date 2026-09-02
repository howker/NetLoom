using System;

namespace NetLoom.Application.Snmp
{
    public sealed class SnmpVariable
    {
        private readonly byte[] _encodedValue;

        public SnmpVariable(
            string oid,
            int typeCode,
            string displayValue,
            byte[] encodedValue)
        {
            Oid = oid ?? throw new ArgumentNullException(nameof(oid));
            DisplayValue = displayValue ?? string.Empty;
            TypeCode = typeCode;

            _encodedValue = encodedValue == null
                ? new byte[0]
                : (byte[])encodedValue.Clone();
        }

        public string Oid { get; }

        public int TypeCode { get; }

        public string DisplayValue { get; }

        public byte[] GetEncodedValue()
        {
            return (byte[])_encodedValue.Clone();
        }
    }
}
