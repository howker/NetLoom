using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace NetLoom.Wpf.Shell
{
    // UI_DESIGN_RULES §8: кнопка, выполнившая действие над всем выбором, становится неактивной, а фокус остаётся на ней.
    // WPF снимает фокус с элемента, у которого IsEnabled=false, поэтому «неактивность» здесь —
    // Вид (триггер стиля) и UI Automation; повторное нажатие отсеивает обработчик.
    public static class InertState
    {
        public static readonly DependencyProperty IsInertProperty =
            DependencyProperty.RegisterAttached(
                "IsInert",
                typeof(bool),
                typeof(InertState),
                new PropertyMetadata(false));

        public static bool GetIsInert(
            DependencyObject element)
        {
            return (bool)element.GetValue(
                IsInertProperty);
        }

        public static void SetIsInert(
            DependencyObject element,
            bool value)
        {
            element.SetValue(
                IsInertProperty,
                value);
        }
    }

    // Кнопка, которая сообщает UI Automation о неактивности, пока установлен InertState.IsInert.
    public class InertButton : Button
    {
        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new InertButtonAutomationPeer(
                this);
        }

        private sealed class InertButtonAutomationPeer : ButtonAutomationPeer
        {
            public InertButtonAutomationPeer(
                Button owner)
                : base(owner)
            {
            }

            protected override bool IsEnabledCore()
            {
                return base.IsEnabledCore() &&
                    !InertState.GetIsInert(Owner);
            }
        }
    }
}
