using System;
using NetLoom.Application.Monitoring.Interfaces;
using NetLoom.Persistence.Sqlite.Database;

namespace NetLoom.Persistence.Sqlite.Monitoring
{
    public sealed class SqliteInterfaceDegradationTransitionProcessor :
        IInterfaceDegradationTransitionProcessor
    {
        private readonly SqliteConnectionFactory
            _connectionFactory;

        private readonly InterfaceDegradationTransitionEvaluator
            _evaluator;

        public SqliteInterfaceDegradationTransitionProcessor(
            SqliteConnectionFactory connectionFactory)
        {
            _connectionFactory =
                connectionFactory ??
                throw new ArgumentNullException(
                    nameof(connectionFactory));

            _evaluator =
                new InterfaceDegradationTransitionEvaluator();
        }

        public InterfaceDegradationTransition Observe(
            InterfaceDegradationClassification classification)
        {
            if (classification == null)
            {
                throw new ArgumentNullException(
                    nameof(classification));
            }

            if (classification.Status ==
                InterfaceDegradationStatus.Indeterminate)
            {
                using (var connection =
                    _connectionFactory.OpenConnection())
                {
                    var previous =
                        SqliteInterfaceDegradationStateStore
                            .Load(
                                connection,
                                classification.DeviceId,
                                classification.IfIndex);

                    return _evaluator.Evaluate(
                        classification,
                        previous);
                }
            }

            using (var connection =
                _connectionFactory.OpenConnection())
            {
                return SqliteImmediateWrite.Execute(
                    connection,
                    () =>
                    {
                        var previous =
                            SqliteInterfaceDegradationStateStore
                                .Load(
                                    connection,
                                    classification.DeviceId,
                                    classification.IfIndex);

                        var transition =
                            _evaluator.Evaluate(
                                classification,
                                previous);

                        var current =
                            transition.CurrentState;

                        if (current == null)
                        {
                            throw new InvalidOperationException(
                                "Determinate interface degradation transition requires a current state.");
                        }

                        if (previous != null &&
                            current.CapturedUtc <=
                                previous.CapturedUtc)
                        {
                            throw new InvalidOperationException(
                                "Interface degradation state must move forward in time.");
                        }

                        SqliteInterfaceDegradationStateStore
                            .Save(
                                connection,
                                current);

                        if (transition.HasStateChange)
                        {
                            SqliteInterfaceDegradationEventOutbox
                                .Insert(
                                    connection,
                                    InterfaceDegradationOutboxEvent
                                        .FromTransition(
                                            transition));
                        }

                        return transition;
                    });
            }
        }
    }
}
