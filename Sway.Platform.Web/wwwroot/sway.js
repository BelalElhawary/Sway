// Input bridge for SwayView: forwards pointer, wheel, key, text and clipboard events to .NET.
const hosts = new WeakMap();

// Keys whose browser default (scrolling, focus change, history navigation) would fight the app.
const swallow = new Set(['Tab', 'Backspace', 'ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown', 'PageUp', 'PageDown', 'Home', 'End', ' ']);
const chords = new Set(['a', 'c', 'v', 'x', 'y', 'z']);

async function readClipboard(dotnet) {
    try {
        const text = await navigator.clipboard.readText();
        dotnet.invokeMethodAsync('OnClipboard', text);
    } catch { /* permission denied or unsupported: keep the last known text */ }
}

export async function writeClipboard(text) {
    try { await navigator.clipboard.writeText(text); } catch { /* ignore */ }
}

export function setTitle(title) {
    document.title = title;
}

export function attach(host, dotnet) {
    const cleanup = [];
    const on = (target, type, fn, opts) => {
        target.addEventListener(type, fn, opts);
        cleanup.push(() => target.removeEventListener(type, fn, opts));
    };
    const point = e => {
        const r = host.getBoundingClientRect();
        return [e.clientX - r.left, e.clientY - r.top];
    };
    const dark = window.matchMedia('(prefers-color-scheme: dark)');
    const reportSize = () => {
        const r = host.getBoundingClientRect();
        dotnet.invokeMethodAsync('OnResize', r.width, r.height, dark.matches);
    };

    const state = { cursor: 'default', composing: false, raf: 0, stopped: false };

    on(host, 'pointermove', e => { const [x, y] = point(e); dotnet.invokeMethodAsync('OnPointerMove', x, y); });
    on(host, 'pointerdown', e => {
        if (e.button !== 0) return;
        host.focus();
        host.setPointerCapture(e.pointerId);
        const [x, y] = point(e);
        dotnet.invokeMethodAsync('OnPointerDown', x, y);
    });
    const up = e => {
        if (e.button !== 0) return;
        const [x, y] = point(e);
        dotnet.invokeMethodAsync('OnPointerUp', x, y);
    };
    on(host, 'pointerup', up);
    on(host, 'pointercancel', up);
    // One wheel notch is about 100 logical pixels; line- and page-mode wheels report lines and pages, not pixels.
    on(host, 'wheel', e => {
        e.preventDefault();
        const [x, y] = point(e);
        const k = e.deltaMode === 1 ? 33 : e.deltaMode === 2 ? 400 : 1;
        dotnet.invokeMethodAsync('OnWheel', x, y, e.deltaX * k, e.deltaY * k);
    }, { passive: false });
    on(host, 'contextmenu', e => e.preventDefault());

    const key = down => e => {
        if (state.composing || e.isComposing) return;
        const mod = e.ctrlKey || e.metaKey;
        // Pressing a modifier is the last chance to fetch the clipboard before a paste shortcut.
        if (down && (e.key === 'Control' || e.key === 'Meta')) readClipboard(dotnet);

        if ((swallow.has(e.key) && !e.altKey) || (mod && chords.has(e.key.toLowerCase()))) e.preventDefault();
        dotnet.invokeMethodAsync('OnKey', e.key, e.code, down, e.repeat, e.shiftKey, e.ctrlKey, e.altKey, e.metaKey);

        // Printable characters are text, not key presses (the key name is a single code point).
        if (down && !mod && [...e.key].length === 1) dotnet.invokeMethodAsync('OnText', e.key);
    };
    on(host, 'keydown', key(true));
    on(host, 'keyup', key(false));

    on(host, 'compositionstart', () => { state.composing = true; });
    on(host, 'compositionend', e => {
        state.composing = false;
        if (e.data) dotnet.invokeMethodAsync('OnText', e.data);
    });
    on(host, 'focus', () => { readClipboard(dotnet); dotnet.invokeMethodAsync('OnFocus'); });

    const ro = new ResizeObserver(reportSize);
    ro.observe(host);
    cleanup.push(() => ro.disconnect());
    on(dark, 'change', reportSize);
    on(window, 'resize', reportSize);

    // Timers and animations need frames even when no input arrives: tick once per animation frame and sync the cursor.
    const loop = async () => {
        if (state.stopped) return;
        dotnet.invokeMethodAsync('Tick');
        const cursor = await dotnet.invokeMethodAsync('GetCursor');
        if (cursor !== state.cursor) { state.cursor = cursor; host.style.cursor = cursor; }
        if (!state.stopped) state.raf = requestAnimationFrame(loop);
    };
    state.raf = requestAnimationFrame(loop);
    cleanup.push(() => { state.stopped = true; cancelAnimationFrame(state.raf); });

    hosts.set(host, cleanup);
    reportSize();
}

export function detach(host) {
    for (const fn of hosts.get(host) ?? []) fn();
    hosts.delete(host);
}

// ---- file pickers ----
// A browser cannot hand out paths, so each chosen file becomes a blob: URL (playable and fetchable) plus its metadata.
function describe(file) {
    return { name: file.name, url: URL.createObjectURL(file), size: file.size, relativePath: file.webkitRelativePath || file.name };
}

function showPicker(configure) {
    return new Promise(resolve => {
        const input = document.createElement('input');
        input.type = 'file';
        input.style.display = 'none';
        configure(input);
        const done = files => { input.remove(); resolve(files); };
        input.addEventListener('change', () => done([...input.files].map(describe)));
        input.addEventListener('cancel', () => done([]));
        document.body.appendChild(input);
        input.click();
    });
}

export function pickFiles(accept, multiple) {
    return showPicker(input => { if (accept) input.accept = accept; input.multiple = multiple; });
}

export function pickFolder() {
    return showPicker(input => { input.webkitdirectory = true; });
}

export async function readBytes(url) {
    return new Uint8Array(await (await fetch(url)).arrayBuffer());
}
