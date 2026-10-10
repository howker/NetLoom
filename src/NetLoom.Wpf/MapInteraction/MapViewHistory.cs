using System;
using System.Collections.Generic;

namespace NetLoom.Wpf.MapInteraction
{
    // Стек истории видов карты (Alt+←): новейшая запись снимается первой, глубина ограничена токеном.
    // При переполнении теряется самая старая запись.
    internal sealed class MapViewHistory<TState>
    {
        private readonly List<TState> _items = new List<TState>();
        private readonly int _limit;

        public MapViewHistory(int limit)
        {
            if (limit < 1) throw new ArgumentOutOfRangeException(nameof(limit));
            _limit = limit;
        }

        public int Limit
        {
            get { return _limit; }
        }

        public int Count
        {
            get { return _items.Count; }
        }

        public void Push(TState state)
        {
            if (_items.Count >= _limit) _items.RemoveAt(0);
            _items.Add(state);
        }

        public bool TryPop(out TState state)
        {
            if (_items.Count == 0)
            {
                state = default(TState);
                return false;
            }
            state = _items[_items.Count - 1];
            _items.RemoveAt(_items.Count - 1);
            return true;
        }

        public void Clear()
        {
            _items.Clear();
        }
    }
}
