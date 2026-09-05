using System.Globalization;
using System.Resources;

namespace NetLoom.Wpf.Localization
{
    internal static class UiText
    {
        private static readonly ResourceManager Manager =
            new ResourceManager(
                "NetLoom.Wpf.Resources.UiStrings",
                typeof(UiText).Assembly);

        public static string Get(string key)
        {
            return Manager.GetString(
                       key,
                       CultureInfo.CurrentUICulture)
                   ?? key;
        }

        public static string Format(
            string key,
            params object[] arguments)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                Get(key),
                arguments);
        }
    }
}
