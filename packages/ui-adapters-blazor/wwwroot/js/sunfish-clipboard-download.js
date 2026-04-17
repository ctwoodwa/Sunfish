// Sunfish Shared Interop: Clipboard & Download Module
// Provides clipboard read/write and in-memory file download triggering.

/**
 * Writes text to the system clipboard.
 * @param {string} text
 */
export async function writeText(text) {
    await navigator.clipboard.writeText(text);
}

/**
 * Writes HTML and/or text to the clipboard using ClipboardItem.
 * Falls back to writeText if ClipboardItem is not available.
 * @param {object} request
 * @param {string} [request.text]
 * @param {string} [request.html]
 */
export async function writeClipboard(request) {
    if (request.html && typeof ClipboardItem !== 'undefined') {
        const items = {};
        if (request.html) items['text/html'] = new Blob([request.html], { type: 'text/html' });
        if (request.text) items['text/plain'] = new Blob([request.text], { type: 'text/plain' });
        await navigator.clipboard.write([new ClipboardItem(items)]);
    } else if (request.text) {
        await navigator.clipboard.writeText(request.text);
    }
}

/**
 * Reads plain text from the system clipboard.
 * @returns {Promise<string>}
 */
export async function readText() {
    return await navigator.clipboard.readText();
}

/**
 * Triggers a browser file download from base64-encoded content.
 * @param {object} request
 * @param {string} request.fileName
 * @param {string} request.contentType
 * @param {string} request.base64Content
 */
export function download(request) {
    const bytes = Uint8Array.from(atob(request.base64Content), c => c.charCodeAt(0));
    const blob = new Blob([bytes], { type: request.contentType });
    const url = URL.createObjectURL(blob);

    const a = document.createElement('a');
    a.href = url;
    a.download = request.fileName;
    document.body.appendChild(a);
    a.click();

    // Cleanup
    setTimeout(() => {
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    }, 100);
}

/**
 * Disposes module resources (stateless — no-op).
 */
export function dispose() {
    // Stateless module; no cleanup needed.
}
