using System.Collections.Generic;
using System.Runtime.Serialization;

namespace NetLoom.Simulator.Replay
{
    [DataContract]
    public sealed class RawSnmpSnapshot
    {
        [DataMember(Name = "schemaVersion", Order = 1)]
        public int SchemaVersion { get; set; }

        [DataMember(Name = "observationId", Order = 2)]
        public string ObservationId { get; set; }

        [DataMember(Name = "kind", Order = 3)]
        public string Kind { get; set; }

        [DataMember(Name = "sourceAddress", Order = 4)]
        public string SourceAddress { get; set; }

        [DataMember(Name = "capturedUtc", Order = 5)]
        public string CapturedUtc { get; set; }

        [DataMember(Name = "variables", Order = 6)]
        public List<RawSnmpVarbindSnapshot> Variables { get; set; }
    }

    [DataContract]
    public sealed class RawSnmpVarbindSnapshot
    {
        [DataMember(Name = "oid", Order = 1)]
        public string Oid { get; set; }

        [DataMember(Name = "typeCode", Order = 2)]
        public int TypeCode { get; set; }

        [DataMember(Name = "displayValue", Order = 3)]
        public string DisplayValue { get; set; }

        [DataMember(Name = "encodedValueBase64", Order = 4)]
        public string EncodedValueBase64 { get; set; }
    }
}
