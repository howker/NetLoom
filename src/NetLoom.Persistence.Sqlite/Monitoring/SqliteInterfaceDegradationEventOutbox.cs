using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.Linq;
using NetLoom.Application.Monitoring.Interfaces;
using NetLoom.Persistence.Sqlite.Database;

namespace NetLoom.Persistence.Sqlite.Monitoring
{
    public sealed class SqliteInterfaceDegradationEventOutbox :
        IInterfaceDegradationEventOutbox,
        IInterfaceDegradationDeliveryStatusReader
    {
        private readonly SqliteConnectionFactory
            _connectionFactory;

        public SqliteInterfaceDegradationEventOutbox(
            SqliteConnectionFactory connectionFactory)
        {
            _connectionFactory =
                connectionFactory ??
                throw new ArgumentNullException(
                    nameof(connectionFactory));
        }

        public IReadOnlyList<InterfaceDegradationDeliveryStatus>
            ReadStatus(
                int maxCount,
                DateTime nowUtc)
        {
            if (maxCount < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxCount));
            }

            if (nowUtc.Kind !=
                DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "Interface degradation delivery status time must be UTC.",
                    nameof(nowUtc));
            }

            using (var connection =
                _connectionFactory.OpenReadOnlyConnection())
            using (var command =
                connection.CreateCommand())
            {
                command.CommandText = @"
SELECT
    event_key,
    device_id,
    if_index,
    captured_utc,
    transition_kind,
    previous_status,
    previous_evidence_fingerprint,
    current_status,
    current_evidence_fingerprint,
    error_rate_per_minute,
    discard_rate_per_minute,
    reason_codes,
    delivery_failure_count,
    last_delivery_failure_utc,
    next_delivery_attempt_utc,
    delivered_utc
FROM interface_degradation_outbox
ORDER BY
    captured_utc DESC,
    event_key DESC
LIMIT @maxCount;";

                command.Parameters.AddWithValue(
                    "@maxCount",
                    maxCount);

                var result =
                    new List<InterfaceDegradationDeliveryStatus>();

                using (var reader =
                    command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var storedEventKey =
                            reader.GetString(0);

                        var item =
                            new InterfaceDegradationOutboxEvent(
                                Guid.Parse(
                                    reader.GetString(1)),
                                Convert.ToInt32(
                                    reader.GetValue(2),
                                    CultureInfo.InvariantCulture),
                                ParseUtc(
                                    reader.GetString(3)),
                                (InterfaceDegradationTransitionKind)
                                    Convert.ToInt32(
                                        reader.GetValue(4),
                                        CultureInfo.InvariantCulture),
                                reader.IsDBNull(5)
                                    ? (InterfaceDegradationStatus?)null
                                    : (InterfaceDegradationStatus)
                                        Convert.ToInt32(
                                            reader.GetValue(5),
                                            CultureInfo.InvariantCulture),
                                reader.IsDBNull(6)
                                    ? string.Empty
                                    : reader.GetString(6),
                                (InterfaceDegradationStatus)
                                    Convert.ToInt32(
                                        reader.GetValue(7),
                                        CultureInfo.InvariantCulture),
                                reader.GetString(8),
                                reader.IsDBNull(9)
                                    ? (double?)null
                                    : Convert.ToDouble(
                                        reader.GetValue(9),
                                        CultureInfo.InvariantCulture),
                                reader.IsDBNull(10)
                                    ? (double?)null
                                    : Convert.ToDouble(
                                        reader.GetValue(10),
                                        CultureInfo.InvariantCulture),
                                ParseReasons(
                                    reader.GetString(11)));

                        if (!string.Equals(
                            storedEventKey,
                            item.EventKey,
                            StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException(
                                "Stored interface degradation outbox event key does not match its immutable payload.");
                        }

                        result.Add(
                            new InterfaceDegradationDeliveryStatus(
                                item,
                                Convert.ToInt32(
                                    reader.GetValue(12),
                                    CultureInfo.InvariantCulture),
                                reader.IsDBNull(13)
                                    ? (DateTime?)null
                                    : ParseUtc(
                                        reader.GetString(13)),
                                reader.IsDBNull(14)
                                    ? (DateTime?)null
                                    : ParseUtc(
                                        reader.GetString(14)),
                                reader.IsDBNull(15)
                                    ? (DateTime?)null
                                    : ParseUtc(
                                        reader.GetString(15)),
                                nowUtc));
                    }
                }

                return result;
            }
        }

        public IReadOnlyList<InterfaceDegradationOutboxEvent>
            ReadPending(
                int maxCount)
        {
            return ReadPendingInternal(
                    maxCount,
                    null)
                .Select(
                    delivery =>
                        delivery.Event)
                .ToArray();
        }

        public IReadOnlyList<InterfaceDegradationPendingDelivery>
            ReadReady(
                int maxCount,
                DateTime eligibleUtc)
        {
            if (eligibleUtc.Kind !=
                DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "Interface degradation delivery eligibility timestamp must be UTC.",
                    nameof(eligibleUtc));
            }

            return ReadPendingInternal(
                maxCount,
                eligibleUtc);
        }

        private IReadOnlyList<InterfaceDegradationPendingDelivery>
            ReadPendingInternal(
                int maxCount,
                DateTime? eligibleUtc)
        {
            if (maxCount < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxCount));
            }

            using (var connection =
                _connectionFactory.OpenConnection())
            using (var command =
                connection.CreateCommand())
            {
                command.CommandText = @"
SELECT
    event_key,
    device_id,
    if_index,
    captured_utc,
    transition_kind,
    previous_status,
    previous_evidence_fingerprint,
    current_status,
    current_evidence_fingerprint,
    error_rate_per_minute,
    discard_rate_per_minute,
    reason_codes,
    delivery_failure_count,
    last_delivery_failure_utc,
    next_delivery_attempt_utc
FROM interface_degradation_outbox
WHERE delivered_utc IS NULL" +
                    (eligibleUtc.HasValue
                        ? @"
  AND (
      next_delivery_attempt_utc IS NULL OR
      next_delivery_attempt_utc <= @eligibleUtc
  )"
                        : string.Empty) +
                    @"
ORDER BY
    captured_utc,
    event_key
LIMIT @maxCount;";

                command.Parameters.AddWithValue(
                    "@maxCount",
                    maxCount);

                if (eligibleUtc.HasValue)
                {
                    command.Parameters.AddWithValue(
                        "@eligibleUtc",
                        FormatUtc(
                            eligibleUtc.Value));
                }

                var result =
                    new List<InterfaceDegradationPendingDelivery>();

                using (var reader =
                    command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var storedEventKey =
                            reader.GetString(0);

                        var item =
                            new InterfaceDegradationOutboxEvent(
                                Guid.Parse(
                                    reader.GetString(1)),
                                Convert.ToInt32(
                                    reader.GetValue(2),
                                    CultureInfo.InvariantCulture),
                                ParseUtc(
                                    reader.GetString(3)),
                                (InterfaceDegradationTransitionKind)
                                    Convert.ToInt32(
                                        reader.GetValue(4),
                                        CultureInfo.InvariantCulture),
                                reader.IsDBNull(5)
                                    ? (InterfaceDegradationStatus?)null
                                    : (InterfaceDegradationStatus)
                                        Convert.ToInt32(
                                            reader.GetValue(5),
                                            CultureInfo.InvariantCulture),
                                reader.IsDBNull(6)
                                    ? string.Empty
                                    : reader.GetString(6),
                                (InterfaceDegradationStatus)
                                    Convert.ToInt32(
                                        reader.GetValue(7),
                                        CultureInfo.InvariantCulture),
                                reader.GetString(8),
                                reader.IsDBNull(9)
                                    ? (double?)null
                                    : Convert.ToDouble(
                                        reader.GetValue(9),
                                        CultureInfo.InvariantCulture),
                                reader.IsDBNull(10)
                                    ? (double?)null
                                    : Convert.ToDouble(
                                        reader.GetValue(10),
                                        CultureInfo.InvariantCulture),
                                ParseReasons(
                                    reader.GetString(11)));

                        if (!string.Equals(
                            storedEventKey,
                            item.EventKey,
                            StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException(
                                "Stored interface degradation outbox event key does not match its immutable payload.");
                        }

                        result.Add(
                            new InterfaceDegradationPendingDelivery(
                                item,
                                Convert.ToInt32(
                                    reader.GetValue(12),
                                    CultureInfo.InvariantCulture),
                                reader.IsDBNull(13)
                                    ? (DateTime?)null
                                    : ParseUtc(
                                        reader.GetString(13)),
                                reader.IsDBNull(14)
                                    ? (DateTime?)null
                                    : ParseUtc(
                                        reader.GetString(14))));
                    }
                }

                return result;
            }
        }

        public bool MarkDeliveryFailed(
            string eventKey,
            int expectedFailureCount,
            DateTime failedUtc,
            DateTime nextAttemptUtc)
        {
            if (string.IsNullOrWhiteSpace(
                eventKey))
            {
                throw new ArgumentException(
                    "EVENT_KEY_REQUIRED",
                    nameof(eventKey));
            }

            if (expectedFailureCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(expectedFailureCount));
            }

            if (failedUtc.Kind !=
                DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "Interface degradation delivery failure timestamp must be UTC.",
                    nameof(failedUtc));
            }

            if (nextAttemptUtc.Kind !=
                DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "Interface degradation next-attempt timestamp must be UTC.",
                    nameof(nextAttemptUtc));
            }

            if (nextAttemptUtc <=
                failedUtc)
            {
                throw new ArgumentException(
                    "Interface degradation next attempt must be later than the delivery failure.",
                    nameof(nextAttemptUtc));
            }

            using (var connection =
                _connectionFactory.OpenConnection())
            {
                return SqliteImmediateWrite.Execute(
                    connection,
                    () =>
                    {
                        using (var command =
                            connection.CreateCommand())
                        {
                            command.CommandText = @"
UPDATE interface_degradation_outbox
SET
    delivery_failure_count =
        delivery_failure_count + 1,
    last_delivery_failure_utc =
        @failedUtc,
    next_delivery_attempt_utc =
        @nextAttemptUtc
WHERE event_key = @eventKey
  AND delivered_utc IS NULL
  AND delivery_failure_count =
        @expectedFailureCount;";

                            command.Parameters.AddWithValue(
                                "@eventKey",
                                eventKey);

                            command.Parameters.AddWithValue(
                                "@expectedFailureCount",
                                expectedFailureCount);

                            command.Parameters.AddWithValue(
                                "@failedUtc",
                                FormatUtc(
                                    failedUtc));

                            command.Parameters.AddWithValue(
                                "@nextAttemptUtc",
                                FormatUtc(
                                    nextAttemptUtc));

                            var affected =
                                command.ExecuteNonQuery();

                            if (affected == 1)
                            {
                                return true;
                            }
                        }

                        using (var probe =
                            connection.CreateCommand())
                        {
                            probe.CommandText = @"
SELECT
    delivered_utc,
    delivery_failure_count
FROM interface_degradation_outbox
WHERE event_key = @eventKey;";

                            probe.Parameters.AddWithValue(
                                "@eventKey",
                                eventKey);

                            using (var reader =
                                probe.ExecuteReader())
                            {
                                if (!reader.Read())
                                {
                                    throw new InvalidOperationException(
                                        "Interface degradation outbox event does not exist.");
                                }

                                return false;
                            }
                        }
                    });
            }
        }

        public bool MarkDelivered(
            string eventKey,
            DateTime deliveredUtc)
        {
            if (string.IsNullOrWhiteSpace(
                eventKey))
            {
                throw new ArgumentException(
                    "EVENT_KEY_REQUIRED",
                    nameof(eventKey));
            }

            if (deliveredUtc.Kind !=
                DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "Interface degradation delivery timestamp must be UTC.",
                    nameof(deliveredUtc));
            }

            using (var connection =
                _connectionFactory.OpenConnection())
            using (var command =
                connection.CreateCommand())
            {
                command.CommandText = @"
UPDATE interface_degradation_outbox
SET delivered_utc = @deliveredUtc
WHERE event_key = @eventKey
  AND delivered_utc IS NULL;";

                command.Parameters.AddWithValue(
                    "@eventKey",
                    eventKey);

                command.Parameters.AddWithValue(
                    "@deliveredUtc",
                    FormatUtc(
                        deliveredUtc));

                var affected =
                    command.ExecuteNonQuery();

                if (affected == 1)
                {
                    return true;
                }

                using (var probe =
                    connection.CreateCommand())
                {
                    probe.CommandText = @"
SELECT delivered_utc
FROM interface_degradation_outbox
WHERE event_key = @eventKey;";

                    probe.Parameters.AddWithValue(
                        "@eventKey",
                        eventKey);

                    var existing =
                        probe.ExecuteScalar();

                    if (existing == null ||
                        existing == DBNull.Value)
                    {
                        throw new InvalidOperationException(
                            "Interface degradation outbox event does not exist.");
                    }

                    return false;
                }
            }
        }

        internal static void Insert(
            SQLiteConnection connection,
            InterfaceDegradationOutboxEvent item)
        {
            if (connection == null)
            {
                throw new ArgumentNullException(
                    nameof(connection));
            }

            if (item == null)
            {
                throw new ArgumentNullException(
                    nameof(item));
            }

            using (var command =
                connection.CreateCommand())
            {
                command.CommandText = @"
INSERT INTO interface_degradation_outbox
(
    event_key,
    device_id,
    if_index,
    captured_utc,
    transition_kind,
    previous_status,
    previous_evidence_fingerprint,
    current_status,
    current_evidence_fingerprint,
    error_rate_per_minute,
    discard_rate_per_minute,
    reason_codes
)
VALUES
(
    @eventKey,
    @deviceId,
    @ifIndex,
    @capturedUtc,
    @transitionKind,
    @previousStatus,
    @previousEvidenceFingerprint,
    @currentStatus,
    @currentEvidenceFingerprint,
    @errorRatePerMinute,
    @discardRatePerMinute,
    @reasonCodes
);";

                command.Parameters.AddWithValue(
                    "@eventKey",
                    item.EventKey);

                command.Parameters.AddWithValue(
                    "@deviceId",
                    item.DeviceId.ToString("D"));

                command.Parameters.AddWithValue(
                    "@ifIndex",
                    item.IfIndex);

                command.Parameters.AddWithValue(
                    "@capturedUtc",
                    FormatUtc(
                        item.CapturedUtc));

                command.Parameters.AddWithValue(
                    "@transitionKind",
                    (int)item.TransitionKind);

                command.Parameters.AddWithValue(
                    "@previousStatus",
                    item.PreviousStatus.HasValue
                        ? (object)(int)item.PreviousStatus.Value
                        : DBNull.Value);

                command.Parameters.AddWithValue(
                    "@previousEvidenceFingerprint",
                    item.PreviousStatus.HasValue
                        ? (object)item.PreviousEvidenceFingerprint
                        : DBNull.Value);

                command.Parameters.AddWithValue(
                    "@currentStatus",
                    (int)item.CurrentStatus);

                command.Parameters.AddWithValue(
                    "@currentEvidenceFingerprint",
                    item.CurrentEvidenceFingerprint);

                command.Parameters.AddWithValue(
                    "@errorRatePerMinute",
                    item.ErrorRatePerMinute.HasValue
                        ? (object)item.ErrorRatePerMinute.Value
                        : DBNull.Value);

                command.Parameters.AddWithValue(
                    "@discardRatePerMinute",
                    item.DiscardRatePerMinute.HasValue
                        ? (object)item.DiscardRatePerMinute.Value
                        : DBNull.Value);

                command.Parameters.AddWithValue(
                    "@reasonCodes",
                    FormatReasons(
                        item.Reasons));

                command.ExecuteNonQuery();
            }
        }

        private static string FormatReasons(
            IReadOnlyList<InterfaceDegradationReason> reasons)
        {
            return string.Join(
                ",",
                reasons.Select(
                    reason =>
                        ((int)reason).ToString(
                            CultureInfo.InvariantCulture)));
        }

        private static InterfaceDegradationReason[] ParseReasons(
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return Array.Empty<InterfaceDegradationReason>();
            }

            return value
                .Split(',')
                .Select(
                    token =>
                        (InterfaceDegradationReason)
                            int.Parse(
                                token,
                                NumberStyles.Integer,
                                CultureInfo.InvariantCulture))
                .ToArray();
        }

        private static string FormatUtc(
            DateTime value)
        {
            if (value.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "Interface degradation outbox timestamp must be UTC.",
                    nameof(value));
            }

            return value.ToString(
                "o",
                CultureInfo.InvariantCulture);
        }

        private static DateTime ParseUtc(
            string value)
        {
            var parsed =
                DateTime.Parse(
                    value,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind);

            if (parsed.Kind != DateTimeKind.Utc)
            {
                throw new InvalidOperationException(
                    "Stored interface degradation outbox timestamp must be UTC.");
            }

            return parsed;
        }
    }
}
