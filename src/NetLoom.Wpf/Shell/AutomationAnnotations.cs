using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace NetLoom.Wpf.Shell
{
    // UI_DESIGN_RULES §8 «Структура»: заголовки панелей и разделов имеют уровень для UI Automation.
    // AutomationProperties.HeadingLevel есть только в WPF для .NET Core 3.0+; приложение оператора
    // (NetLoom.Desktop) собирается под net8.0-windows, сборка net48 уровни не передаёт — API нет.
    public static class AutomationHeading
    {
        public static readonly DependencyProperty LevelProperty =
            DependencyProperty.RegisterAttached(
                "Level",
                typeof(int),
                typeof(AutomationHeading),
                new PropertyMetadata(
                    0,
                    OnLevelChanged));

        public static int GetLevel(
            DependencyObject element)
        {
            return (int)element.GetValue(
                LevelProperty);
        }

        public static void SetLevel(
            DependencyObject element,
            int value)
        {
            element.SetValue(
                LevelProperty,
                value);
        }

        private static void OnLevelChanged(
            DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
#if NETCOREAPP
            var level =
                (int)e.NewValue;

            AutomationProperties.SetHeadingLevel(
                d,
                level < 1 || level > 9
                    ? AutomationHeadingLevel.None
                    : (AutomationHeadingLevel)level);
#endif
        }
    }

    // UI_DESIGN_RULES §8 «Объявление изменений»: ненавязчивые обновления — LiveSetting=Polite.
    // WPF объявляет живую область, только когда приложение сообщает об изменении (LiveRegionChanged);
    // Это свойство делает и то и другое для текста (TextBlock) и для списка (ItemsControl).
    public static class LiveRegion
    {
        public static readonly DependencyProperty IsPoliteProperty =
            DependencyProperty.RegisterAttached(
                "IsPolite",
                typeof(bool),
                typeof(LiveRegion),
                new PropertyMetadata(
                    false,
                    OnIsPoliteChanged));

        private static readonly DependencyPropertyDescriptor TextDescriptor =
            DependencyPropertyDescriptor.FromProperty(
                TextBlock.TextProperty,
                typeof(TextBlock));

        // Обработчик изменения списка хранится на самом элементе, чтобы его можно было снять.
        private static readonly DependencyProperty ItemsHandlerProperty =
            DependencyProperty.RegisterAttached(
                "ItemsHandler",
                typeof(NotifyCollectionChangedEventHandler),
                typeof(LiveRegion));

        public static bool GetIsPolite(
            DependencyObject element)
        {
            return (bool)element.GetValue(
                IsPoliteProperty);
        }

        public static void SetIsPolite(
            DependencyObject element,
            bool value)
        {
            element.SetValue(
                IsPoliteProperty,
                value);
        }

        private static void OnIsPoliteChanged(
            DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            var element =
                d as FrameworkElement;

            if (element == null)
            {
                return;
            }

            element.Loaded -= OnLoaded;
            element.Unloaded -= OnUnloaded;
            Unsubscribe(
                element);

            if (!(bool)e.NewValue)
            {
                AutomationProperties.SetLiveSetting(
                    element,
                    AutomationLiveSetting.Off);
                return;
            }

            AutomationProperties.SetLiveSetting(
                element,
                AutomationLiveSetting.Polite);

            // Подписки держат ссылку на элемент — только пока он в визуальном дереве.
            element.Loaded += OnLoaded;
            element.Unloaded += OnUnloaded;

            if (element.IsLoaded)
            {
                Subscribe(
                    element);
            }
        }

        private static void OnLoaded(
            object sender,
            RoutedEventArgs e)
        {
            Subscribe(
                (FrameworkElement)sender);
        }

        private static void OnUnloaded(
            object sender,
            RoutedEventArgs e)
        {
            Unsubscribe(
                (FrameworkElement)sender);
        }

        private static void Subscribe(
            FrameworkElement element)
        {
            Unsubscribe(
                element);

            if (element is TextBlock)
            {
                TextDescriptor.AddValueChanged(
                    element,
                    OnChanged);
            }

            var items =
                element as ItemsControl;

            if (items != null)
            {
                NotifyCollectionChangedEventHandler handler =
                    (sender, args) =>
                        Announce(
                            items);

                items.SetValue(
                    ItemsHandlerProperty,
                    handler);

                ((INotifyCollectionChanged)items.Items).CollectionChanged +=
                    handler;
            }
        }

        private static void Unsubscribe(
            FrameworkElement element)
        {
            if (element is TextBlock)
            {
                TextDescriptor.RemoveValueChanged(
                    element,
                    OnChanged);
            }

            var items =
                element as ItemsControl;

            var handler =
                items?.GetValue(
                    ItemsHandlerProperty) as NotifyCollectionChangedEventHandler;

            if (handler != null)
            {
                ((INotifyCollectionChanged)items.Items).CollectionChanged -=
                    handler;
                items.ClearValue(
                    ItemsHandlerProperty);
            }
        }

        private static void OnChanged(
            object sender,
            EventArgs e)
        {
            Announce(
                sender as UIElement);
        }

        private static void Announce(
            UIElement element)
        {
            if (element == null ||
                !element.IsVisible ||
                !AutomationPeer.ListenerExists(
                    AutomationEvents.LiveRegionChanged))
            {
                return;
            }

            UIElementAutomationPeer
                .CreatePeerForElement(
                    element)
                ?.RaiseAutomationEvent(
                    AutomationEvents.LiveRegionChanged);
        }
    }

    // UI_DESIGN_RULES §8: выбранный вариант (раздел рейла, режим карты, фильтр, тема) передаётся
    // Программе как состояние, а не только цветом. Кнопки-варианты получают ItemStatus «Выбрано».
    public static class AutomationSelection
    {
        public static void SetSelected(
            FrameworkElement element,
            bool selected,
            string selectedText)
        {
            if (element == null)
            {
                return;
            }

            if (selected)
            {
                AutomationProperties.SetItemStatus(
                    element,
                    selectedText);
                return;
            }

            element.ClearValue(
                AutomationProperties.ItemStatusProperty);
        }
    }
}
