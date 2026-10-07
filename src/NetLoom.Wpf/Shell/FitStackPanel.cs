using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace NetLoom.Wpf.Shell
{
    // Горизонтальный ряд, в котором виден только тот элемент, что помещается целиком.
    // Остальные не рисуются вовсе, а не обрезаются краем (UI_DESIGN_RULES §10 «нет обрезанного текста»).
    // Порядок элементов задаёт важность: первые остаются, последние отпадают при нехватке ширины.
    // Нужен ленте событий: третье событие уходило под ссылку «Все события» (Sprint 47).
    public sealed class FitStackPanel : Panel
    {
        protected override Size MeasureOverride(
            Size availableSize)
        {
            var width = 0.0;
            var height = 0.0;
            var unlimited =
                new Size(
                    double.PositiveInfinity,
                    availableSize.Height);

            foreach (UIElement child in InternalChildren)
            {
                if (child == null)
                {
                    continue;
                }

                child.Measure(
                    unlimited);

                var desired =
                    child.DesiredSize;

                if (width + desired.Width > availableSize.Width)
                {
                    // Дальше ничего не показывается, даже если следующий элемент короче:
                    // Менее важное событие не встаёт впереди более важного.
                    break;
                }

                width += desired.Width;
                height = Math.Max(
                    height,
                    desired.Height);
            }

            return new Size(
                width,
                height);
        }

        protected override Size ArrangeOverride(
            Size finalSize)
        {
            var x = 0.0;
            var hidden = false;

            foreach (UIElement child in InternalChildren)
            {
                if (child == null)
                {
                    continue;
                }

                var desired =
                    child.DesiredSize;

                if (hidden ||
                    x + desired.Width > finalSize.Width + 0.5)
                {
                    hidden = true;
                    child.Arrange(
                        new Rect(
                            0,
                            0,
                            0,
                            0));

                    // Скрытое событие не получает фокус по Tab и щелчок; свойства не влияют на раскладку.
                    KeyboardNavigation.SetTabNavigation(
                        child,
                        KeyboardNavigationMode.None);
                    child.IsHitTestVisible = false;
                    continue;
                }

                child.ClearValue(
                    KeyboardNavigation.TabNavigationProperty);
                child.ClearValue(
                    UIElement.IsHitTestVisibleProperty);

                child.Arrange(
                    new Rect(
                        x,
                        0,
                        desired.Width,
                        finalSize.Height));

                x += desired.Width;
            }

            return finalSize;
        }
    }
}
