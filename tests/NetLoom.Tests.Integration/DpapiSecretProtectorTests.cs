using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetLoom.Persistence.Sqlite.Security;

namespace NetLoom.Tests.Integration
{
    [TestClass]
    public sealed class DpapiSecretProtectorTests
    {
        [TestMethod]
        public void ProtectAndUnprotectPreservesSecret()
        {
            var protector = new DpapiSecretProtector();
            var plaintext = Encoding.UTF8.GetBytes("test-secret-value");

            var encrypted = protector.Protect(plaintext);
            var decrypted = protector.Unprotect(encrypted);

            Assert.IsFalse(plaintext.SequenceEqual(encrypted));
            CollectionAssert.AreEqual(plaintext, decrypted);
        }
    }
}
