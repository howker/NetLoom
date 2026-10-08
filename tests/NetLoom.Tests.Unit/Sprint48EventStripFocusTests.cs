using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.TopologyRefresh;
using NetLoom.Contracts.Alerts;
using NetLoom.Wpf;

namespace NetLoom.Tests.Unit
{
    public sealed partial class Sprint46ShellFoundationTests
    {
        [TestMethod]
        public void EventStripKeepsKeyboardFocusWhenAlertsAreUnchanged()
        {
            RunEventStripFocusTest(
                (window, eventList, snapshot, button) =>
                {
                    var focusedElement = Keyboard.FocusedElement;
                    var container =
                        eventList.ItemContainerGenerator.ContainerFromIndex(0);
                    Assert.IsNotNull(container);

                    CaptureEventStripAlerts(window, snapshot.AlertSnapshot);
                    PumpDispatcher();

                    Assert.AreSame(focusedElement, Keyboard.FocusedElement);
                    Assert.AreSame(
                        container,
                        eventList.ItemContainerGenerator.ContainerFromIndex(0));
                });
        }

        [TestMethod]
        public void EventStripMovesFocusToAllEventsWhenTheFocusedEventDisappears()
        {
            RunEventStripFocusTest(
                (window, eventList, snapshot, button) =>
                {
                    var focusedRow = button.DataContext;
                    var alertKey = DiagnosticRowString(focusedRow, "AlertKey");
                    var title = DiagnosticRowString(focusedRow, "Title");
                    var previousRows = eventList.ItemsSource;

                    CaptureEventStripAlerts(
                        window,
                        new TopologyAlertSnapshot(
                            DateTime.UtcNow, "cist", new TopologyAlert[0]));
                    PumpDispatcher();

                    Assert.AreNotSame(previousRows, eventList.ItemsSource);
                    var focusedElement = Keyboard.FocusedElement;
                    var allEventsButton =
                        (Button)window.FindName("ShellAllEventsButton");
                    Assert.IsNotNull(focusedElement);
                    Assert.AreNotSame(window, focusedElement);
                    Assert.IsTrue(
                        eventList.IsKeyboardFocusWithin ||
                        ReferenceEquals(allEventsButton, focusedElement),
                        "Фокус должен оставаться в ленте или на кнопке «Все события».");

                    if (eventList.Items.Cast<object>().Any(
                            row => DiagnosticRowString(row, "AlertKey") == alertKey))
                    {
                        Assert.IsTrue(eventList.IsKeyboardFocusWithin);
                        var focusedButton = focusedElement as Button;
                        Assert.IsNotNull(focusedButton);
                        Assert.IsNotNull(focusedButton.DataContext);
                        Assert.AreEqual(
                            alertKey,
                            DiagnosticRowString(focusedButton.DataContext, "AlertKey"));
                        Assert.AreEqual(
                            title,
                            DiagnosticRowString(focusedButton.DataContext, "Title"));
                    }
                    else
                    {
                        Assert.AreSame(allEventsButton, focusedElement);
                    }
                });
        }

        private static void RunEventStripFocusTest(
            Action<MainWindow, ItemsControl, TopologyRefreshSnapshot, Button> check)
        {
            RunOnSta(
                () =>
                {
                    var snapshot = Pass2InspectorSnapshot(
                        out _, out _, out _, out _, out _);
                    var window = new MainWindow(
                        new FixedRefreshProvider(snapshot), new EmptyLookupReader())
                    {
                        Width = 1440,
                        Height = 900
                    };

                    try
                    {
                        var timerField = typeof(MainWindow).GetField(
                            "_refreshTimer", BindingFlags.Instance | BindingFlags.NonPublic);
                        Assert.IsNotNull(timerField);
                        var refreshTimer = (DispatcherTimer)timerField.GetValue(window);
                        Assert.IsNotNull(refreshTimer);

                        // Останавливаем таймер после запуска в обработчике Loaded окна.
                        // Обновление предупреждений в тесте вызывается только явно.
                        window.Loaded += (sender, args) => refreshTimer.Stop();
                        window.Show();
                        PumpDispatcher();
                        Assert.IsFalse(refreshTimer.IsEnabled);

                        var eventList = (ItemsControl)window.FindName("ShellEventList");
                        Assert.IsNotNull(eventList);
                        WaitForCondition(
                            () => FindVisibleEventStripButton(eventList) != null);
                        var button = FindVisibleEventStripButton(eventList);
                        Assert.IsNotNull(button);
                        button.Focus();

                        if (!ReferenceEquals(button, Keyboard.FocusedElement))
                        {
                            window.Activate();
                            PumpDispatcher();
                            button.Focus();
                        }

                        Assert.AreSame(
                            button,
                            Keyboard.FocusedElement,
                            "Окно должно получить настоящий фокус клавиатуры для проверки ленты.");
                        check(window, eventList, snapshot, button);
                    }
                    finally
                    {
                        window.Close();
                        PumpDispatcher();
                    }
                });
        }

        private static void CaptureEventStripAlerts(
            MainWindow window,
            TopologyAlertSnapshot snapshot)
        {
            var capture = typeof(MainWindow).GetMethod(
                "CaptureAlertEvents", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(capture);
            capture.Invoke(window, new object[] { snapshot });
        }

        private static Button FindVisibleEventStripButton(DependencyObject root)
        {
            for (var index = 0;
                 index < VisualTreeHelper.GetChildrenCount(root);
                 index++)
            {
                var child = VisualTreeHelper.GetChild(root, index);
                var button = child as Button;

                if (button != null && button.IsVisible && button.ActualWidth > 0)
                {
                    return button;
                }

                var nested = FindVisibleEventStripButton(child);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }
    }
}
