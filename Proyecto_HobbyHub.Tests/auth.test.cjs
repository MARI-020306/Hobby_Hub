const { test } = require('node:test');
const assert = require('node:assert/strict');
const vm = require('node:vm');
const fs = require('node:fs');
const path = require('node:path');
const source = fs.readFileSync(path.join(__dirname, '../Proyecto_HobbyHub/wwwroot/js/auth.js'), 'utf8');

function setup({ nativeValid = true, jqueryValid = true } = {}) {
    const handlers = {};
    const button = { disabled: false, textContent: 'Iniciar sesión' };
    const form = { checkValidity: () => nativeValid, addEventListener: (name, fn) => handlers[name] = fn };
    const jquery = () => ({ valid: () => jqueryValid });
    jquery.fn = { valid() {} };
    const window = { jQuery: jquery, addEventListener: (name, fn) => handlers[name] = fn };
    const document = {
        getElementById: id => ({ loginForm: form, loginButton: button }[id]),
        addEventListener: (_, fn) => fn()
    };
    vm.runInNewContext(source, { document, window });
    const submit = (alreadyPrevented = false) => {
        const event = { defaultPrevented: alreadyPrevented, preventDefault() { this.defaultPrevented = true; } };
        handlers.submit(event);
        return event;
    };
    return { button, submit, handlers };
}
test('invalid browser form remains usable', () => {
    const app = setup({ nativeValid: false });
    assert.equal(app.submit().defaultPrevented, true);
    assert.equal(app.button.disabled, false);
});
test('jQuery validation failure leaves button enabled', () => {
    const app = setup({ jqueryValid: false });
    assert.equal(app.submit().defaultPrevented, true);
    assert.equal(app.button.disabled, false);
});
test('previously cancelled submission leaves button enabled', () => {
    const app = setup();
    app.submit(true);
    assert.equal(app.button.disabled, false);
});
test('valid form blocks duplicate submissions and resets on back navigation', () => {
    const app = setup();
    assert.equal(app.submit().defaultPrevented, false);
    assert.equal(app.button.disabled, true);
    assert.equal(app.submit().defaultPrevented, true);
    app.handlers.pageshow();
    assert.equal(app.button.disabled, false);
});
