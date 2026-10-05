// Media bridge for Sway.Media.Web: each player is an HTMLVideoElement (it plays audio files too) kept off-screen.
// Video frames are drawn to a canvas and copied out as RGBA for the .NET side to paint.
const players = new Map();
let nextId = 1;

// The picture is read back through a canvas, which needs the media to be CORS-clean; blob: and same-origin URLs always are.
const MAX_SIDE = 1920;

export function create() {
    const el = document.createElement('video');
    el.playsInline = true;
    el.preload = 'auto';
    const p = { el, stopped: true, dirty: false, canvas: null, ctx: null, retried: false, tainted: false };
    // Frames are only worth copying when the browser has a new one.
    if ('requestVideoFrameCallback' in el) {
        const tick = () => { p.dirty = true; el.requestVideoFrameCallback(tick); };
        el.requestVideoFrameCallback(tick);
    }
    el.addEventListener('seeked', () => { p.dirty = true; });
    el.addEventListener('loadeddata', () => { p.dirty = true; });
    el.addEventListener('error', () => {
        // A server without CORS headers refuses the anonymous request; the media still plays without a picture.
        if (!p.retried && el.crossOrigin) {
            p.retried = true;
            p.tainted = true;
            const src = el.src;
            el.removeAttribute('crossorigin');
            el.src = src;
            if (!p.stopped) el.play().catch(() => { });
        }
    });
    const id = nextId++;
    players.set(id, p);
    return id;
}

export function open(id, src) {
    const p = players.get(id);
    if (!p) return;
    const el = p.el;
    p.stopped = false;
    p.retried = false;
    p.tainted = false;
    p.dirty = true;
    const sameOrigin = src.startsWith('blob:') || src.startsWith('data:') || new URL(src, document.baseURI).origin === location.origin;
    if (sameOrigin) el.removeAttribute('crossorigin'); else el.crossOrigin = 'anonymous';
    el.src = src;
    el.load();
}

export function play(id) {
    const p = players.get(id);
    if (!p || !p.el.src) return;
    p.stopped = false;
    // Starting from the end replays; autoplay policy rejections leave the player paused and the user can press play again.
    if (p.el.ended) p.el.currentTime = 0;
    p.el.play().catch(() => { });
}

export function pause(id) { players.get(id)?.el.pause(); }

export function stop(id) {
    const p = players.get(id);
    if (!p) return;
    p.stopped = true;
    p.el.pause();
    if (p.el.readyState > 0) p.el.currentTime = 0;
}

export function seek(id, seconds) {
    const p = players.get(id);
    if (p && p.el.readyState > 0) p.el.currentTime = seconds;
}

export function setVolume(id, volume) { const p = players.get(id); if (p) p.el.volume = Math.min(1, Math.max(0, volume)); }
export function setMuted(id, muted) { const p = players.get(id); if (p) p.el.muted = muted; }
export function setLoop(id, loop) { const p = players.get(id); if (p) p.el.loop = loop; }

export function dispose(id) {
    const p = players.get(id);
    if (!p) return;
    p.el.pause();
    p.el.removeAttribute('src');
    p.el.load();
    players.delete(id);
}

// Status codes match Sway.Media.MediaStatus: 0 idle, 1 opening, 2 buffering, 3 playing, 4 paused, 5 ended, 6 error.
function status(p) {
    const el = p.el;
    if (el.error) return 6;
    if (!el.src || p.stopped) return el.src && el.readyState === 0 ? 1 : 0;
    if (el.readyState === 0) return 1;
    if (el.ended && !el.loop) return 5;
    if (el.paused) return 4;
    return el.readyState >= 3 ? 3 : 2;
}

function bufferedPercent(el) {
    if (!isFinite(el.duration) || el.duration <= 0 || el.buffered.length === 0) return 100;
    const end = el.buffered.end(el.buffered.length - 1);
    return Math.min(100, (end - el.currentTime) / Math.min(5, el.duration - el.currentTime || 5) * 100);
}

function frameSize(el) {
    let w = el.videoWidth, h = el.videoHeight;
    const k = Math.max(w, h) / MAX_SIDE;
    if (k > 1) { w = Math.round(w / k); h = Math.round(h / k); }
    return [w, h];
}

// [position seconds, duration seconds, status, buffered percent, frame width, frame height]
export function poll(id) {
    const p = players.get(id);
    if (!p) return [0, 0, 0, 100, 0, 0];
    const el = p.el;
    const [w, h] = frameSize(el);
    return [el.currentTime || 0, isFinite(el.duration) ? el.duration : 0, status(p), bufferedPercent(el), w, h];
}

// Copies the newest frame into dest (width * height * 4 bytes, RGBA). Returns false when there is nothing new.
export function copyFrame(id, dest) {
    const p = players.get(id);
    if (!p || p.tainted || !p.dirty || p.el.readyState < 2) return false;
    const [w, h] = frameSize(p.el);
    if (w === 0 || h === 0 || dest.length < w * h * 4) return false;
    if (!p.canvas) {
        p.canvas = document.createElement('canvas');
        p.ctx = p.canvas.getContext('2d', { willReadFrequently: true });
    }
    if (p.canvas.width !== w || p.canvas.height !== h) { p.canvas.width = w; p.canvas.height = h; }
    try {
        p.ctx.drawImage(p.el, 0, 0, w, h);
        dest.set(p.ctx.getImageData(0, 0, w, h).data);
        p.dirty = false;
        return true;
    } catch {
        p.tainted = true; // cross-origin media without CORS headers: audio plays, but the picture cannot be read
        return false;
    }
}
