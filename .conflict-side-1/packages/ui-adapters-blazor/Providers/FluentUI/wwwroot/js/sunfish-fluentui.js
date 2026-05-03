export function showModal(modalId) {
    const modal = document.getElementById(modalId);
    if (modal) {
        modal.classList.add('mar-modal--open');
        modal.setAttribute('aria-hidden', 'false');
        const focusable = modal.querySelector('button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])');
        if (focusable) focusable.focus();
        return true;
    }
    return false;
}

export function hideModal(modalId) {
    const modal = document.getElementById(modalId);
    if (modal) {
        modal.classList.remove('mar-modal--open');
        modal.setAttribute('aria-hidden', 'true');
    }
}

export function getElementBounds(element) {
    const rect = element.getBoundingClientRect();
    return { x: rect.x, y: rect.y, width: rect.width, height: rect.height };
}

export function observeScroll(element, dotNetRef) {
    const observer = new IntersectionObserver((entries) => {
        entries.forEach(entry => {
            dotNetRef.invokeMethodAsync('OnScrollObserved', entry.isIntersecting);
        });
    });
    observer.observe(element);
}
