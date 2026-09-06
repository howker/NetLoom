using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using NetLoom.Application.Observations;
using NetLoom.Application.Snmp;
using NetLoom.Domain.Observations;

namespace NetLoom.Simulator.Replay
{
    public sealed class RawSnmpSnapshotCodec
    {
        public const int CurrentSchemaVersion = 1;

        private static readonly DataContractJsonSerializer
            Serializer =
                new DataContractJsonSerializer(
                    typeof(RawSnmpSnapshot));

        public RawSnmpSnapshot Load(
            string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException(
                    "Snapshot path is required.",
                    nameof(path));
            }

            using (var stream = File.OpenRead(path))
            {
                var snapshot =
                    Serializer.ReadObject(stream)
                    as RawSnmpSnapshot;

                Validate(snapshot);

                return snapshot;
            }
        }

        public void Save(
            string path,
            RawSnmpSnapshot snapshot)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException(
                    "Snapshot path is required.",
                    nameof(path));
            }

            Validate(snapshot);

            var directory =
                Path.GetDirectoryName(
                    Path.GetFullPath(path));

            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using (var stream =
                File.Create(path))
            {
                Serializer.WriteObject(
                    stream,
                    snapshot);
            }
        }

        public RawSnmpSnapshot Capture(
            SnmpObservation observation)
        {
            if (observation == null)
            {
                throw new ArgumentNullException(
                    nameof(observation));
            }

            return new RawSnmpSnapshot
            {
                SchemaVersion =
                    CurrentSchemaVersion,

                ObservationId =
                    observation.Observation.Id
                        .ToString("D"),

                Kind =
                    observation.Observation.Kind
                        .ToString(),

                SourceAddress =
                    observation.Observation.SourceAddress,

                CapturedUtc =
                    observation.Observation.CapturedUtc
                        .ToString(
                            "O",
                            CultureInfo.InvariantCulture),

                Variables =
                    observation.Variables
                        .Select(
                            variable =>
                                new RawSnmpVarbindSnapshot
                                {
                                    Oid =
                                        variable.Oid,

                                    TypeCode =
                                        variable.TypeCode,

                                    DisplayValue =
                                        variable.DisplayValue,

                                    EncodedValueBase64 =
                                        Convert.ToBase64String(
                                            variable.GetEncodedValue() ??
                                            new byte[0])
                                })
                        .ToList()
            };
        }

        public SnmpObservation BuildObservation(
            RawSnmpSnapshot snapshot)
        {
            Validate(snapshot);

            Guid observationId;

            if (!Guid.TryParse(
                snapshot.ObservationId,
                out observationId) ||
                observationId == Guid.Empty)
            {
                throw Invalid(
                    "observationId must be a non-empty GUID.");
            }

            ObservationKind kind;

            if (!Enum.TryParse(
                snapshot.Kind,
                true,
                out kind))
            {
                throw Invalid(
                    "kind is not a known ObservationKind.");
            }

            DateTime capturedUtc;

            if (!DateTime.TryParse(
                    snapshot.CapturedUtc,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out capturedUtc) ||
                capturedUtc.Kind != DateTimeKind.Utc)
            {
                throw Invalid(
                    "capturedUtc must be an ISO-8601 UTC value.");
            }

            var variables =
                new List<SnmpVariable>();

            for (var index = 0;
                 index < snapshot.Variables.Count;
                 index++)
            {
                var item =
                    snapshot.Variables[index];

                byte[] encoded;

                try
                {
                    encoded =
                        string.IsNullOrEmpty(
                            item.EncodedValueBase64)
                            ? new byte[0]
                            : Convert.FromBase64String(
                                item.EncodedValueBase64);
                }
                catch (FormatException exception)
                {
                    throw new InvalidDataException(
                        "variables[" +
                        index +
                        "].encodedValueBase64 is invalid.",
                        exception);
                }

                variables.Add(
                    new SnmpVariable(
                        item.Oid,
                        item.TypeCode,
                        item.DisplayValue,
                        encoded));
            }

            return new SnmpObservation(
                new Observation(
                    observationId,
                    kind,
                    snapshot.SourceAddress,
                    capturedUtc),
                variables);
        }

        private static void Validate(
            RawSnmpSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw Invalid(
                    "Snapshot is empty.");
            }

            if (snapshot.SchemaVersion !=
                CurrentSchemaVersion)
            {
                throw Invalid(
                    "Unsupported schemaVersion: " +
                    snapshot.SchemaVersion + ".");
            }

            if (string.IsNullOrWhiteSpace(
                snapshot.ObservationId))
            {
                throw Invalid(
                    "observationId is required.");
            }

            if (string.IsNullOrWhiteSpace(
                snapshot.Kind))
            {
                throw Invalid(
                    "kind is required.");
            }

            if (string.IsNullOrWhiteSpace(
                snapshot.SourceAddress))
            {
                throw Invalid(
                    "sourceAddress is required.");
            }

            if (string.IsNullOrWhiteSpace(
                snapshot.CapturedUtc))
            {
                throw Invalid(
                    "capturedUtc is required.");
            }

            if (snapshot.Variables == null)
            {
                throw Invalid(
                    "variables is required.");
            }

            for (var index = 0;
                 index < snapshot.Variables.Count;
                 index++)
            {
                var item =
                    snapshot.Variables[index];

                if (item == null)
                {
                    throw Invalid(
                        "variables[" +
                        index +
                        "] is null.");
                }

                if (string.IsNullOrWhiteSpace(
                    item.Oid))
                {
                    throw Invalid(
                        "variables[" +
                        index +
                        "].oid is required.");
                }
            }
        }

        private static InvalidDataException Invalid(
            string message)
        {
            return new InvalidDataException(
                message);
        }
    }
}
