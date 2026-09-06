using System;
using System.Collections.Generic;
using System.Globalization;
using NetLoom.Application.Topology;
using NetLoom.Domain.Topology;
using NetLoom.Persistence.Sqlite.Database;

namespace NetLoom.Persistence.Sqlite.Topology
{
    public sealed class SqlitePhysicalLinkEvidenceExplanationReader
        : IPhysicalLinkEvidenceExplanationReader
    {
        private readonly SqliteConnectionFactory _connectionFactory;

        public SqlitePhysicalLinkEvidenceExplanationReader(
            SqliteConnectionFactory connectionFactory)
        {
            _connectionFactory =
                connectionFactory ??
                throw new ArgumentNullException(
                    nameof(connectionFactory));
        }

        public IReadOnlyList<PhysicalLinkEvidenceExplanation> Get(
            Guid physicalLinkId)
        {
            if (physicalLinkId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Physical link id is required.",
                    nameof(physicalLinkId));
            }

            using (var connection =
                _connectionFactory.OpenConnection())
            using (var command =
                connection.CreateCommand())
            {
                command.CommandText = @"
SELECT
    e.physical_link_id,
    e.evidence_kind,
    e.evidence_strength,
    e.source_address,
    e.slot_discriminator,
    e.observation_id,
    e.captured_utc,
    e.detail,
    CASE
        WHEN e.observation_id IS NULL THEN 0
        WHEN o.observation_id IS NULL THEN 2
        ELSE 1
    END AS raw_availability
FROM physical_link_evidence_current e
LEFT JOIN observations o
    ON o.observation_id = e.observation_id
WHERE e.physical_link_id = @physicalLinkId
ORDER BY
    e.evidence_kind,
    e.source_address,
    e.slot_discriminator;";

                command.Parameters.AddWithValue(
                    "@physicalLinkId",
                    physicalLinkId.ToString("D"));

                var result =
                    new List<PhysicalLinkEvidenceExplanation>();

                using (var reader =
                    command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var observationId =
                            reader.IsDBNull(5)
                                ? (Guid?)null
                                : Guid.Parse(
                                    reader.GetString(5));

                        var capturedUtc =
                            reader.IsDBNull(6)
                                ? (DateTime?)null
                                : DateTime.Parse(
                                    reader.GetString(6),
                                    CultureInfo.InvariantCulture,
                                    DateTimeStyles.RoundtripKind);

                        var evidence =
                            new PhysicalLinkEvidence(
                                Guid.Parse(
                                    reader.GetString(0)),
                                Parse<PhysicalLinkEvidenceKind>(
                                    reader.GetString(1)),
                                Parse<PhysicalLinkEvidenceStrength>(
                                    reader.GetString(2)),
                                reader.GetString(3),
                                reader.GetString(4),
                                observationId,
                                capturedUtc,
                                reader.IsDBNull(7)
                                    ? null
                                    : reader.GetString(7));

                        result.Add(
                            new PhysicalLinkEvidenceExplanation(
                                evidence,
                                (ObservationRawAvailability)
                                    reader.GetInt32(8)));
                    }
                }

                return result;
            }
        }

        private static T Parse<T>(
            string value)
        {
            return (T)Enum.Parse(
                typeof(T),
                value,
                false);
        }
    }
}
