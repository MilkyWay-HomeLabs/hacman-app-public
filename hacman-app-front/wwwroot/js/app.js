/**
 * HACMAN App Bootstrap
 *
 * Flow:
 * 1. GET /auth/user/bootstrap (server validates accessToken, returns Nebula userData)
 * 2. If 401 → POST /auth/refresh → retry or redirect to Nebula (max 2 attempts)
 * 3. Bootstrap OK:
 *    - save userData (Nebula) → localStorage['userData'] (only once)
 *    - GET {api}/v1/users/external-auth-id → hacmanUser
 *    - if 404 → POST {api}/v1/users → create hacmanUser
 *    - GET {api}/v1/user-galleries?userId=. → hacmanUserGalleries
 *    - save hacmanUser and hacmanUserGalleries → localStorage
 *    - dispatch 'hacman:user-bootstrap-ready'
 * 4. Non-auth error → dispatch 'hacman:auth-error' (no redirection)
 * 5. Network error and cache → use localStorage
 */

// ── Constants ─────────────────────────────────────────────────────────────────
const REDIRECT_COUNT_KEY    = 'hacman_redirect_count';

// Cached payloads so Blazor can replay them if it registers listeners AFTER the IIFE fires.
let _cachedBootstrapPayload = null;
let _cachedAuthError        = null;

// ── Helpers ───────────────────────────────────────────────────────────────────

function appBaseHref() {
    const base = document.querySelector('base');
    const href = base ? base.getAttribute('href') : '/';
    return href.endsWith('/') ? href : href + '/';
}

/** Hacman API URL from a meta tag injected by App.razor */
function getHacmanApiBaseUrl() {
    const meta = document.querySelector('meta[name="hacman-api-base-url"]');
    const url  = (meta ? meta.getAttribute('content') : null) || 'https://milkyway.test/hacman/api';
    return url.endsWith('/') ? url : url + '/';
}

/** Nebula Login URL from a meta tag injected by App.razor */
function getNebulaLoginUrl() {
    const meta = document.querySelector('meta[name="nebula-login-url"]');
    return (meta ? meta.getAttribute('content') : null) || 'https://milkyway.test/nebula/app/redirect?destination=Hacman';
}

/** Max redirect attempts from a meta tag injected by App.razor */
function getMaxRedirectAttempts() {
    const meta = document.querySelector('meta[name="max-redirect-attempts"]');
    const content = meta ? meta.getAttribute('content') : null;
    return parseInt(content || '2', 10);
}

function getRedirectCount()      { return parseInt(sessionStorage.getItem(REDIRECT_COUNT_KEY) || '0', 10); }
function resetRedirectCount()    { sessionStorage.removeItem(REDIRECT_COUNT_KEY); }
function incrementRedirectCount(){ const c = getRedirectCount() + 1; sessionStorage.setItem(REDIRECT_COUNT_KEY, String(c)); return c; }

function tryParseJson(v) { if (!v) return null; try { return JSON.parse(v); } catch { return null; } }

function dispatchAuthError(message) {
    console.error('[HACMAN] AUTH ERROR:', message);
    _cachedAuthError = message;
    window.dispatchEvent(new CustomEvent('hacman:auth-error', { detail: { message } }));
}

function redirectToLogin() {
    const count = incrementRedirectCount();
    const max   = getMaxRedirectAttempts();
    if (count > max) {
        dispatchAuthError(`Authentication failed after ${max} attempts. Please refresh the page or contact the administrator.`);
        return;
    }
    console.log(`[HACMAN] Redirecting to Nebula (attempt ${count}/${max})...`);
    window.location.href = getNebulaLoginUrl();
}

function emitBootstrapReady(userData, hacmanUser, hacmanUserGalleries) {
    const login = userData?.login ?? userData?.Login ?? null;
    const email = userData?.email ?? userData?.Email ?? null;
    const detail = { login, email, userData: userData ?? null, hacmanUser: hacmanUser ?? null, hacmanUserGalleries: hacmanUserGalleries ?? [] };
    _cachedBootstrapPayload = detail; // cache for late-registering Blazor listeners
    window.dispatchEvent(new CustomEvent('hacman:user-bootstrap-ready', { detail }));
    console.log('[HACMAN] ✓ Bootstrap ready | User:', login, '| Hacman ID:', hacmanUser?.id ?? 'none', '| Galleries:', (hacmanUserGalleries ?? []).length);
}

// ── Token validation (frontend server) ───────────────────────────────────────

async function doFetchBootstrap() {
    return fetch(appBaseHref() + 'auth/user/bootstrap', { credentials: 'include' });
}

async function doRefreshToken() {
    try {
        const r = await fetch(appBaseHref() + 'auth/refresh', { method: 'POST', credentials: 'include' });
        if (r.ok) { console.log('[HACMAN] Token refreshed.'); return true; }
        console.warn('[HACMAN] Refresh failed:', r.status); return false;
    } catch (e) { console.warn('[HACMAN] Network error during refresh:', e); return false; }
}

// ── Hacman API (browser → Traefik → backend) ────────────────────────────

async function fetchHacmanUser() {
    const url  = getHacmanApiBaseUrl() + 'v1/users/external-auth-id';
    console.log('[HACMAN] GET', url);
    const resp = await fetch(url, { credentials: 'include' });
    if (resp.ok)            return resp.json();
    if (resp.status === 404) return null;
    throw new Error(`GET users/external-auth-id → ${resp.status}: ${await resp.text()}`);
}

async function createHacmanUser(username) {
    const url  = getHacmanApiBaseUrl() + 'v1/users';
    console.log('[HACMAN] POST', url, '| username:', username);
    const resp = await fetch(url, {
        method: 'POST', credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ username })
    });
    if (resp.ok) return resp.json();
    throw new Error(`POST v1/users → ${resp.status}: ${await resp.text()}`);
}

async function fetchHacmanUserGalleries(userId) {
    const url  = getHacmanApiBaseUrl() + `v1/user-galleries?userId=${userId}`;
    console.log('[HACMAN] GET', url);
    const resp = await fetch(url, { credentials: 'include' });
    if (resp.ok) return resp.json();
    console.warn('[HACMAN] Galleries unavailable:', resp.status);
    return [];
}

// ── Main bootstrap (IIFE) ─────────────────────────────────────────────────────

(async () => {
    try {
        // 1. Token validation (frontend server)
        let resp = await doFetchBootstrap();

        if (resp.status === 401) {
            console.log('[HACMAN] Bootstrap 401 – attempting refresh...');
            resp = await doRefreshToken() ? await doFetchBootstrap() : null;
            if (!resp) { redirectToLogin(); return; }
        }

        if (!resp || resp.status === 401) { console.warn('[HACMAN] No tokens.'); redirectToLogin(); return; }

        if (!resp.ok) { dispatchAuthError(`Session error (HTTP ${resp.status}). Please try again.`); return; }

        resetRedirectCount();
        const bootstrap = await resp.json();
        console.log('[HACMAN] Bootstrap OK.');

        // 2. Nebula userData → localStorage (only once, do not overwrite)
        if (bootstrap.userData && !localStorage.getItem('userData')) {
            localStorage.setItem('userData', JSON.stringify(bootstrap.userData));
            console.log('[HACMAN] userData saved.');
        } else {
            console.log('[HACMAN] userData already exists – skipping.');
        }

        const login = bootstrap.userData?.login ?? bootstrap.userData?.Login ?? bootstrap.userData?.userName ?? null;

        // 3. Hacman user
        let hacmanUser = null;
        try {
            hacmanUser = await fetchHacmanUser();
            if (hacmanUser === null) {
                console.log('[HACMAN] Creating new Hacman user...');
                hacmanUser = await createHacmanUser(login ?? 'agent');
            }
            if (hacmanUser) {
                localStorage.setItem('hacmanUser', JSON.stringify(hacmanUser));
                console.log('[HACMAN] hacmanUser saved. ID:', hacmanUser.id);
            }
        } catch (e) { console.error('[HACMAN] hacmanUser error:', e); }

        // 4. Galleries
        let galleries = [];
        if (hacmanUser?.id) {
            try {
                galleries = await fetchHacmanUserGalleries(hacmanUser?.id);
                localStorage.setItem('hacmanUserGalleries', JSON.stringify(galleries));
                console.log('[HACMAN] Galleries saved. Count:', galleries.length);
            } catch (e) { console.error('[HACMAN] Galleries error:', e); }
        }

        emitBootstrapReady(bootstrap.userData, hacmanUser, galleries);

    } catch (e) {
        console.error('[HACMAN] Critical bootstrap error:', e);
        const cu = tryParseJson(localStorage.getItem('userData'));
        const ch = tryParseJson(localStorage.getItem('hacmanUser'));
        const cg = tryParseJson(localStorage.getItem('hacmanUserGalleries')) ?? [];
        if (cu && ch) { console.warn('[HACMAN] Cache offline.'); emitBootstrapReady(cu, ch, cg); }
        else          { dispatchAuthError('Network error. Please refresh the page.'); }
    }
})();

// ── Blazor interop ────────────────────────────────────────────────────────────

window.hacman_registerBootstrapListener = function (dotnetRef) {
    if (!dotnetRef) { console.warn('[HACMAN] dotnetRef is null'); return; }

    window.addEventListener('hacman:user-bootstrap-ready', function (e) {
        try { dotnetRef.invokeMethodAsync('ReceiveBootstrapData', JSON.stringify(e.detail)); }
        catch (err) { console.warn('[HACMAN] ReceiveBootstrapData error:', err); }
    });

    window.addEventListener('hacman:auth-error', function (e) {
        try { dotnetRef.invokeMethodAsync('ReceiveAuthError', e.detail?.message ?? 'Unknown error'); }
        catch (err) { console.warn('[HACMAN] ReceiveAuthError error:', err); }
    });

    // Replay cached payloads if the IIFE completed before Blazor registered listeners.
    // This is common because app.js runs immediately on page load while a Blazor circuit
    // establishment (OnAfterRenderAsync) happens later, especially with Long Polling fallback.
    if (_cachedBootstrapPayload) {
        console.log('[HACMAN] Replaying cached bootstrap payload to late-registering Blazor listener.');
        // IMPORTANT: Do NOT include hacmanUserGalleries in the replay.
        // 320+ gallery objects can easily exceed Blazor's SignalR message size limit (32 KB default)
        // and cause the circuit to be terminated with a 404 error, creating an infinite reconnection loop.
        // Blazor's ReceiveBootstrapData reads galleries from localStorage directly instead.
        const minimalPayload = {
            login:               _cachedBootstrapPayload.login,
            email:               _cachedBootstrapPayload.email,
            userData:            _cachedBootstrapPayload.userData,
            hacmanUser:          _cachedBootstrapPayload.hacmanUser,
            hacmanUserGalleries: []   // intentionally empty – Blazor reads localStorage
        };
        try { dotnetRef.invokeMethodAsync('ReceiveBootstrapData', JSON.stringify(minimalPayload)); }
        catch (err) { console.warn('[HACMAN] Replay ReceiveBootstrapData error:', err); }
    } else if (_cachedAuthError) {
        console.log('[HACMAN] Replaying cached auth error to late-registering Blazor listener.');
        try { dotnetRef.invokeMethodAsync('ReceiveAuthError', _cachedAuthError); }
        catch (err) { console.warn('[HACMAN] Replay ReceiveAuthError error:', err); }
    }
};
