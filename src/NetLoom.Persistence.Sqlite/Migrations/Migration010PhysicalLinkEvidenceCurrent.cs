using System.Collections.Generic;

namespace NetLoom.Persistence.Sqlite.Migrations
{
    public sealed class Migration010PhysicalLinkEvidenceCurrent :
        IMigration
    {
        private static readonly IReadOnlyList<string> SqlStatements =
            new[]
            {
                @"
CREATE TABLE physical_link_evidence_current
(
    physical_link_id TEXT NOT NULL,
    evidence_kind TEXT NOT NULL,
    evidence_strength TEXT NOT NULL,
    source_address TEXT NOT NULL,
    slot_discriminator TEXT NOT NULL,
    observation_id TEXT NULL,
    captured_utc TEXT NULL,
    detail TEXT NULL,

    PRIMARY KEY
    (
        physical_link_id,
        evidence_kind,
        source_address,
        slot_discriminator
    ),

    FOREIGN KEY(physical_link_id)
        REFERENCES physical_links(id)
        ON DELETE CASCADE,

    CHECK(length(source_address) > 0),
    CHECK(length(slot_discriminator) > 0)
);"
            };

        public int Version
        {
            get { return 10; }
        }

        public string Name
        {
            get { return "Current physical link evidence"; }
        }

        public IReadOnlyList<string> Statements
        {
            get { return SqlStatements; }
        }
    }
}
