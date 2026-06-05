window.FormNavigation = {
    _handler: null,
    _form: null,

    initialize: function (formSelector, fieldIds) {
        // Clean up any previous listener
        if (this._form && this._handler) {
            this._form.removeEventListener('keydown', this._handler);
        }

        const form = document.querySelector(formSelector);
        if (!form) return;

        this._form = form;
        this._handler = function (e) {
            if (e.key !== 'Enter') return;

            const activeEl = document.activeElement;
            if (!activeEl || !activeEl.id) return;

            const currentIndex = fieldIds.indexOf(activeEl.id);
            if (currentIndex === -1) return;

            e.preventDefault();

            if (currentIndex < fieldIds.length - 1) {
                const nextField = document.getElementById(fieldIds[currentIndex + 1]);
                if (nextField) nextField.focus();
            } else {
                const submitBtn = form.querySelector('button[type="submit"]');
                if (submitBtn) submitBtn.click();
            }
        };

        form.addEventListener('keydown', this._handler);
    },

    focusField: function (fieldId) {
        const el = document.getElementById(fieldId);
        if (el) el.focus();
    }
};
