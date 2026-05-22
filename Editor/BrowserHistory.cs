using System.Collections.Generic;

namespace EditorBrowser
{
    /// <summary>
    /// Back/forward navigation history. Pushing a new URL from a mid-history
    /// position truncates the forward entries, matching browser semantics.
    /// </summary>
    internal sealed class BrowserHistory
    {
        private readonly List<string> _entries = new List<string>();
        private int _index = -1;

        public string Current => (_index >= 0 && _index < _entries.Count) ? _entries[_index] : null;

        public bool CanGoBack => _index > 0;

        public bool CanGoForward => _index >= 0 && _index < _entries.Count - 1;

        public int Count => _entries.Count;

        public void Push(string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            if (Current == url) return;

            if (_index < _entries.Count - 1)
                _entries.RemoveRange(_index + 1, _entries.Count - _index - 1);

            _entries.Add(url);
            _index = _entries.Count - 1;
        }

        public string GoBack()
        {
            if (!CanGoBack) return Current;
            _index--;
            return Current;
        }

        public string GoForward()
        {
            if (!CanGoForward) return Current;
            _index++;
            return Current;
        }

        public void Clear()
        {
            _entries.Clear();
            _index = -1;
        }
    }
}
