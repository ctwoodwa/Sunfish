export function getFavorites(key) {
    try {
        return localStorage.getItem(key);
    } catch {
        return null;
    }
}

export function setFavorites(key, json) {
    try {
        localStorage.setItem(key, json);
    } catch {
        // localStorage unavailable — silently degrade
    }
}
