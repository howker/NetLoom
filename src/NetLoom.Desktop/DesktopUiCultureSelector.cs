using System;
using System.Globalization;
using System.Threading;

namespace NetLoom.Desktop
{
    internal static class DesktopUiCultureSelector
    {
        private const string CultureEnvironmentVariable =
            "NETLOOM_UI_CULTURE";

        private const string DefaultCultureName =
            "ru-RU";

        public static CultureInfo Apply()
        {
            var requested =
                Environment.GetEnvironmentVariable(
                    CultureEnvironmentVariable);

            var culture =
                Resolve(
                    requested);

            CultureInfo.DefaultThreadCurrentCulture =
                culture;

            CultureInfo.DefaultThreadCurrentUICulture =
                culture;

            Thread.CurrentThread.CurrentCulture =
                culture;

            Thread.CurrentThread.CurrentUICulture =
                culture;

            return culture;
        }

        internal static CultureInfo Resolve(
            string requested)
        {
            var normalized =
                string.IsNullOrWhiteSpace(
                    requested)
                    ? DefaultCultureName
                    : requested.Trim();

            if (string.Equals(
                    normalized,
                    "ru",
                    StringComparison.OrdinalIgnoreCase))
            {
                normalized =
                    "ru-RU";
            }
            else if (string.Equals(
                    normalized,
                    "en",
                    StringComparison.OrdinalIgnoreCase))
            {
                normalized =
                    "en-US";
            }

            if (!string.Equals(
                    normalized,
                    "ru-RU",
                    StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(
                    normalized,
                    "en-US",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "Unsupported UI culture. " +
                    "Use ru-RU or en-US.",
                    CultureEnvironmentVariable);
            }

            return CultureInfo.GetCultureInfo(
                normalized);
        }
    }
}
