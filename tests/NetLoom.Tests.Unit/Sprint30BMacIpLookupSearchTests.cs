using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Lookup;

namespace NetLoom.Tests.Unit
{
    [TestClass]
    public sealed class
        Sprint30BMacIpLookupSearchTests
    {
        [TestMethod]
        public void SearchDetectsAndNormalizesIp()
        {
            var reader =
                new RecordingReader();

            var result =
                new MacIpLookupSearchService(
                    reader)
                    .Search(
                        " 192.0.2.25 ",
                        25);

            Assert.AreEqual(
                MacIpLookupKind.Ip,
                result.Kind);

            Assert.AreEqual(
                "192.0.2.25",
                reader.LastIp);

            Assert.AreEqual(
                25,
                reader.LastLimit);
        }

        [TestMethod]
        public void SearchDetectsAndNormalizesMac()
        {
            var reader =
                new RecordingReader();

            var result =
                new MacIpLookupSearchService(
                    reader)
                    .Search(
                        "00-11-22-33-44-55",
                        30);

            Assert.AreEqual(
                MacIpLookupKind.Mac,
                result.Kind);

            Assert.AreEqual(
                "00:11:22:33:44:55",
                reader.LastMac);

            Assert.AreEqual(
                30,
                reader.LastLimit);
        }

        [TestMethod]
        public void InvalidQueryDoesNotCallReader()
        {
            var reader =
                new RecordingReader();

            try
            {
                new MacIpLookupSearchService(
                    reader)
                    .Search(
                        "not-an-address",
                        10);

                Assert.Fail(
                    "Expected invalid query rejection.");
            }
            catch (ArgumentException)
            {
            }

            Assert.IsNull(reader.LastIp);
            Assert.IsNull(reader.LastMac);
        }

        private sealed class RecordingReader :
            IMacIpLookupReader
        {
            public string LastIp { get; private set; }

            public string LastMac { get; private set; }

            public int LastLimit { get; private set; }

            public MacIpLookupResult FindByMac(
                string macAddress,
                int maxCandidates)
            {
                LastMac = macAddress;
                LastLimit = maxCandidates;

                return new MacIpLookupResult(
                    MacIpLookupKind.Mac,
                    macAddress,
                    new MacIpLookupCandidate[0]);
            }

            public MacIpLookupResult FindByIp(
                string ipAddress,
                int maxCandidates)
            {
                LastIp = ipAddress;
                LastLimit = maxCandidates;

                return new MacIpLookupResult(
                    MacIpLookupKind.Ip,
                    ipAddress,
                    new MacIpLookupCandidate[0]);
            }
        }
    }
}