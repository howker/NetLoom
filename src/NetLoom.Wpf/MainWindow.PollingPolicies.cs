using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using NetLoom.Application.PollingPolicies;
using NetLoom.Contracts.TopologyMap;
using NetLoom.Wpf.Localization;

namespace NetLoom.Wpf;

public partial class MainWindow
{
    private IPollingPolicyStore _pollingPolicyStore;
    private PollingPolicyResolver _pollingPolicyResolver = DefaultPollingPolicyResolver();

    public IPollingPolicyStore PollingPolicyStore
    {
        get => _pollingPolicyStore;
        set
        {
            _pollingPolicyStore = value;
            LoadPollingPolicyResolver();
        }
    }

    private static PollingPolicyResolver DefaultPollingPolicyResolver() =>
        new PollingPolicyResolver(
            new[] { PollingPolicy.CreateDefault() },
            Array.Empty<PollingPolicyAssignment>(),
            new Dictionary<Guid, Guid?>(),
            new Dictionary<Guid, Guid?>());

    private void LoadPollingPolicyResolver()
    {
        try
        {
            _pollingPolicyResolver = _pollingPolicyStore?.LoadResolver() ?? DefaultPollingPolicyResolver();
        }
        catch (Exception error)
        {
            Trace.TraceError(error.ToString());
            _pollingPolicyResolver = DefaultPollingPolicyResolver();
        }
    }

    private PollingPolicy EffectivePollingPolicy(Guid deviceId) =>
        _pollingPolicyResolver.ResolveDevice(deviceId).Policy;

    private string PollingPolicyDisplayName(PollingPolicy policy) =>
        policy.IsDefault ? UiText.Get("PollingPolicyDefaultName") : policy.Name;

    private string PollingDisabledText(Guid deviceId, MapNode node)
    {
        var policy = EffectivePollingPolicy(deviceId);
        if (!policy.ActivePolling)
            return UiText.Format("PollingDisabledByPolicy", PollingPolicyDisplayName(policy));
        return node != null && node.Origin != MapNodeOrigin.Manual &&
               node.MonitoringCapability == MapMonitoringCapability.None
            ? UiText.Get("PollingDisabledUnmanaged")
            : null;
    }

    // Экран политик вызовет этот метод после сохранения назначений.
    internal async Task OnPollingPoliciesChangedAsync()
    {
        LoadPollingPolicyResolver();
        if (_lastTopologyAlertSnapshot != null)
            ApplyMonitoringAlertsIfChanged(_monitoringControl.Current);
        ShowSelectedDiagnostic();
        UpdateShellEquipmentPresentation(_lastMapSnapshot);
        UpdateMonitoringPresentation(_monitoringControl.Current);
        await RestartMonitoringTargetSetAsync();
    }
}
