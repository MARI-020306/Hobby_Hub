(function (root, factory) {
    if (typeof module === 'object' && module.exports) module.exports = factory;
    else factory(root.document, root);
})(typeof window !== 'undefined' ? window : globalThis, function initializeNavigation(document, window) {
    'use strict';
    const button = document.getElementById('navigation-toggle');
    const panel = document.getElementById('main-navigation');
    const sidebar = document.querySelector('.app-sidebar');
    if (!button || !panel || !sidebar) return;
    const viewport = window.matchMedia('(max-width: 767px)');
    document.documentElement.dataset.navigation = 'ready';
    function setOpen(open, focus) {
        sidebar.dataset.menuOpen = String(open);
        button.setAttribute('aria-expanded', String(open));
        button.setAttribute('aria-label', open ? 'Cerrar menú' : 'Abrir menú');
        if (focus) button.focus();
    }
    button.addEventListener('click', () => setOpen(sidebar.dataset.menuOpen !== 'true', false));
    document.addEventListener('keydown', event => {
        if (event.key === 'Escape' && viewport.matches && sidebar.dataset.menuOpen === 'true') {
            event.preventDefault(); setOpen(false, true);
        }
    });
    panel.addEventListener('click', event => { if (event.target.closest('a') && viewport.matches) setOpen(false, false); });
    viewport.addEventListener('change', () => setOpen(false, false));
    window.addEventListener('pageshow', () => {
        setOpen(false, false);
        document.querySelectorAll('form[aria-busy="true"]').forEach(form => form.removeAttribute('aria-busy'));
        const status=document.querySelector('.submission-status');if(status)status.hidden=true;
    });
    document.addEventListener('submit', event => {
        const form=event.target;
        if(event.defaultPrevented || !form.checkValidity() || (window.jQuery && !window.jQuery(form).valid()))return;
        // Do not disable buttons: MVC may need the clicked button's name/value.
        form.setAttribute('aria-busy','true');
        const status=document.querySelector('.submission-status');if(status)status.hidden=false;
    });
    setOpen(false,false);
    return {setOpen};
});
