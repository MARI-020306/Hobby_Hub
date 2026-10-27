(function () {
    'use strict';
    // Validation messages are text. Never interpret a localized/user-derived message as HTML.
    if (window.jQuery && window.jQuery.validator) {
        window.jQuery.validator.setDefaults({ escapeHtml: true });
    }
})();
