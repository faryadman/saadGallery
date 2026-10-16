(() => {
    'use strict';
    const header = document.querySelector('.sg-header');
    const panel = document.getElementById('sgNav');
    const toggle = header?.querySelector('[data-bs-target="#sgNav"]');
    const backdrop = document.querySelector('[data-nav-backdrop]');
    if (!panel || !toggle || !backdrop || !window.bootstrap?.Collapse) return;

    const mobile = window.matchMedia('(max-width: 1199.98px)');
    const collapse = bootstrap.Collapse.getOrCreateInstance(panel, { toggle: false });
    let closePending = false;
    const reset = () => {
        document.body.classList.remove('sg-mobile-nav-open');
        backdrop.hidden = true;
    };
    const close = (restoreFocus = false) => {
        closePending = true;
        if (restoreFocus) toggle.focus({ preventScroll: true });
        collapse.hide();
    };
    panel.addEventListener('show.bs.collapse', event => {
        if (event.target !== panel || !mobile.matches) return;
        closePending = false;
        document.body.classList.add('sg-mobile-nav-open');
        backdrop.hidden = false;
    });
    panel.addEventListener('shown.bs.collapse', event => {
        if (event.target === panel && (closePending || !mobile.matches)) collapse.hide();
    });
    panel.addEventListener('hide.bs.collapse', event => {
        if (event.target !== panel) return;
        reset();
        panel.querySelectorAll('[data-bs-toggle="dropdown"]').forEach(button => bootstrap.Dropdown.getInstance(button)?.hide());
    });
    panel.addEventListener('hidden.bs.collapse', reset);
    document.addEventListener('pointerdown', event => {
        if (mobile.matches && !backdrop.hidden && !header.contains(event.target)) close(true);
    });
    document.addEventListener('keydown', event => {
        if (mobile.matches && !backdrop.hidden && event.key === 'Escape') {
            event.preventDefault();
            close(true);
        }
    }, true);
    // This is a navigation disclosure: tabbing out closes it without trapping focus.
    document.addEventListener('focusin', event => {
        if (mobile.matches && !backdrop.hidden && !header.contains(event.target)) close();
    });
    panel.addEventListener('click', event => {
        if (mobile.matches && event.target.closest('a[href]')) close();
    });
    mobile.addEventListener('change', () => {
        close();
        reset();
    });
    window.addEventListener('pagehide', reset);
    window.addEventListener('pageshow', event => {
        if (event.persisted) {
            close();
            reset();
        }
    });
})();
