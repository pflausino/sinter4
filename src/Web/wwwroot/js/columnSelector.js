window.ColumnSelector = {
    _listener: null,
    _element: null,
    _dotNetRef: null,

    open: function (element, dotNetRef) {
        this._element = element;
        this._dotNetRef = dotNetRef;
        this._listener = (e) => {
            if (!element.contains(e.target)) {
                this.close();
                dotNetRef.invokeMethodAsync('CloseColumnSelector');
            }
        };
        // Delay to avoid catching the opening click
        setTimeout(() => document.addEventListener('mousedown', this._listener), 0);
    },

    close: function () {
        if (this._listener) {
            document.removeEventListener('mousedown', this._listener);
            this._listener = null;
        }
    },

    // Storage can be unavailable (private mode, blocked site data); callers fall back to defaults
    loadVisibility: function (key) {
        try { return localStorage.getItem(key); } catch { return null; }
    },

    saveVisibility: function (key, value) {
        try { localStorage.setItem(key, value); } catch { }
    }
};
