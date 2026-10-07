using System;
using System.Collections.Generic;
using NetLoom.Application.DiscoveryInbox;
using NetLoom.Persistence.Sqlite.Database;

namespace NetLoom.Persistence.Sqlite.Discovery
{
    public sealed class SqliteDiscoveryExclusionSource :
        IDiscoveryExclusionSource
    {
        private readonly SqliteConnectionFactory _connectionFactory;

        public SqliteDiscoveryExclusionSource(
            SqliteConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory ??
                throw new ArgumentNullException(
                    nameof(connectionFactory));
        }

        public IReadOnlyList<DiscoveryExclusionRule> GetRules(
            Guid accessProfileId)
        {
            if (accessProfileId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Access profile id is required.",
                    nameof(accessProfileId));
            }

            var rules = new List<DiscoveryExclusionRule>();

            using (var connection = _connectionFactory.OpenReadOnlyConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
SELECT target_kind, target_value
FROM access_profile_exclusions
WHERE access_profile_id = @profileId
ORDER BY target_kind, target_value, exclusion_id;";
                command.Parameters.AddWithValue(
                    "@profileId",
                    accessProfileId.ToString("D"));

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        rules.Add(
                            new DiscoveryExclusionRule(
                                reader.GetString(0),
                                reader.GetString(1)));
                    }
                }
            }

            return rules.AsReadOnly();
        }
    }
}
