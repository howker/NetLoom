using System;
using System.Collections.Generic;
using NetLoom.Contracts.Alerts;

namespace NetLoom.Application.Alerts
{
    public enum TopologyAlertTransitionKind
    {
        Unchanged = 0,
        FirstAppearance = 1,
        Changed = 2,
        Resolved = 3
    }

    public sealed class TopologyAlertTransitionTracker
    {
        private readonly Dictionary<
            string,
            HashSet<string>> _previousAlertKeys =
                new Dictionary<
                    string,
                    HashSet<string>>(
                    StringComparer.Ordinal);

        public TopologyAlertTransitionKind Observe(
            TopologyAlertSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(
                    nameof(snapshot));
            }

            var current =
                new HashSet<string>(
                    StringComparer.Ordinal);

            foreach (var alert in snapshot.Alerts)
            {
                current.Add(
                    alert.AlertKey);
            }

            HashSet<string> previous;

            if (!_previousAlertKeys.TryGetValue(
                snapshot.InstanceId,
                out previous))
            {
                _previousAlertKeys[
                    snapshot.InstanceId] =
                    current;

                return current.Count == 0
                    ? TopologyAlertTransitionKind.Unchanged
                    : TopologyAlertTransitionKind.FirstAppearance;
            }

            if (previous.SetEquals(current))
            {
                return
                    TopologyAlertTransitionKind.Unchanged;
            }

            TopologyAlertTransitionKind result;

            if (previous.Count == 0 &&
                current.Count > 0)
            {
                result =
                    TopologyAlertTransitionKind
                        .FirstAppearance;
            }
            else if (previous.Count > 0 &&
                     current.Count == 0)
            {
                result =
                    TopologyAlertTransitionKind
                        .Resolved;
            }
            else
            {
                result =
                    TopologyAlertTransitionKind
                        .Changed;
            }

            _previousAlertKeys[
                snapshot.InstanceId] =
                current;

            return result;
        }
    }
}
