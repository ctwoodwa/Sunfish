// SunfishGantt JS Interop Module
// Handles scroll sync between task list and timeline, drag-to-move bars,
// drag-to-resize bars (left/right handles), and progress drag handle.

/**
 * Initialize the Gantt interop on a container element.
 * @param {object} dotNetRef - DotNetObjectReference for .NET callbacks
 * @param {HTMLElement} containerEl - The root .sf-gantt element
 * @param {object} options - { rowHeight, slotWidth }
 * @returns {{ dispose: Function }} Cleanup handle
 */
export function initGantt(dotNetRef, containerEl, options) {
    if (!containerEl) return { dispose() {} };

    const abortController = new AbortController();
    const signal = abortController.signal;

    // --- Scroll sync ---
    const taskList = containerEl.querySelector('.sf-gantt__tasklist');
    const timeline = containerEl.querySelector('.sf-gantt__timeline');

    if (taskList && timeline) {
        let syncing = false;
        const syncScroll = (source, target) => {
            if (syncing) return;
            syncing = true;
            target.scrollTop = source.scrollTop;
            syncing = false;
        };
        taskList.addEventListener('scroll', () => syncScroll(taskList, timeline), { signal });
        timeline.addEventListener('scroll', () => syncScroll(timeline, taskList), { signal });
    }

    // --- Drag to move / resize / progress ---
    containerEl.addEventListener('pointerdown', (e) => {
        const bar = e.target.closest('.sf-gantt__bar');
        if (!bar) return;

        const isResizeLeft = e.target.classList.contains('sf-gantt__bar-resize--left');
        const isResizeRight = e.target.classList.contains('sf-gantt__bar-resize--right');
        const isProgressHandle = e.target.classList.contains('sf-gantt__bar-progress-handle');

        // Don't drag if it's just a click on the bar itself with no intent to move
        const barRect = bar.getBoundingClientRect();
        const startX = e.clientX;
        const barIndex = parseInt(bar.dataset.index, 10);
        if (isNaN(barIndex)) return;

        const originalLeft = parseFloat(bar.style.left) || 0;
        const originalWidth = parseFloat(bar.style.width) || barRect.width;

        let moved = false;

        const onMove = (ev) => {
            moved = true;
            const dx = ev.clientX - startX;

            if (isResizeLeft) {
                bar.style.left = (originalLeft + dx) + 'px';
                bar.style.width = Math.max(originalWidth - dx, 4) + 'px';
            } else if (isResizeRight) {
                bar.style.width = Math.max(originalWidth + dx, 4) + 'px';
            } else if (isProgressHandle) {
                const progressBar = bar.querySelector('.sf-gantt__bar-progress');
                if (progressBar) {
                    const newWidth = Math.max(0, Math.min(ev.clientX - barRect.left, originalWidth));
                    progressBar.style.width = newWidth + 'px';
                }
            } else {
                // Move entire bar
                bar.style.left = (originalLeft + dx) + 'px';
            }
        };

        const onUp = (ev) => {
            document.removeEventListener('pointermove', onMove);
            document.removeEventListener('pointerup', onUp);

            if (!moved) return; // Was just a click, not a drag

            const dx = ev.clientX - startX;

            if (isProgressHandle) {
                const newWidth = Math.max(0, Math.min(ev.clientX - barRect.left, originalWidth));
                const pct = originalWidth > 0 ? (newWidth / originalWidth) * 100 : 0;
                dotNetRef.invokeMethodAsync('OnBarProgressChanged', barIndex, pct);
            } else if (isResizeLeft) {
                dotNetRef.invokeMethodAsync('OnBarResized', barIndex, dx, 0);
            } else if (isResizeRight) {
                dotNetRef.invokeMethodAsync('OnBarResized', barIndex, 0, dx);
            } else {
                dotNetRef.invokeMethodAsync('OnBarMoved', barIndex, dx);
            }
        };

        document.addEventListener('pointermove', onMove);
        document.addEventListener('pointerup', onUp);
        bar.setPointerCapture(e.pointerId);
        e.preventDefault();
    }, { signal });

    return {
        dispose() {
            abortController.abort();
        }
    };
}

/**
 * Dispose a previously created interop instance.
 * @param {{ dispose: Function }} instance
 */
export function dispose(instance) {
    if (instance && typeof instance.dispose === 'function') {
        instance.dispose();
    }
}
