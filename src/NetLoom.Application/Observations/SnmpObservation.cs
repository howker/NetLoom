using System;
using System.Collections.Generic;
using System.Linq;
using NetLoom.Application.Snmp;
using NetLoom.Domain.Observations;

namespace NetLoom.Application.Observations
{
    public sealed class SnmpObservation
    {
        public SnmpObservation(
            Observation observation,
            IEnumerable<SnmpVariable> variables)
        {
            Observation = observation ??
                throw new ArgumentNullException(nameof(observation));

            if (variables == null)
            {
                throw new ArgumentNullException(nameof(variables));
            }

            Variables = variables.ToArray();
        }

        public Observation Observation { get; }

        public IReadOnlyList<SnmpVariable> Variables { get; }
    }
}
