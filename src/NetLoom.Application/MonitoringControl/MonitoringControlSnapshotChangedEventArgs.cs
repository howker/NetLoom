using System;

namespace NetLoom.Application.MonitoringControl
{
    public sealed class MonitoringControlSnapshotChangedEventArgs :
        EventArgs
    {
        public MonitoringControlSnapshotChangedEventArgs(
            MonitoringControlSnapshot snapshot)
        {
            Snapshot =
                snapshot ??
                throw new ArgumentNullException(
                    nameof(snapshot));
        }

        public MonitoringControlSnapshot Snapshot { get; }
    }
}
