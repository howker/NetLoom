using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace NetLoom.Wpf.Shell
{
    // G5 (sprint46-mockup-gap), UI_DESIGN_RULES §3 и §4: подписи разделов заглавными задаёт стиль.
    // Ресурсы хранят текст в обычном регистре, а у TextBlock в WPF нет преобразования регистра.
    // Капитель (Typography.Capitals) зависит от шрифта и на старых Windows не гарантирована.
    // Стиль включает это свойство, и назначенный текст переводится в верхний регистр по культуре интерфейса.
    public static class TextCase
    {
        public static readonly DependencyProperty UpperProperty =
            DependencyProperty.RegisterAttached(
                "Upper",
                typeof(bool),
                typeof(TextCase),
                new PropertyMetadata(
                    false,
                    OnUpperChanged));

        private static readonly DependencyPropertyDescriptor TextDescriptor =
            DependencyPropertyDescriptor.FromProperty(
                TextBlock.TextProperty,
                typeof(TextBlock));

        public static bool GetUpper(
            DependencyObject element)
        {
            return (bool)element.GetValue(
                UpperProperty);
        }

        public static void SetUpper(
            DependencyObject element,
            bool value)
        {
            element.SetValue(
                UpperProperty,
                value);
        }

        private static void OnUpperChanged(
            DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            var textBlock =
                d as TextBlock;

            if (textBlock == null)
            {
                return;
            }

            textBlock.Loaded -= OnLoaded;
            textBlock.Unloaded -= OnUnloaded;
            TextDescriptor.RemoveValueChanged(
                textBlock,
                OnTextChanged);

            if (!(bool)e.NewValue)
            {
                return;
            }

            // Подписка на изменение текста держит ссылку на элемент, поэтому живёт только в визуальном дереве.
            // Строки шаблонов пересоздаются, и отписка при выгрузке не даёт им копиться.
            textBlock.Loaded += OnLoaded;
            textBlock.Unloaded += OnUnloaded;

            if (textBlock.IsLoaded)
            {
                Subscribe(
                    textBlock);
            }

            Apply(
                textBlock);
        }

        private static void OnLoaded(
            object sender,
            RoutedEventArgs e)
        {
            var textBlock =
                (TextBlock)sender;

            Subscribe(
                textBlock);
            Apply(
                textBlock);
        }

        private static void OnUnloaded(
            object sender,
            RoutedEventArgs e)
        {
            TextDescriptor.RemoveValueChanged(
                (TextBlock)sender,
                OnTextChanged);
        }

        private static void Subscribe(
            TextBlock textBlock)
        {
            // Повторная подписка безопасна: сначала снимаем прежнюю.
            TextDescriptor.RemoveValueChanged(
                textBlock,
                OnTextChanged);
            TextDescriptor.AddValueChanged(
                textBlock,
                OnTextChanged);
        }

        private static void OnTextChanged(
            object sender,
            EventArgs e)
        {
            Apply(
                (TextBlock)sender);
        }

        private static void Apply(
            TextBlock textBlock)
        {
            var text =
                textBlock.Text;

            if (string.IsNullOrEmpty(
                    text))
            {
                return;
            }

            var upper =
                text.ToUpper(
                    CultureInfo.CurrentUICulture);

            if (!string.Equals(
                    text,
                    upper,
                    StringComparison.Ordinal))
            {
                // SetCurrentValue сохраняет привязку Text, если она есть (заголовки групп поиска).
                textBlock.SetCurrentValue(
                    TextBlock.TextProperty,
                    upper);
            }
        }
    }
}
