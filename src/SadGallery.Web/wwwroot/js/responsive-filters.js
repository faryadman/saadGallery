// Native disclosure remains usable without JavaScript; desktop opens it automatically.
(() => {
    'use strict';
    const desktop = window.matchMedia('(min-width: 992px)');
    const panels = document.querySelectorAll('[data-responsive-filters]');
    function update() {
        panels.forEach(panel => {
            const open = desktop.matches || panel.dataset.filterError === 'true';
            panel.toggleAttribute('data-filter-desktop', desktop.matches);
            if (!open && panel.contains(document.activeElement)) {
                panel.querySelector('summary').focus({ preventScroll: true });
            }
            panel.open = open;
        });
    }
    update();
    desktop.addEventListener('change', update);
})();
