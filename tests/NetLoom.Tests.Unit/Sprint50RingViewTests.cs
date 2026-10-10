using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Lookup;
using NetLoom.Application.TopologyRefresh;
using NetLoom.Contracts.Alerts;
using NetLoom.Contracts.Diagnostics;
using NetLoom.Contracts.Rings;
using NetLoom.Contracts.StpTree;
using NetLoom.Contracts.TopologyMap;
using NetLoom.Wpf;
using NetLoom.Wpf.Localization;

namespace NetLoom.Tests.Unit
{
    // Sprint 50: вид кольца — выбор через меню «Показать», карточку предупреждения и инспектор, инспектор кольца.
    [TestClass]
    public sealed class Sprint50RingViewTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        internal static readonly Guid R1 = Id(1);
        internal static readonly Guid R2 = Id(2);
        internal static readonly Guid R3 = Id(3);
        internal static readonly Guid Outside = Id(4);
        internal static readonly Guid C1 = Id(5);
        internal static readonly Guid C2 = Id(6);
        internal static readonly Guid S1 = Id(7);
        internal static readonly Guid L12 = Id(101);
        internal static readonly Guid L23 = Id(102);
        internal static readonly Guid L31 = Id(103);
        internal static readonly Guid LOutside = Id(104);
        internal static readonly Guid LC1S = Id(105);
        internal static readonly Guid LSC2 = Id(106);
        internal static readonly Guid LCC = Id(107);
        internal static readonly Guid PortS1 = Id(201);

        internal const string SimpleRingKey = "ring-simple";
        internal const string CorePairRingKey = "ring-core-pair";

        private static Guid Id(int n)
        {
            return Guid.Parse("50505050-0006-0000-0000-" + n.ToString("D12"));
        }

        [TestMethod]
        public void RingFromEquipmentOpensMap()
        {
            WithWindow(Snapshot(), window =>
            {
                Click((Button)window.FindName("ShellEquipmentButton"));
                var list = (ItemsControl)window.FindName("EquipmentList");
                WaitForCondition(() => VisualButtons(list).Any(button => Equals(button.Tag, R1)));
                Click(VisualButtons(list).Single(button => Equals(button.Tag, R1)));
                Assert.AreEqual("Equipment", GetField(window, "_shellSection").ToString());
                var blocks = (ItemsControl)window.FindName("InspectorRingButtons");
                window.UpdateLayout();
                Click(VisualButtons(blocks).Single(button => Equals(button.Tag, SimpleRingKey)));
                Assert.AreEqual("Map", GetField(window, "_shellSection").ToString());
                Assert.IsTrue(((FrameworkElement)window.FindName("ShellMapSurface")).IsVisible);
                Assert.AreEqual(SimpleRingKey, GetField(window, "_selectedRingKey"));
                typeof(MainWindow).GetField("_hoveredPhysicalLinkId", PrivateInstance).SetValue(window, null);
                typeof(MainWindow).GetMethod("ReapplyOperationalFocusPresentation", PrivateInstance)
                    .Invoke(window, null);
                Assert.AreEqual(UiText.Get("InspectorEntityRing").ToUpper(CultureInfo.CurrentCulture),
                    Text(window, "InspectorEntityTypeText").ToUpper(CultureInfo.CurrentCulture));
            });
        }

        [TestMethod]
        public void RingMenuSelectsRingShowsInspectorDimsOutsideAndEscapeRestoresView()
        {
            WithWindow(Snapshot(), window =>
            {
                Assert.AreEqual("None", GetField(window, "_operationalFocusMode").ToString());

                // Пункт «Кольца» стоит после «Окрестности» и разделителя, перед «Все проблемы».
                var show = (Button)window.FindName("MapOperationalFocusButton");
                show.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var items = show.ContextMenu.Items.Cast<object>().ToArray();
                show.ContextMenu.IsOpen = false;
                var neighborhoodIndex = Array.FindIndex(items, item => item is MenuItem menu &&
                    AutomationProperties.GetName(menu) == UiText.Get("MapNeighborhoodMenu"));
                var ringsIndex = Array.FindIndex(items, item => item is MenuItem menu &&
                    AutomationProperties.GetName(menu) == UiText.Get("RingMenu"));
                var allProblemsIndex = Array.FindIndex(items, item => item is MenuItem menu &&
                    Equals(menu.Header, UiText.Get("MapOperationalFocusAllProblems")));
                Assert.IsTrue(neighborhoodIndex >= 0 && ringsIndex > neighborhoodIndex + 1 && allProblemsIndex > ringsIndex);
                Assert.IsInstanceOfType(items[neighborhoodIndex + 1], typeof(Separator));

                SelectRingFromMenu(window, SimpleLabel());

                Assert.AreEqual(SimpleRingKey, GetField(window, "_selectedRingKey"));
                Assert.AreEqual("Ring", GetField(window, "_operationalFocusMode").ToString());
                Assert.AreEqual(UiText.Get("InspectorEntityRing").ToUpper(CultureInfo.CurrentCulture), Text(window, "InspectorEntityTypeText").ToUpper(CultureInfo.CurrentCulture));
                Assert.AreEqual(SimpleTitle(), Text(window, "DiagnosticElementTitleText"));
                Assert.AreEqual(UiText.Get("RingKindSimple"), Text(window, "DiagnosticElementSubtitleText"));
                Assert.AreEqual(UiText.Get("RingStatusDegraded"), Text(window, "InspectorProblemText"));
                var fields = Fields(window);
                Assert.AreEqual(UiText.Get("RingStatusDegraded"), fields[UiText.Get("RingFieldStatus")]);
                Assert.AreEqual("ring-sw-1", fields[UiText.Get("RingFieldRoot")]);
                Assert.AreEqual(UiText.Get("RingBlockedPortNone"), fields[UiText.Get("RingFieldBlockedPort")]);
                Assert.AreEqual(UiText.FormatCount("DiagnosticDeviceCount", 3), fields[UiText.Get("RingFieldMembers")]);
                Assert.IsFalse(fields.ContainsKey(UiText.Get("RingFieldCores")));
                Assert.AreEqual(Visibility.Collapsed, ((TabItem)window.FindName("InspectorInterfacesTab")).Visibility);
                Assert.AreEqual(Visibility.Collapsed, ((TabItem)window.FindName("InspectorLinksTab")).Visibility);
                Assert.AreEqual(Visibility.Collapsed, ((TabItem)window.FindName("InspectorEvidenceTab")).Visibility);

                var members = ((ItemsControl)window.FindName("DiagnosticSecondaryList")).Items.Cast<object>()
                    .Select(row => (string)row.GetType().GetProperty("Text").GetValue(row)).ToArray();
                CollectionAssert.AreEqual(new[] { "ring-sw-1", "ring-sw-2", "ring-sw-3" }, members);

                Assert.AreEqual(UiText.Get("MapOperationalFocusRingShow"), show.Content);

                // Реальный указатель может стоять над окном теста — убираем случайное наведение на связь.
                typeof(MainWindow).GetField("_hoveredPhysicalLinkId", PrivateInstance).SetValue(window, null);
                typeof(MainWindow).GetMethod("ReapplyOperationalFocusPresentation", PrivateInstance, null,
                    Type.EmptyTypes, null).Invoke(window, null);

                // Связи кольца не приглушены, связь и устройство вне кольца приглушены.
                foreach (var linkId in new[] { L12, L23, L31 })
                {
                    Assert.IsTrue(LineById(window, linkId).Opacity > 0.5, "Ring link must stay opaque.");
                }
                Assert.IsTrue(LineById(window, LOutside).Opacity < 0.2, "Outside link must be dimmed.");
                Assert.IsTrue(DeviceBorder(window, R1).Opacity > 0.5);
                Assert.IsTrue(DeviceBorder(window, Outside).Opacity < 0.2, "Outside device must be dimmed. opacity=" + DeviceBorder(window, Outside).Opacity + " selected=" + GetField(window, "_selectedDeviceId") + " highlighted=" + GetField(window, "_highlightedDeviceId") + " mode=" + GetField(window, "_operationalFocusMode") + " inFocus=" + ((System.Collections.Generic.HashSet<Guid>)GetField(window, "_operationalFocusDeviceIds")).Contains(Outside));

                // Esc возвращает прежний вид и режим.
                var escape = PressKey(window, Key.Escape);
                Assert.IsTrue(escape.Handled);
                Assert.AreEqual("None", GetField(window, "_operationalFocusMode").ToString());
                Assert.IsNull(GetField(window, "_selectedRingKey"));
                Assert.IsTrue(LineById(window, LOutside).Opacity > 0.5);
            });
        }

        [TestMethod]
        public void CorePairRingShowsCoresBlockedPortAndNoSelectionOfOtherObjectKeepsRingMode()
        {
            WithWindow(Snapshot(), window =>
            {
                SelectRingFromMenu(window, CorePairLabel());
                var fields = Fields(window);
                Assert.AreEqual(UiText.Get("RingStatusProtected"), fields[UiText.Get("RingFieldStatus")]);
                Assert.AreEqual(UiText.Format("RingCoresJoin", "core-sw-1", "core-sw-2"), fields[UiText.Get("RingFieldCores")]);
                Assert.AreEqual("access-sw-1 Gi0/2", fields[UiText.Get("RingFieldBlockedPort")]);
                Assert.IsFalse(fields.ContainsKey(UiText.Get("RingFieldLastTopologyChange")));
                var members = ((ItemsControl)window.FindName("DiagnosticSecondaryList")).Items.Cast<object>()
                    .Select(row => (string)row.GetType().GetProperty("Text").GetValue(row)).ToArray();
                CollectionAssert.AreEqual(new[]
                {
                    "access-sw-1",
                    UiText.Format("RingMemberCore", "core-sw-1"),
                    UiText.Format("RingMemberCore", "core-sw-2")
                }, members);

                // Нейтральное состояние окрашено основным цветом текста, не зелёным.
                var problem = (TextBlock)window.FindName("InspectorProblemText");
                Assert.AreEqual(((SolidColorBrush)window.FindResource("NetLoom.Brush.TextPrimary")).Color,
                    ((SolidColorBrush)problem.Foreground).Color);

                // Выбор устройства снимает выбор кольца, но не режим «Кольцо».
                SelectDevice(window, C1);
                Assert.IsNull(GetField(window, "_selectedRingKey"));
                Assert.AreEqual("Ring", GetField(window, "_operationalFocusMode").ToString());
                Assert.AreEqual(UiText.Get("InspectorEntityDevice").ToUpper(CultureInfo.CurrentCulture), Text(window, "InspectorEntityTypeText").ToUpper(CultureInfo.CurrentCulture));
            });
        }

        [TestMethod]
        public void AlertCardShowOnMapSelectsItsRing()
        {
            WithWindow(Snapshot(withAlert: true), window =>
            {
                Click((Button)window.FindName("ShellAlertsButton"));
                WaitForCondition(() => Equals(GetField(window, "_selectedRingKey"), SimpleRingKey));

                // Другой выбор снимает кольцо; «Показать на карте» у карточки выбирает его снова.
                SelectDevice(window, Outside);
                Assert.IsNull(GetField(window, "_selectedRingKey"));
                window.UpdateLayout();
                var alerts = (ItemsControl)window.FindName("AlertList");
                var button = VisualButtons(alerts).First(item =>
                    Equals(item.Content, UiText.Get("AlertShowOnMapAction")));
                Click(button);
                Assert.AreEqual(SimpleRingKey, GetField(window, "_selectedRingKey"));
                Assert.AreEqual("Ring", GetField(window, "_operationalFocusMode").ToString());
                Assert.AreEqual(UiText.Get("InspectorEntityRing").ToUpper(CultureInfo.CurrentCulture), Text(window, "InspectorEntityTypeText").ToUpper(CultureInfo.CurrentCulture));
            });
        }

        [TestMethod]
        public void DeviceAndLinkInspectorOfferRingButtonsAndOutsiderHasNone()
        {
            WithWindow(Snapshot(), window =>
            {
                var blocks = (ItemsControl)window.FindName("InspectorRingButtons");
                SelectDevice(window, R1);
                window.UpdateLayout();
                Assert.IsTrue(blocks.IsVisible);
                var buttons = VisualButtons(blocks).ToArray();
                Assert.AreEqual(1, buttons.Length);
                Assert.AreEqual(SimpleRingKey, buttons[0].Tag);
                Assert.AreEqual(SimpleLabel(), AutomationProperties.GetName(buttons[0]));
                Assert.AreEqual(UiText.Get("InspectorRingsTitle").ToUpper(CultureInfo.CurrentCulture), Text(window, "InspectorRingsTitleText").ToUpper(CultureInfo.CurrentCulture));

                Click(buttons[0]);
                Assert.AreEqual(SimpleRingKey, GetField(window, "_selectedRingKey"));
                Assert.AreEqual(UiText.Get("InspectorEntityRing").ToUpper(CultureInfo.CurrentCulture), Text(window, "InspectorEntityTypeText").ToUpper(CultureInfo.CurrentCulture));
                Assert.AreEqual(Visibility.Collapsed, blocks.Visibility);

                SelectDevice(window, Outside);
                window.UpdateLayout();
                Assert.AreEqual(Visibility.Collapsed, blocks.Visibility);
                Assert.AreEqual(0, VisualButtons(blocks).Count());

                SelectLink(window, L12);
                window.UpdateLayout();
                Assert.IsTrue(blocks.IsVisible);
                Assert.AreEqual(1, VisualButtons(blocks).Count());

                SelectLink(window, LOutside);
                window.UpdateLayout();
                Assert.AreEqual(Visibility.Collapsed, blocks.Visibility);
            });
        }

        [TestMethod]
        public void UnresolvedRingSaysUndeterminedAndNeverUnprotected()
        {
            var snapshot = Snapshot(simpleStatus: RingProtectionStatus.Unresolved, simpleRoot: null,
                coreDesignatedRoot: "8000-aabbccddeeff", coreRoot: null);
            WithWindow(snapshot, window =>
            {
                SelectRingFromMenu(window, UnresolvedSimpleLabel());
                var fields = Fields(window);
                Assert.AreEqual(UiText.Get("RingStatusUnresolved"), fields[UiText.Get("RingFieldStatus")]);
                Assert.AreEqual(UiText.Get("RingStatusUnresolved"), fields[UiText.Get("RingFieldRoot")]);
                Assert.AreEqual(UiText.Get("RingBlockedPortUnknown"), fields[UiText.Get("RingFieldBlockedPort")]);
                AssertNoUnprotectedText(window);

                SelectRingFromMenu(window, CorePairLabel());
                fields = Fields(window);
                Assert.AreEqual(UiText.Format("RingRootOutside", "8000-aabbccddeeff"), fields[UiText.Get("RingFieldRoot")]);
            });
        }

        [TestMethod]
        public void LastTopologyChangeRowExistsOnlyWhenKnown()
        {
            WithWindow(Snapshot(), window =>
            {
                SelectRingFromMenu(window, SimpleLabel());
                Assert.IsTrue(Fields(window).ContainsKey(UiText.Get("RingFieldLastTopologyChange")));
                SelectRingFromMenu(window, CorePairLabel());
                Assert.IsFalse(Fields(window).ContainsKey(UiText.Get("RingFieldLastTopologyChange")));
            });
        }

        [TestMethod]
        public void RingMenuWithoutRingsHasOneDisabledItem()
        {
            WithWindow(Snapshot(withRings: false), window =>
            {
                var show = (Button)window.FindName("MapOperationalFocusButton");
                show.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var rings = show.ContextMenu.Items.OfType<MenuItem>()
                    .Single(item => AutomationProperties.GetName(item) == UiText.Get("RingMenu"));
                show.ContextMenu.IsOpen = false;
                var only = rings.Items.OfType<MenuItem>().Single();
                Assert.AreEqual(UiText.Get("RingMenuNone"), only.Header);
                Assert.IsFalse(only.IsEnabled);
            });
        }

        // Подписи колец: «{заголовок} · {состояние}».
        private static string SimpleTitle()
        {
            return UiText.Format("RingTitleWithLocation", "Цех 1");
        }

        private static string SimpleLabel()
        {
            return UiText.Format("RingMenuItem", SimpleTitle(), UiText.Get("RingStatusDegraded"));
        }

        private static string UnresolvedSimpleLabel()
        {
            return UiText.Format("RingMenuItem", SimpleTitle(), UiText.Get("RingStatusUnresolved"));
        }

        private static string CorePairLabel()
        {
            return UiText.Format("RingMenuItem",
                UiText.Format("RingTitleWithLocation", "Узел связи"), UiText.Get("RingStatusProtected"));
        }

        private static void AssertNoUnprotectedText(MainWindow window)
        {
            var unprotected = UiText.Get("RingStatusUnprotected");
            var texts = new List<string>
            {
                Text(window, "InspectorProblemText"),
                Text(window, "InspectorProblemExplanationText"),
                Text(window, "DiagnosticElementTitleText"),
                Text(window, "DiagnosticElementSubtitleText")
            };
            texts.AddRange(Fields(window).Values);
            Assert.IsFalse(texts.Any(text => text != null && text.IndexOf(unprotected, StringComparison.CurrentCulture) >= 0),
                "An unresolved ring must never be called unprotected.");
        }

        internal static TopologyRefreshSnapshot Snapshot(
            bool withAlert = false,
            bool withRings = true,
            RingProtectionStatus simpleStatus = RingProtectionStatus.Degraded,
            Guid? simpleRoot = null,
            string coreDesignatedRoot = null,
            Guid? coreRoot = null)
        {
            var now = DateTime.UtcNow;
            var root = simpleRoot ?? (simpleStatus == RingProtectionStatus.Unresolved ? (Guid?)null : R1);
            var coreRootId = coreDesignatedRoot == null ? (coreRoot ?? C1) : coreRoot;
            var devices = new[]
            {
                new { Id = R1, Name = "ring-sw-1", Location = "Цех 1", X = 100.0, Y = 100.0 },
                new { Id = R2, Name = "ring-sw-2", Location = "Цех 1", X = 360.0, Y = 100.0 },
                new { Id = R3, Name = "ring-sw-3", Location = "Цех 1", X = 230.0, Y = 300.0 },
                new { Id = Outside, Name = "outside-sw", Location = "Склад", X = 100.0, Y = 560.0 },
                new { Id = C1, Name = "core-sw-1", Location = "Узел связи", X = 760.0, Y = 100.0 },
                new { Id = C2, Name = "core-sw-2", Location = "Узел связи", X = 1020.0, Y = 100.0 },
                new { Id = S1, Name = "access-sw-1", Location = "Узел связи", X = 890.0, Y = 300.0 }
            };
            var nodes = devices.Select(device => new MapNode(device.Id.ToString("D"), device.Name, null,
                device.X, device.Y, category: MapNodeCategory.Switch, deviceId: device.Id)).ToArray();
            var names = devices.ToDictionary(device => device.Id, device => device.Name);

            var diagnosticLinks = new[]
            {
                Link(L12, R1, R2, names, StpTreePortState.Forwarding, StpTreePortState.Forwarding),
                Link(L23, R2, R3, names, StpTreePortState.Forwarding, StpTreePortState.Forwarding),
                Link(L31, R3, R1, names,
                    simpleStatus == RingProtectionStatus.Degraded ? StpTreePortState.Disabled : StpTreePortState.Forwarding,
                    simpleStatus == RingProtectionStatus.Degraded ? StpTreePortState.Disabled : StpTreePortState.Forwarding),
                Link(LOutside, R1, Outside, names, StpTreePortState.Forwarding, StpTreePortState.Forwarding),
                Link(LC1S, C1, S1, names, StpTreePortState.Forwarding, StpTreePortState.Forwarding),
                Link(LSC2, S1, C2, names, StpTreePortState.Blocking, StpTreePortState.Forwarding, PortS1),
                Link(LCC, C1, C2, names, StpTreePortState.Forwarding, StpTreePortState.Forwarding)
            };
            var mapLinks = diagnosticLinks.Select(link => new MapLink(link.PhysicalLinkId.ToString("D"),
                link.DeviceAId.ToString("D"), link.DeviceBId.ToString("D"), link.InterfaceAName, link.InterfaceBName,
                MapConfidence.High, MapFreshness.Fresh, new MapEvidenceItem[0], link.PhysicalLinkId)).ToArray();
            var diagnosticDevices = devices.Select(device => new DeviceDiagnostic(device.Id, device.Name, null,
                device.Location, now, now,
                device.Id == S1
                    ? new[]
                    {
                        new InterfaceDiagnostic(PortS1, S1, 2, "Gi0/2", null, "up", "up", 1000000000L, now,
                            StpTreePortState.Blocking, DiagnosticDegradationStatus.Unknown, null,
                            new DiagnosticDegradationReason[0])
                    }
                    : new InterfaceDiagnostic[0],
                "192.0.2." + (10 + Array.IndexOf(devices.Select(item => item.Id).ToArray(), device.Id)))).ToArray();

            var rings = withRings
                ? new[]
                {
                    new RingDiagnostic(SimpleRingKey, PhysicalRedundancyRegionKind.SimpleRing, simpleStatus,
                        new[] { R1, R2, R3 }, new[] { L12, L23, L31 }, new Guid[0], new Guid[0],
                        simpleStatus == RingProtectionStatus.Degraded ? new[] { L31 } : new Guid[0],
                        new Guid[0], new RingBlockedPort[0], root, null,
                        simpleStatus == RingProtectionStatus.Unresolved ? new[] { R2 } : new Guid[0],
                        now.AddMinutes(-5)),
                    new RingDiagnostic(CorePairRingKey, PhysicalRedundancyRegionKind.CorePairRing,
                        RingProtectionStatus.Protected, new[] { C1, C2, S1 }, new[] { LC1S, LSC2, LCC },
                        new[] { C1, C2 }, new[] { LSC2 }, new Guid[0], new Guid[0],
                        new[] { new RingBlockedPort(S1, PortS1, LSC2) }, coreRootId, coreDesignatedRoot,
                        new Guid[0], null)
                }
                : new RingDiagnostic[0];

            var alerts = withAlert
                ? new[]
                {
                    new TopologyAlert("ring-simple-alert", TopologyAlertKind.RingProtectionDegraded,
                        TopologyAlertSeverity.Warning, "cist", new[] { SimpleRingKey }, new[] { L31 },
                        new[] { TopologyAlertReason.DisabledRingLink })
                }
                : new TopologyAlert[0];

            return new TopologyRefreshSnapshot(new MapSnapshot(now, nodes, mapLinks),
                new TopologyAlertSnapshot(now, "cist", alerts),
                new NetworkDiagnosticSnapshot(now, diagnosticDevices, diagnosticLinks, rings));
        }

        private static PhysicalLinkDiagnostic Link(Guid id, Guid a, Guid b, IDictionary<Guid, string> names,
            StpTreePortState stpA, StpTreePortState stpB, Guid? interfaceA = null)
        {
            var now = DateTime.UtcNow;
            return new PhysicalLinkDiagnostic(id, a, b, interfaceA, null, names[a], names[b], "Gi0/1", "Gi0/2",
                DiagnosticLinkStrength.Confirmed, MapFreshness.Fresh, null, null, "STP", now, now, stpA, stpB,
                new DiagnosticEvidenceItem[0], false, 0, 0, 0);
        }

        private static void WithWindow(TopologyRefreshSnapshot snapshot, Action<MainWindow> action)
        {
            RunOnSta(() =>
            {
                Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("ru-RU");
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
                var window = new MainWindow(new FixedRefreshProvider(snapshot), new EmptyLookupReader());
                try
                {
                    window.Show();
                    WaitForCondition(() => DeviceBorder(window, R1) != null &&
                        LineById(window, L12) != null);
                    window.UpdateLayout();
                    action(window);
                }
                finally
                {
                    window.Close();
                    PumpDispatcher();
                }
            });
        }

        private static void SelectRingFromMenu(MainWindow window, string label)
        {
            var show = (Button)window.FindName("MapOperationalFocusButton");
            show.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var rings = show.ContextMenu.Items.OfType<MenuItem>()
                .Single(item => AutomationProperties.GetName(item) == UiText.Get("RingMenu"));
            show.ContextMenu.IsOpen = false;
            var item = rings.Items.OfType<MenuItem>().Single(candidate => Equals(candidate.Header, label));
            item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            PumpDispatcher();
            window.UpdateLayout();
        }

        private static Dictionary<string, string> Fields(MainWindow window)
        {
            var result = new Dictionary<string, string>();
            foreach (var row in ((ItemsControl)window.FindName("DiagnosticFieldsList")).Items)
            {
                var type = row.GetType();
                result[(string)type.GetProperty("Label").GetValue(row)] =
                    (string)type.GetProperty("Value").GetValue(row);
            }

            return result;
        }

        private static string Text(MainWindow window, string name)
        {
            return ((TextBlock)window.FindName(name)).Text;
        }

        private static object GetField(MainWindow window, string name)
        {
            return typeof(MainWindow).GetField(name, PrivateInstance).GetValue(window);
        }

        private static Line LineById(MainWindow window, Guid id)
        {
            return ((Canvas)window.FindName("MapCanvas")).Children.OfType<Line>()
                .FirstOrDefault(line => Equals(line.Tag, id));
        }

        private static Border DeviceBorder(MainWindow window, Guid id)
        {
            return ((Canvas)window.FindName("MapCanvas")).Children.OfType<Border>()
                .FirstOrDefault(border => border.Tag is Guid && (Guid)border.Tag == id);
        }

        private static void SelectDevice(MainWindow window, Guid id)
        {
            var border = DeviceBorder(window, id);
            Assert.IsNotNull(border);
            border.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
            { RoutedEvent = UIElement.MouseLeftButtonDownEvent, Source = border });
            border.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
            { RoutedEvent = UIElement.MouseLeftButtonUpEvent, Source = border });
            PumpDispatcher();
        }

        private static void SelectLink(MainWindow window, Guid id)
        {
            var line = LineById(window, id);
            Assert.IsNotNull(line);
            line.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
            { RoutedEvent = UIElement.MouseLeftButtonDownEvent, Source = line });
            PumpDispatcher();
        }

        private static KeyEventArgs PressKey(MainWindow window, Key key)
        {
            Keyboard.ClearFocus();
            var args = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window),
                Environment.TickCount, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
            window.RaiseEvent(args);
            PumpDispatcher();
            return args;
        }

        private static IEnumerable<Button> VisualButtons(DependencyObject root)
        {
            if (root is Button button)
            {
                yield return button;
            }

            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            {
                foreach (var child in VisualButtons(VisualTreeHelper.GetChild(root, index)))
                {
                    yield return child;
                }
            }
        }

        private static void Click(Button button)
        {
            Assert.IsNotNull(button);
            Assert.IsTrue(button.IsEnabled, "The production button must be enabled for this interaction.");
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            PumpDispatcher();
        }

        private static void RunOnSta(Action action)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception error)
                {
                    failure = error;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            if (!thread.Join(TimeSpan.FromSeconds(30)))
            {
                throw new AssertFailedException("STA WPF test did not complete.");
            }

            if (failure != null)
            {
                throw failure;
            }
        }

        private static void WaitForCondition(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            while (!condition())
            {
                if (DateTime.UtcNow >= deadline)
                {
                    Assert.Fail("The expected WPF state was not reached.");
                }

                PumpDispatcher();
                Thread.Sleep(10);
            }

            PumpDispatcher();
        }

        private static void PumpDispatcher()
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

        private sealed class FixedRefreshProvider : ITopologyRefreshSnapshotProvider
        {
            private readonly TopologyRefreshSnapshot _snapshot;

            public FixedRefreshProvider(TopologyRefreshSnapshot snapshot)
            {
                _snapshot = snapshot;
            }

            public TopologyRefreshSnapshot GetSnapshot(string stpInstanceId)
            {
                return _snapshot;
            }
        }

        private sealed class EmptyLookupReader : IMacIpLookupReader
        {
            public MacIpLookupResult FindByMac(string macAddress, int maxCandidates)
            {
                return new MacIpLookupResult(MacIpLookupKind.Mac, macAddress, new MacIpLookupCandidate[0]);
            }

            public MacIpLookupResult FindByIp(string ipAddress, int maxCandidates)
            {
                return new MacIpLookupResult(MacIpLookupKind.Ip, ipAddress, new MacIpLookupCandidate[0]);
            }
        }
    }
}
