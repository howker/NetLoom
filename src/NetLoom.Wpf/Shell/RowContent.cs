using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace NetLoom.Wpf.Shell
{
    // UI_DESIGN_RULES §8: обновление данных раз в несколько секунд не должно пересоздавать строки списков,
    // Если их содержимое не изменилось, — иначе кнопка в строке теряет фокус клавиатуры (как лента событий, Sprint 48).
    // Сравнение — по значениям открытых свойств простых типов; изменяемое состояние представления
    // (выбор, раскрытие) не сравнивается.
    internal static class RowContent
    {
        private static readonly HashSet<string> PresentationState =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "IsSelected"
            };

        public static bool SameRows(
            IEnumerable current,
            IReadOnlyList<object> next)
        {
            if (current == null ||
                next == null)
            {
                return false;
            }

            var previous =
                current.Cast<object>().ToArray();

            if (previous.Length != next.Count)
            {
                return false;
            }

            for (var index = 0; index < previous.Length; index++)
            {
                if (!SameContent(
                        previous[index],
                        next[index]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool SameContent(
            object first,
            object second)
        {
            if (first == null ||
                second == null ||
                first.GetType() != second.GetType())
            {
                return false;
            }

            foreach (var property in first.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                // Изменяемое состояние представления (выбор с открытым сеттером) живёт в самой строке;
                // Неизменяемый IsSelected — данные строки и сравнивается.
                if (!property.CanRead ||
                    property.GetIndexParameters().Length > 0 ||
                    (PresentationState.Contains(property.Name) &&
                     property.GetSetMethod() != null))
                {
                    continue;
                }

                if (!SameValue(
                        property.GetValue(first),
                        property.GetValue(second)))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool SameValue(
            object first,
            object second)
        {
            if (first == null || second == null)
            {
                return first == null && second == null;
            }

            var type = first.GetType();

            if (type.IsPrimitive ||
                type.IsEnum ||
                first is string ||
                first is decimal ||
                first is Guid ||
                first is DateTime ||
                first is TimeSpan)
            {
                return first.Equals(second);
            }

            var firstItems = first as IEnumerable;
            var secondItems = second as IEnumerable;

            if (firstItems != null &&
                secondItems != null)
            {
                var a = firstItems.Cast<object>().ToArray();
                var b = secondItems.Cast<object>().ToArray();
                return a.Length == b.Length &&
                       a.Zip(b, SameValue).All(equal => equal);
            }

            // Кисти, команды и прочие объекты представления: одинаковый экземпляр или равенство значения.
            return ReferenceEquals(first, second) ||
                   first.Equals(second);
        }
    }
}
