using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Discovery;
using NetLoom.Application.Snmp;
using NetLoom.Domain.Access;
using NetLoom.Wpf;

namespace NetLoom.Tests.Unit
{
    internal static class Sprint48ProfileCheckDialogFixture
    {
        internal const string Community = "profile-check-fixture-secret";

        internal static SnmpProfileCheckReport Report(bool authenticationFailed = false)
        {
            return new SnmpProfileCheckReport(new[]
            {
                new SnmpProfileCheckItem(SnmpProfileCheckKind.Availability, SnmpProfileCheckStatus.Ok,
                    milliseconds: authenticationFailed ? 3 : 14),
                new SnmpProfileCheckItem(SnmpProfileCheckKind.System,
                    authenticationFailed ? SnmpProfileCheckStatus.Failed : SnmpProfileCheckStatus.Ok,
                    failure: authenticationFailed ? SnmpTransportFailure.Authentication : (SnmpTransportFailure?)null),
                new SnmpProfileCheckItem(SnmpProfileCheckKind.IfMib,
                    authenticationFailed ? SnmpProfileCheckStatus.NotChecked : SnmpProfileCheckStatus.Ok,
                    authenticationFailed ? (int?)null : 52),
                new SnmpProfileCheckItem(SnmpProfileCheckKind.LldpMib,
                    authenticationFailed ? SnmpProfileCheckStatus.NotChecked : SnmpProfileCheckStatus.Ok,
                    authenticationFailed ? (int?)null : 18),
                new SnmpProfileCheckItem(SnmpProfileCheckKind.BridgeMib,
                    authenticationFailed ? SnmpProfileCheckStatus.NotChecked : SnmpProfileCheckStatus.Ok),
                new SnmpProfileCheckItem(SnmpProfileCheckKind.QBridgeMib,
                    authenticationFailed ? SnmpProfileCheckStatus.NotChecked : SnmpProfileCheckStatus.Partial)
            });
        }

        internal static void Open(MainWindow owner, AccessProfile editingProfile, Action<Window> action)
        {
            Exception failure = null;
            owner.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                Window dialog = null;
                try
                {
                    dialog = owner.OwnedWindows.Cast<Window>().Single();
                    dialog.UpdateLayout();
                    action(dialog);
                }
                catch (Exception error) { failure = error; }
                finally { dialog?.Close(); }
            }));
            var method = typeof(MainWindow).GetMethod("ShowDiscoveryProfileDialog", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            method.Invoke(owner, new object[] { editingProfile });
            if (failure != null) throw failure;
        }

        internal static IEnumerable<DependencyObject> Visuals(DependencyObject root)
        {
            if (root == null) yield break;
            yield return root;
            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
                foreach (var child in Visuals(VisualTreeHelper.GetChild(root, index))) yield return child;
        }

        internal static T Named<T>(Window dialog, string name) where T : FrameworkElement
        {
            return Visuals(dialog).OfType<T>().Single(element => element.Name == name);
        }
    }
}
