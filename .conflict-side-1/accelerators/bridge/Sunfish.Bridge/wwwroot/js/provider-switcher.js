export function setProvider(provider) {
    try {
        localStorage.setItem('marilo:provider', provider);
    } catch { }
    location.reload();
}

export function getStoredProvider() {
    try {
        return localStorage.getItem('marilo:provider') || 'fluentui';
    } catch {
        return 'fluentui';
    }
}

export function applyProvider(provider) {
    const fluentLink = document.getElementById('marilo-provider-fluentui');
    const bootstrapLink = document.getElementById('marilo-provider-bootstrap');
    const materialLink = document.getElementById('marilo-provider-material');

    if (fluentLink) fluentLink.disabled = true;
    if (bootstrapLink) bootstrapLink.disabled = true;
    if (materialLink) materialLink.disabled = true;

    if (provider === 'bootstrap' && bootstrapLink) {
        bootstrapLink.disabled = false;
    } else if (provider === 'material' && materialLink) {
        materialLink.disabled = false;
    } else if (fluentLink) {
        fluentLink.disabled = false;
    }
}

export function setTheme(name) {
    try { localStorage.setItem('marilo:theme', name); } catch { }
}

export function getStoredTheme() {
    try { return localStorage.getItem('marilo:theme') || 'Default'; } catch { return 'Default'; }
}

export function setDarkMode(dark) {
    try { localStorage.setItem('marilo:darkmode', dark ? '1' : '0'); } catch { }
}

export function getStoredDarkMode() {
    try { return localStorage.getItem('marilo:darkmode') === '1'; } catch { return false; }
}
