using System;
using System.Linq;
using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Monitoring.Health;
using NetLoom.Application.Monitoring.Metrics;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class MonitoringMetricFoundationTests
    {
        private static readonly DateTime T1 =
            new DateTime(
                2026, 1, 2, 12, 0, 0,
                DateTimeKind.Utc);

        [TestMethod]
        public void UnboundHealthSnapshotDoesNotCreatePersistentMetricIdentity()
        {
            var snapshot =
                new HealthSnapshot(
                    null,
                    IPAddress.Parse(
                        "192.0.2.30"),
                    T1,
                    HealthStatus.Up,
                    TimeSpan.FromMinutes(5));

            var projector =
                new HealthMetricProjector();

            var samples =
                projector.Project(
                    snapshot);

            Assert.AreEqual(
                0,
                samples.Count);
        }

        [TestMethod]
        public void BoundHealthSnapshotProjectsStableDeviceMetrics()
        {
            var deviceId =
                Guid.NewGuid();

            var snapshot =
                new HealthSnapshot(
                    deviceId,
                    IPAddress.Parse(
                        "192.0.2.31"),
                    T1,
                    HealthStatus.Up,
                    TimeSpan.FromSeconds(123));

            var samples =
                new HealthMetricProjector().
                    Project(
                        snapshot);

            Assert.AreEqual(
                2,
                samples.Count);

            Assert.IsTrue(
                samples.All(
                    sample =>
                        sample.DeviceId ==
                        deviceId));

            Assert.IsTrue(
                samples.Any(
                    sample =>
                        sample.Kind ==
                            MonitoringMetricKind.HealthAvailability &&
                        sample.Value ==
                            1.0));

            Assert.IsTrue(
                samples.Any(
                    sample =>
                        sample.Kind ==
                            MonitoringMetricKind.HealthUptimeSeconds &&
                        sample.Value ==
                            123.0));
        }

        [TestMethod]
        public void DownHealthProjectsAvailabilityZero()
        {
            var samples =
                new HealthMetricProjector().
                    Project(
                        new HealthSnapshot(
                            Guid.NewGuid(),
                            IPAddress.Parse(
                                "192.0.2.32"),
                            T1,
                            HealthStatus.Down,
                            null));

            Assert.AreEqual(
                1,
                samples.Count);

            Assert.AreEqual(
                MonitoringMetricKind.HealthAvailability,
                samples[0].Kind);

            Assert.AreEqual(
                0.0,
                samples[0].Value);
        }

        [TestMethod]
        public void MetricSampleRejectsEmptyDeviceId()
        {
            try
            {
                new MonitoringMetricSample(
                    Guid.Empty,
                    null,
                    MonitoringMetricKind.HealthAvailability,
                    T1,
                    1.0);

                Assert.Fail(
                    "Expected empty DeviceId rejection.");
            }
            catch (ArgumentException)
            {
            }
        }

        [TestMethod]
        public void HealthSnapshotRejectsNonUtcTimestamp()
        {
            try
            {
                new HealthSnapshot(
                    Guid.NewGuid(),
                    IPAddress.Loopback,
                    DateTime.SpecifyKind(
                        T1,
                        DateTimeKind.Local),
                    HealthStatus.Up,
                    null);

                Assert.Fail(
                    "Expected non-UTC rejection.");
            }
            catch (ArgumentException)
            {
            }
        }
    }
}
