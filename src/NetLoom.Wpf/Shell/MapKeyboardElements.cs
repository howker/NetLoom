using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;

namespace NetLoom.Wpf.Shell
{
    // UI_DESIGN_RULES §8 (K4): узлы, подписи связей и вкладки размещений на карте получают фокус клавиатуры.
    // Обычные Border и TextBlock не дают самостоятельного узла UI Automation (или дают только текст),
    // Поэтому элементы карты — собственные типы с узлом UI Automation и именем из AutomationProperties.Name.
    public sealed class MapKeyboardBorder : Border
    {
        public MapKeyboardBorder()
        {
            // Элемент получает фокус, но в обходе Tab участвует, только пока он «текущий» на карте (IsTabStop задаёт окно).
            Focusable = true;
            KeyboardNavigation.SetIsTabStop(
                this,
                false);
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new MapKeyboardElementPeer(
                this);
        }
    }

    // Подпись связи: имя «Связь A ↔ B, порты …» задаёт окно.
    public sealed class MapKeyboardLabel : TextBlock
    {
        public MapKeyboardLabel()
        {
            Focusable = true;
            KeyboardNavigation.SetIsTabStop(
                this,
                false);
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new MapKeyboardElementPeer(
                this);
        }
    }

    internal sealed class MapKeyboardElementPeer : FrameworkElementAutomationPeer
    {
        public MapKeyboardElementPeer(
            System.Windows.FrameworkElement owner)
            : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore()
        {
            return AutomationControlType.Custom;
        }

        protected override string GetClassNameCore()
        {
            return "MapKeyboardElement";
        }

        protected override bool IsControlElementCore()
        {
            return true;
        }

        protected override bool IsContentElementCore()
        {
            return true;
        }
    }
}
