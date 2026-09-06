using System.Collections.Generic;

namespace NetLoom.Persistence.Sqlite.Migrations
{
    public sealed class Migration011StpObservations :
        IMigration
    {
        private static readonly IReadOnlyList<string>
            SqlStatements =
                new[]
                {
                    @"
CREATE TABLE stp_observations
(
    observation_id TEXT NOT NULL,
    instance_id TEXT NOT NULL,
    protocol_specification INTEGER NULL,
    designated_root TEXT NULL,
    root_cost INTEGER NULL,
    root_bridge_port_index INTEGER NULL,
    root_if_index INTEGER NULL,

    PRIMARY KEY
    (
        observation_id,
        instance_id
    ),

    FOREIGN KEY (observation_id)
        REFERENCES observations(observation_id)
        ON DELETE CASCADE
);",

                    @"
CREATE INDEX ix_stp_observations_instance
ON stp_observations(instance_id);",

                    @"
CREATE TABLE stp_port_states
(
    observation_id TEXT NOT NULL,
    instance_id TEXT NOT NULL,
    bridge_port_index INTEGER NOT NULL,
    if_index INTEGER NULL,
    priority INTEGER NULL,
    state INTEGER NULL,
    enabled INTEGER NULL,
    path_cost INTEGER NULL,
    designated_root TEXT NULL,
    designated_cost INTEGER NULL,
    designated_bridge TEXT NULL,
    designated_port TEXT NULL,
    forward_transitions INTEGER NULL,

    PRIMARY KEY
    (
        observation_id,
        instance_id,
        bridge_port_index
    ),

    FOREIGN KEY
    (
        observation_id,
        instance_id
    )
        REFERENCES stp_observations
        (
            observation_id,
            instance_id
        )
        ON DELETE CASCADE
);",

                    @"
CREATE INDEX ix_stp_port_states_if_index
ON stp_port_states(if_index);",

                    @"
CREATE INDEX ix_stp_port_states_state
ON stp_port_states(state);"
                };

        public int Version
        {
            get { return 11; }
        }

        public string Name
        {
            get { return "STP observations"; }
        }

        public IReadOnlyList<string> Statements
        {
            get { return SqlStatements; }
        }
    }
}
