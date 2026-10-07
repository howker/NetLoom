using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Application.Lookup;

namespace NetLoom.Tests.Unit
{
    // Полевая проверка Sprint 46, P3: поиск по началу MAC-адреса (макет netloom-v2-7: «00:90:e8:38»).
    [TestClass]
    public sealed class FieldCheckLookupTests
    {
        [TestMethod]
        public void MacPrefixNeedsSeparatorAndFourToElevenHexDigits()
        {
            Assert.AreEqual("0090E838", AddressTextNormalizer.NormalizeMacPrefixCompact("00:90:e8:38"));
            Assert.AreEqual("0090E8", AddressTextNormalizer.NormalizeMacPrefixCompact("00-90-E8"));
            Assert.AreEqual("0090E83", AddressTextNormalizer.NormalizeMacPrefixCompact("0090.e83"));

            // Имя устройства без разделителя, слишком короткая часть и полный адрес частью не считаются.
            Assert.IsNull(AddressTextNormalizer.NormalizeMacPrefixCompact("beef01"));
            Assert.IsNull(AddressTextNormalizer.NormalizeMacPrefixCompact("ps1-sw"));
            Assert.IsNull(AddressTextNormalizer.NormalizeMacPrefixCompact("00:9"));
            Assert.IsNull(AddressTextNormalizer.NormalizeMacPrefixCompact("00:90:e8:38:ca:95"));
        }

        [TestMethod]
        public void SearchServiceSendsMacPrefixToPrefixReader()
        {
            var reader = new RecordingReader();

            var result =
                new MacIpLookupSearchService(reader)
                    .Search("00:90:e8:38", 50);

            Assert.AreEqual("0090E838", reader.Prefix);
            Assert.AreEqual(MacIpLookupKind.Mac, result.Kind);
        }

        private sealed class RecordingReader :
            IMacIpLookupReader,
            IMacPrefixLookupReader
        {
            public string Prefix { get; private set; }

            public MacIpLookupResult FindByMac(string macAddress, int maxCandidates)
            {
                throw new InvalidOperationException("Full MAC lookup is not expected.");
            }

            public MacIpLookupResult FindByIp(string ipAddress, int maxCandidates)
            {
                throw new InvalidOperationException("IP lookup is not expected.");
            }

            public MacIpLookupResult FindByMacPrefix(string compactPrefix, int maxCandidates)
            {
                Prefix = compactPrefix;
                return new MacIpLookupResult(MacIpLookupKind.Mac, "00:90:E8:38", new List<MacIpLookupCandidate>());
            }
        }
    }
}
