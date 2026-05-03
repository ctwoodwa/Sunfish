export function setProvider(provider) {
    try {
        localStorage.setItem('sunfish:provider', provider);
    } catch { }
    location.reload();
}

export function getStoredProvider() {
    try {
        return localStorage.getItem('sunfish:provider') || 'fluentui';
    } catch {
        return 'fluentui';
    }
}

export function applyProvider(provider) {
    const fluentLink = document.getElementById('sunfish-provider-fluentui');
    const bootstrapLink = document.getElementById('sunfish-provider-bootstrap');
    const materialLink = document.getElementById('sunfish-provider-material');

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
    try { localStorage.setItem('sunfish:theme', name); } catch { }
}

export function getStoredTheme() {
    try { return localStorage.getItem('sunfish:theme') || 'Default'; } catch { return 'Default'; }
}

export function setDarkMode(dark) {
    try { localStorage.setItem('sunfish:darkmode', dark ? '1' : '0'); } catch { }
}

export function getStoredDarkMode() {
    try { return localStorage.getItem('sunfish:darkmode') === '1'; } catch { return false; }
}
