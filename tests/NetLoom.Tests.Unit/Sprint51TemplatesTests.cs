using System;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.PollingPolicies;
using NetLoom.Application.Snmp;
using NetLoom.Domain.Access;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class Sprint51TemplatesTests
    {
        [TestMethod]
        public void CatalogContainsFiveSecretFreeTemplates()
        {
            CollectionAssert.AreEqual(new[] { "secure-snmpv3", "industrial-v2c" },
                PollingTemplates.AccessProfileTemplates.Select(item => item.Key).ToArray());
            CollectionAssert.AreEqual(new[] { "one-time-audit", "scheduled-topology", "no-active-polling" },
                PollingTemplates.PollingPolicyTemplates.Select(item => item.Key).ToArray());
            Assert.AreEqual(SnmpVersion.V3, PollingTemplates.SecureSnmpV3.Version);
            Assert.AreEqual(SnmpSecurityLevel.AuthPriv, PollingTemplates.SecureSnmpV3.SecurityLevel);
            Assert.AreEqual(SnmpAuthenticationProtocol.Sha256, PollingTemplates.SecureSnmpV3.AuthenticationProtocol);
            Assert.AreEqual(SnmpPrivacyProtocol.Aes, PollingTemplates.SecureSnmpV3.PrivacyProtocol);
            Assert.AreEqual(3000, PollingTemplates.SecureSnmpV3.TimeoutMilliseconds);
            Assert.AreEqual(1, PollingTemplates.SecureSnmpV3.RetryCount);
            Assert.AreEqual(SnmpVersion.V2C, PollingTemplates.IndustrialV2c.Version);
            Assert.IsNull(PollingTemplates.IndustrialV2c.SecurityLevel);
            Assert.AreEqual(5000, PollingTemplates.IndustrialV2c.TimeoutMilliseconds);
            Assert.AreEqual(2, PollingTemplates.IndustrialV2c.RetryCount);
            Assert.AreEqual(PollingScheduleMode.Once, PollingTemplates.OneTimeAudit.StateSchedule.Mode);
            Assert.AreEqual(PollingScheduleMode.Once, PollingTemplates.OneTimeAudit.TopologySchedule.Mode);
            CollectionAssert.AreEqual(new[] { 22, 23, 80, 443 }, PollingTemplates.OneTimeAudit.TcpPorts.ToArray());
            Assert.AreEqual(PollingScheduleMode.General, PollingTemplates.ScheduledTopology.StateSchedule.Mode);
            Assert.AreEqual(3600, PollingTemplates.ScheduledTopology.TopologySchedule.IntervalSeconds);
            Assert.IsFalse(PollingTemplates.NoActivePolling.ActivePolling);
            Assert.AreEqual(PollingScheduleMode.Off, PollingTemplates.NoActivePolling.StateSchedule.Mode);
            Assert.AreEqual(0, PollingTemplates.NoActivePolling.TcpPorts.Count);
            foreach (var type in new[] { typeof(AccessProfileTemplate), typeof(PollingPolicyTemplate) })
            {
                foreach (var member in type.GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(member => member.MemberType == MemberTypes.Field || member.MemberType == MemberTypes.Property))
                {
                    if (member.Name == "Key" || member.Name.Contains("<Key>")) continue;
                    Assert.IsFalse(new[] { "Password", "Community", "Secret", "Credential", "Key" }
                        .Any(word => member.Name.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0), member.Name);
                }
            }
            foreach (var template in PollingTemplates.AccessProfileTemplates)
                Assert.IsNotNull(template.Key);
            foreach (var template in PollingTemplates.PollingPolicyTemplates)
            {
                var policy = template.CreatePolicy(Guid.NewGuid(), "From template");
                Assert.IsFalse(policy.IsDefault);
                Assert.AreEqual(template.ActivePolling, policy.ActivePolling);
                Assert.AreEqual(template.StateSchedule, policy.StateSchedule);
                Assert.AreEqual(template.TopologySchedule, policy.TopologySchedule);
                CollectionAssert.AreEqual(template.TcpPorts.ToArray(), policy.TcpPorts.ToArray());
            }
        }
    }
}
