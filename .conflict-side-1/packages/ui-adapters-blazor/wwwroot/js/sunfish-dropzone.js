// sunfish-dropzone.js — External drop-zone forwarding for SunfishFileUpload / SunfishUpload
// Exports: registerDropZone, unregisterDropZone

let nextHandleId = 1;
const handles = new Map();

/**
 * Registers an external DOM element as a drop zone that forwards dropped files
 * to the specified file input element.
 * @param {string} dropZoneElementId - The id of the external drop zone element.
 * @param {string} fileInputElementId - The id of the hidden file input element.
 * @returns {number} A handle ID for cleanup, or -1 if elements not found.
 */
export function registerDropZone(dropZoneElementId, fileInputElementId) {
    const dropZone = document.getElementById(dropZoneElementId);
    const fileInput = document.getElementById(fileInputElementId);

    if (!dropZone || !fileInput) {
        return -1;
    }

    const handleId = nextHandleId++;

    const onDragEnter = (e) => {
        e.preventDefault();
        e.stopPropagation();
        dropZone.classList.add('sf-dropzone--drag-over');
    };

    const onDragOver = (e) => {
        e.preventDefault();
        e.stopPropagation();
        dropZone.classList.add('sf-dropzone--drag-over');
    };

    const onDragLeave = (e) => {
        e.preventDefault();
        e.stopPropagation();
        dropZone.classList.remove('sf-dropzone--drag-over');
    };

    const onDrop = (e) => {
        e.preventDefault();
        e.stopPropagation();
        dropZone.classList.remove('sf-dropzone--drag-over');

        if (e.dataTransfer && e.dataTransfer.files.length > 0) {
            fileInput.files = e.dataTransfer.files;
            fileInput.dispatchEvent(new Event('change', { bubbles: true }));
        }
    };

    dropZone.addEventListener('dragenter', onDragEnter);
    dropZone.addEventListener('dragover', onDragOver);
    dropZone.addEventListener('dragleave', onDragLeave);
    dropZone.addEventListener('drop', onDrop);

    handles.set(handleId, { dropZone, onDragEnter, onDragOver, onDragLeave, onDrop });

    return handleId;
}

/**
 * Removes all event listeners for the given handle and cleans up.
 * @param {number} handleId - The handle returned by registerDropZone.
 */
export function unregisterDropZone(handleId) {
    const entry = handles.get(handleId);
    if (!entry) return;

    entry.dropZone.removeEventListener('dragenter', entry.onDragEnter);
    entry.dropZone.removeEventListener('dragover', entry.onDragOver);
    entry.dropZone.removeEventListener('dragleave', entry.onDragLeave);
    entry.dropZone.removeEventListener('drop', entry.onDrop);
    entry.dropZone.classList.remove('sf-dropzone--drag-over');

    handles.delete(handleId);
}
