using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NetLoom.Application.Discovery;
using NetLoom.Application.Snmp;
using NetLoom.Desktop.Monitoring;
using NetLoom.Domain.Access;

namespace NetLoom.Desktop.Discovery
{
    internal sealed class DesktopEngineProfileCheck : IDisposable
    {
        private readonly string _engineExecutablePath;
        private readonly IEngineProcessFactory _processFactory;
        private readonly IDiscoveryProcessEnvironmentProvider _environmentProvider;
        private readonly TimeSpan _timeout;

        public DesktopEngineProfileCheck(string engineExecutablePath,
            IDiscoveryProcessEnvironmentProvider environmentProvider)
            : this(engineExecutablePath, new DesktopEngineProcessFactory(), environmentProvider)
        {
        }

        internal DesktopEngineProfileCheck(string engineExecutablePath, IEngineProcessFactory processFactory,
            IDiscoveryProcessEnvironmentProvider environmentProvider, TimeSpan? timeout = null)
        {
            if (string.IsNullOrWhiteSpace(engineExecutablePath)) throw new ArgumentException("ENGINE_EXECUTABLE_PATH_REQUIRED");
            _engineExecutablePath = engineExecutablePath;
            _processFactory = processFactory ?? throw new ArgumentNullException(nameof(processFactory));
            _environmentProvider = environmentProvider ?? throw new ArgumentNullException(nameof(environmentProvider));
            _timeout = timeout ?? TimeSpan.FromSeconds(30);
        }

        public static IReadOnlyList<string> BuildTokens(IPAddress address, SnmpVersion version)
        {
            if (address == null) throw new ArgumentNullException(nameof(address));
            return new[]
            {
                "check-profile", "--address", address.ToString(), "--port", "161", "--version", version.ToString(),
                "--timeout-ms", "750", "--retries", "0", "--max-repetitions", "10", "--icmp-timeout-ms", "500"
            };
        }

        public async Task<SnmpProfileCheckReport> CheckAsync(IPAddress address, SnmpVersion version,
            byte[] communityUtf8, Guid? profileId)
        {
            var gate = new object();
            var items = new Dictionary<SnmpProfileCheckKind, SnmpProfileCheckItem>();
            var completed = false;
            var protocolFailed = false;
            var failure = SnmpTransportFailure.Protocol;
            IEngineProcessSession process = null;
            Action<string> output = line =>
            {
                if (line == null || !line.StartsWith(EngineProfileCheckMarkerParser.Prefix, StringComparison.Ordinal)) return;
                SnmpProfileCheckItem item;
                bool terminal;
                lock (gate)
                {
                    if (!EngineProfileCheckMarkerParser.TryParse(line, out item, out terminal) || completed)
                    {
                        protocolFailed = true;
                        return;
                    }
                    if (terminal) completed = true;
                    else if (items.ContainsKey(item.Kind)) protocolFailed = true;
                    else items.Add(item.Kind, item);
                }
            };
            try
            {
                using (var deadline = new CancellationTokenSource())
                {
                    var timeout = Task.Delay(_timeout, deadline.Token);
                    var environment = CreateEnvironment(version, communityUtf8, profileId);
                    process = _processFactory.Start(new EngineProcessStartRequest(_engineExecutablePath,
                        EngineDiscoveryCommandBuilder.FormatArguments(BuildTokens(address, version)), environment));
                    process.OutputLineReceived += output;
                    process.BeginRead();
                    var winner = await Task.WhenAny(process.Completion, timeout).ConfigureAwait(false);
                    if (winner == timeout)
                    {
                        failure = SnmpTransportFailure.Timeout;
                        process.Terminate();
                    }
                    else
                    {
                        deadline.Cancel();
                        var exitCode = await process.Completion.ConfigureAwait(false);
                        lock (gate)
                        {
                            if (exitCode == 0 && completed && !protocolFailed && items.Count == 6)
                                return Report(items, null);
                            if (exitCode != 0 || protocolFailed || !completed) items.Clear();
                        }
                    }
                }
            }
            catch
            {
                // Сообщения процесса и исключений не возвращаются в UI: они могут содержать секрет.
            }
            finally
            {
                if (communityUtf8 != null) Array.Clear(communityUtf8, 0, communityUtf8.Length);
                if (process != null)
                {
                    process.OutputLineReceived -= output;
                    try { process.Terminate(); } catch { }
                    try { process.Dispose(); } catch { }
                }
            }
            lock (gate) return Report(items, failure);
        }

        private IReadOnlyDictionary<string, string> CreateEnvironment(SnmpVersion version, byte[] communityUtf8, Guid? profileId)
        {
            if (communityUtf8 != null && communityUtf8.Length > 0)
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "NETLOOM_SNMP_COMMUNITY", Encoding.UTF8.GetString(communityUtf8) },
                    { "NETLOOM_SNMP_USERNAME", string.Empty },
                    { "NETLOOM_SNMP_AUTH_PROTOCOL", "None" },
                    { "NETLOOM_SNMP_AUTH_PASSWORD", string.Empty },
                    { "NETLOOM_SNMP_PRIVACY_PROTOCOL", "None" },
                    { "NETLOOM_SNMP_PRIVACY_PASSWORD", string.Empty },
                    { "NETLOOM_SNMP_CONTEXT", string.Empty }
                };
            }
            if (!profileId.HasValue) throw new InvalidOperationException("DISCOVERY_SNMP_COMMUNITY_REQUIRED");
            try
            {
                return _environmentProvider.CreateEnvironment(profileId.Value, version);
            }
            catch (InvalidOperationException error) when (version != SnmpVersion.V3
                && error.Message == "DISCOVERY_ACCESS_PROFILE_VERSION_MISMATCH")
            {
                // v1 и v2c используют один секрет: смену версии можно проверить до сохранения профиля.
                return _environmentProvider.CreateEnvironment(profileId.Value,
                    version == SnmpVersion.V1 ? SnmpVersion.V2C : SnmpVersion.V1);
            }
        }

        private static SnmpProfileCheckReport Report(IDictionary<SnmpProfileCheckKind, SnmpProfileCheckItem> items,
            SnmpTransportFailure? failure)
        {
            return new SnmpProfileCheckReport(Enum.GetValues(typeof(SnmpProfileCheckKind)).Cast<SnmpProfileCheckKind>()
                .Select(kind => items.ContainsKey(kind) ? items[kind]
                    : new SnmpProfileCheckItem(kind, SnmpProfileCheckStatus.Failed, failure: failure)));
        }

        public void Dispose()
        {
            _processFactory.Dispose();
        }
    }
}
