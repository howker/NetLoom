using System;
using System.Globalization;
using System.Linq;
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

        public static string FormatCount(
            string keyStem,
            int count,
            params object[] arguments)
        {
            if (string.IsNullOrWhiteSpace(
                keyStem))
            {
                throw new ArgumentException(
                    "Resource key stem is required.",
                    nameof(keyStem));
            }

            var formatKey =
                keyStem +
                PluralSuffix(
                    count,
                    CultureInfo.CurrentUICulture);

            var formatArguments =
                new object[]
                {
                    count
                }
                .Concat(
                    arguments ??
                    new object[0])
                .ToArray();

            return Format(
                formatKey,
                formatArguments);
        }

        private static string PluralSuffix(
            int count,
            CultureInfo culture)
        {
            if (culture != null &&
                string.Equals(
                    culture.TwoLetterISOLanguageName,
                    "ru",
                    StringComparison.OrdinalIgnoreCase))
            {
                var absolute =
                    Math.Abs(
                        (long)count);

                var mod10 =
                    absolute % 10;

                var mod100 =
                    absolute % 100;

                if (mod10 == 1 &&
                    mod100 != 11)
                {
                    return "One";
                }

                if (mod10 >= 2 &&
                    mod10 <= 4 &&
                    (mod100 < 12 ||
                     mod100 > 14))
                {
                    return "Few";
                }

                return "Many";
            }

            return count == 1
                ? "One"
                : "Other";
        }
    }
}
