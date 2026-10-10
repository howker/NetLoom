using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Lookup;
using NetLoom.Application.MonitoringControl;
using NetLoom.Application.PollingPolicies;
using NetLoom.Application.TopologyRefresh;
using NetLoom.Contracts.Alerts;
using NetLoom.Contracts.Diagnostics;
using NetLoom.Contracts.TopologyMap;
using NetLoom.Domain.Access;
using NetLoom.Persistence.Sqlite.Database;
using NetLoom.Persistence.Sqlite.PollingPolicies;
using NetLoom.Wpf;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class Sprint51PolicyConsequencesTests
    {
        private static readonly DateTime Now = new DateTime(2026, 10, 10, 8, 0, 0, DateTimeKind.Utc);

        [TestMethod]
        public void DirectAndInheritedDisabledPoliciesRemoveTargetsAndExplainTheResult()
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                var directory = Path.Combine(Path.GetTempPath(), "NetLoom.Tests", Guid.NewGuid().ToString("N"));
                var previousCulture = CultureInfo.CurrentUICulture;
                MainWindow window = null;
                try
                {
                    CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("ru-RU");
                    SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
                    var a = Guid.NewGuid();
                    var b = Guid.NewGuid();
                    var c = Guid.NewGuid();
                    var location = Guid.NewGuid();
                    var factory = new SqliteConnectionFactory(Path.Combine(directory, "netloom.db"));
                    new DatabaseInitializer(factory).Initialize();
                    using (var connection = factory.OpenConnection())
                    {
                        InsertLocation(connection, location);
                        InsertDevice(connection, a, null);
                        InsertDevice(connection, b, null);
                        InsertDevice(connection, c, location);
                    }
                    var store = new SqlitePollingPolicyStore(factory);
                    var off = new PollingPolicy(Guid.NewGuid(), "Без опроса", false, false,
                        PollingSchedule.Off, PollingSchedule.Off, new[] { 22 });
                    store.SavePolicy(off, Now);
                    store.Assign(PollingPolicySubjectKind.Device, b, off.Id, Now);
                    store.Assign(PollingPolicySubjectKind.Location, location, off.Id, Now);

                    var monitoring = new RecordingMonitoringControl();
                    window = CreateWindow(new[] { a, b, c }, monitoring);
                    window.PollingPolicyStore = store;
                    window.Show();
                    WaitFor(() => ((ItemsControl)window.FindName("EquipmentList")).Items.Count == 3);
                    ((ComboBox)window.FindName("DiscoveryProfileComboBox")).SelectedIndex = 0;
                    WaitFor(() => ((ButtonBase)window.FindName("ShellMonitoringStartButton")).IsEnabled);
                    ((ButtonBase)window.FindName("ShellMonitoringStartButton")).RaiseEvent(
                        new RoutedEventArgs(ButtonBase.ClickEvent));
                    WaitFor(() => monitoring.Targets != null);
                    CollectionAssert.AreEqual(new[] { a }, monitoring.Targets.Select(target => target.DeviceId).ToArray());

                    var disabled = "Опрос выключен политикой «Без опроса»";
                    foreach (var id in new[] { b, c })
                    {
                        var diagnostic = new DeviceDiagnostic(id, id.ToString("D"), null, null,
                            null, null, Array.Empty<InterfaceDiagnostic>(), "192.0.2.2");
                        Invoke(window, "SetInspectorDeviceAvailability", diagnostic);
                        Assert.AreEqual(disabled,
                            ((TextBlock)window.FindName("InspectorOperationalStatusText")).Text);
                    }

                    var equipment = (ItemsControl)window.FindName("EquipmentList");
                    var rowB = equipment.Items.Cast<object>().Single(row =>
                        (Guid?)row.GetType().GetProperty("DeviceId").GetValue(row) == b);
                    Assert.AreEqual(disabled, rowB.GetType().GetProperty("PollingText").GetValue(rowB));
                    Assert.IsFalse((bool)rowB.GetType().GetProperty("HasNoData").GetValue(rowB));
                    ((ButtonBase)window.FindName("EquipmentFilterNoDataButton")).RaiseEvent(
                        new RoutedEventArgs(ButtonBase.ClickEvent));
                    Pump();
                    Assert.IsFalse(equipment.Items.Cast<object>().Any(row =>
                        (Guid?)row.GetType().GetProperty("DeviceId").GetValue(row) == b));

                    monitoring.Publish(new MonitoringTargetOutcome(b, IPAddress.Parse("192.0.2.2"),
                        Now, false, false, 2, null));
                    Pump();
                    var merged = (TopologyAlertSnapshot)Invoke(window, "MergeMonitoringAlerts", monitoring.Current);
                    Assert.IsFalse(merged.Alerts.Any(alert => alert.Kind == TopologyAlertKind.DeviceUnreachable));

                    store.Assign(PollingPolicySubjectKind.Device, a, off.Id, Now);
                    Invoke(window, "LoadPollingPolicyResolver");
                    var args = new object[] { null, null, null };
                    Assert.IsFalse((bool)Invoke(window, "TryBuildMonitoringTargetSetRequest", args));
                    Assert.AreEqual("Нет устройств для опроса: у всех опрос выключен политикой", args[2]);
                }
                catch (Exception error) { failure = error; }
                finally
                {
                    window?.Close();
                    CultureInfo.CurrentUICulture = previousCulture;
                    SQLiteConnection.ClearAllPools();
                    if (Directory.Exists(directory)) Directory.Delete(directory, true);
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            if (!thread.Join(TimeSpan.FromSeconds(20))) Assert.Fail("STA WPF test timed out.");
            if (failure != null) throw failure;
        }

        private static MainWindow CreateWindow(Guid[] ids, RecordingMonitoringControl monitoring)
        {
            var nodes = ids.Select((id, index) => new MapNode(id.ToString("D"),
                "Switch " + index, null, 100 + index * 200, 100, null, MapNodeOrigin.Automatic,
                MapMonitoringCapability.Unknown, MapNodeCategory.Unknown, id, "192.0.2." + (index + 1))).ToArray();
            var devices = ids.Select((id, index) => new DeviceDiagnostic(id, "Switch " + index,
                null, null, null, null, Array.Empty<InterfaceDiagnostic>(),
                "192.0.2." + (index + 1))).ToArray();
            var snapshot = new TopologyRefreshSnapshot(
                new MapSnapshot(Now, nodes, Array.Empty<MapLink>()),
                new TopologyAlertSnapshot(Now, "cist", Array.Empty<TopologyAlert>()),
                new NetworkDiagnosticSnapshot(Now, devices, Array.Empty<PhysicalLinkDiagnostic>()));
            var profile = new AccessProfile(Guid.NewGuid(), "Test profile", true, SnmpVersion.V2C, null);
            return new MainWindow(new FixedProvider(snapshot), new EmptyLookup(), monitoring,
                new[] { profile });
        }

        private static void InsertLocation(SQLiteConnection connection, Guid id)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "INSERT INTO locations (location_id, name, created_utc, updated_utc) VALUES (@id, 'Room', @now, @now);";
                command.Parameters.AddWithValue("@id", id.ToString("D"));
                command.Parameters.AddWithValue("@now", Now.ToString("o"));
                command.ExecuteNonQuery();
            }
        }

        private static void InsertDevice(SQLiteConnection connection, Guid id, Guid? location)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "INSERT INTO devices (id, location_id, created_at_utc, updated_at_utc) VALUES (@id, @location, @now, @now);";
                command.Parameters.AddWithValue("@id", id.ToString("D"));
                command.Parameters.AddWithValue("@location", location.HasValue ? (object)location.Value.ToString("D") : DBNull.Value);
                command.Parameters.AddWithValue("@now", Now.ToString("o"));
                command.ExecuteNonQuery();
            }
        }

        private static object Invoke(MainWindow window, string method, params object[] args) =>
            typeof(MainWindow).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(window, args);

        private static void WaitFor(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (!condition())
            {
                if (DateTime.UtcNow >= deadline) Assert.Fail("Expected WPF state was not reached.");
                Pump();
                Thread.Sleep(10);
            }
        }

        private static void Pump()
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,
                new DispatcherOperationCallback(state =>
                {
                    ((DispatcherFrame)state).Continue = false;
                    return null;
                }), frame);
            Dispatcher.PushFrame(frame);
        }

        private sealed class FixedProvider : ITopologyRefreshSnapshotProvider
        {
            private readonly TopologyRefreshSnapshot _snapshot;
            public FixedProvider(TopologyRefreshSnapshot snapshot) { _snapshot = snapshot; }
            public TopologyRefreshSnapshot GetSnapshot(string stpInstanceId) => _snapshot;
        }

        private sealed class EmptyLookup : IMacIpLookupReader
        {
            public MacIpLookupResult FindByMac(string address, int limit) =>
                new MacIpLookupResult(MacIpLookupKind.Mac, address, Array.Empty<MacIpLookupCandidate>());
            public MacIpLookupResult FindByIp(string address, int limit) =>
                new MacIpLookupResult(MacIpLookupKind.Ip, address, Array.Empty<MacIpLookupCandidate>());
        }

        private sealed class RecordingMonitoringControl : IMultiTargetMonitoringControl
        {
            public MonitoringControlSnapshot Current { get; private set; } =
                new MonitoringControlSnapshot(MonitoringControlState.Stopped, null, null, null);
            public IReadOnlyList<MonitoringTarget> Targets { get; private set; }
            public event EventHandler<MonitoringControlSnapshotChangedEventArgs> SnapshotChanged;
            public Task StartSetAsync(IReadOnlyList<MonitoringTarget> targets, MonitoringSessionPolicy policy,
                MonitoringTargetSetPolicy setPolicy, CancellationToken cancellationToken)
            {
                Targets = targets;
                Set(new MonitoringControlSnapshot(MonitoringControlState.Running, null, null, null));
                return Task.CompletedTask;
            }
            public Task StartAsync(MonitoringTarget target, MonitoringSessionPolicy policy,
                CancellationToken cancellationToken) => throw new InvalidOperationException();
            public Task StopAsync(CancellationToken cancellationToken)
            {
                Set(new MonitoringControlSnapshot(MonitoringControlState.Stopped, null, null, null));
                return Task.CompletedTask;
            }
            public Task PollNowAsync(MonitoringTarget target, MonitoringSessionPolicy policy,
                CancellationToken cancellationToken) => Task.CompletedTask;
            public void Publish(MonitoringTargetOutcome outcome) => Set(new MonitoringControlSnapshot(
                MonitoringControlState.Running, null, null, null, targetOutcomes: new[] { outcome }));
            private void Set(MonitoringControlSnapshot snapshot)
            {
                Current = snapshot;
                SnapshotChanged?.Invoke(this, new MonitoringControlSnapshotChangedEventArgs(snapshot));
            }
        }
    }
}
