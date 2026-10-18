// ==UserScript==
// @name         Tuna Universal v7.1
// @namespace    univrsal
// @version      7.1.0
// @description  Universal music metadata bridge for OBS runtime: Yandex, YouTube, YouTube Music, Spotify, Deezer, SoundCloud.
// @match        *://music.yandex.ru/*
// @match        *://music.yandex.com/*
// @match        *://open.spotify.com/*
// @match        *://soundcloud.com/*
// @match        *://www.deezer.com/*
// @match        *://*.youtube.com/*
// @match        *://music.youtube.com/*
// @grant        unsafeWindow
// @grant        GM_xmlhttpRequest
// @grant        GM.xmlHttpRequest
// @grant        GM_registerMenuCommand
// @connect      localhost
// @connect      127.0.0.1
// @license      GPLv2
// ==/UserScript==

(function () {
    'use strict';

    const SCRIPT_VERSION = '7.1.0';
    const STORAGE_KEY = 'streamer_os_tuna_config_v1';
    const DEFAULT_CONFIG = {
        host: '127.0.0.1',
        port: 1608,
        endpointPath: '/',
        refreshRateMs: 750,
        heartbeatMs: 15000,
        progressStepMs: 5000,
        debugEnabled: false,
        showDebugPanel: false,
        requestTimeoutMs: 2000
    };

    function normalizePath(value) {
        const raw = typeof value === 'string' && value.trim() ? value.trim() : '/';
        return raw.startsWith('/') ? raw : `/${raw}`;
    }

    function loadConfig() {
        try {
            const parsed = JSON.parse(localStorage.getItem(STORAGE_KEY) || '{}');
            return {
                host: parsed.host || DEFAULT_CONFIG.host,
                port: Number(parsed.port) || DEFAULT_CONFIG.port,
                endpointPath: normalizePath(parsed.endpointPath || DEFAULT_CONFIG.endpointPath),
                refreshRateMs: Math.max(250, Number(parsed.refreshRateMs) || DEFAULT_CONFIG.refreshRateMs),
                heartbeatMs: Math.max(2000, Number(parsed.heartbeatMs) || DEFAULT_CONFIG.heartbeatMs),
                progressStepMs: Math.max(1000, Number(parsed.progressStepMs) || DEFAULT_CONFIG.progressStepMs),
                debugEnabled: Boolean(parsed.debugEnabled),
                showDebugPanel: Boolean(parsed.showDebugPanel),
                requestTimeoutMs: Math.max(500, Number(parsed.requestTimeoutMs) || DEFAULT_CONFIG.requestTimeoutMs)
            };
        } catch (error) {
            console.warn('[Tuna] Failed to read config, falling back to defaults.', error);
            return { ...DEFAULT_CONFIG };
        }
    }

    let config = loadConfig();
    let debugBox = null;
    const runtimeState = {
        lastFingerprint: '',
        lastSentAt: 0,
        lastProgress: 0,
        lastStatus: '',
        lastErrorAt: 0
    };

    function saveConfig(nextConfig) {
        config = {
            ...config,
            ...nextConfig,
            endpointPath: normalizePath((nextConfig && nextConfig.endpointPath) || config.endpointPath)
        };
        localStorage.setItem(STORAGE_KEY, JSON.stringify(config));
        recreateDebugPanel();
        log(`Config saved. Endpoint: ${buildEndpointUrl()}`, 'info', true);
    }

    function resetConfig() {
        localStorage.removeItem(STORAGE_KEY);
        config = { ...DEFAULT_CONFIG };
        recreateDebugPanel();
        log('Config reset to defaults.', 'info', true);
    }

    function buildEndpointUrl() {
        return `http://${config.host}:${config.port}${normalizePath(config.endpointPath)}`;
    }

    function log(message, level = 'log', force = false) {
        if (!config.debugEnabled && !force) {
            return;
        }

        const fn = level === 'error' ? console.error : level === 'warn' ? console.warn : console.log;
        fn(`[Tuna ${SCRIPT_VERSION}] ${message}`);
    }

    function ensureDebugPanel() {
        if (!config.showDebugPanel) {
            if (debugBox) {
                debugBox.remove();
                debugBox = null;
            }
            return null;
        }

        if (!document.body) {
            return null;
        }

        if (!debugBox) {
            debugBox = document.createElement('div');
            debugBox.style.cssText = [
                'position:fixed',
                'top:0',
                'left:0',
                'width:340px',
                'background:rgba(0,0,0,0.86)',
                'color:#fff',
                'z-index:999999',
                'padding:10px 12px',
                'font-family:Consolas, monospace',
                'font-size:11px',
                'line-height:1.45',
                'border-left:4px solid #60a5fa',
                'border-radius:0 0 10px 0',
                'backdrop-filter:blur(10px)',
                'pointer-events:none'
            ].join(';');
            document.body.appendChild(debugBox);
        }

        return debugBox;
    }

    function recreateDebugPanel() {
        if (debugBox) {
            debugBox.remove();
            debugBox = null;
        }
        ensureDebugPanel();
    }

    function updateDebug(platform, title, progress, duration, isPlaying, extra) {
        const panel = ensureDebugPanel();
        if (!panel) {
            return;
        }

        const statusText = isPlaying ? 'PLAYING' : 'STOPPED';
        const statusColor = isPlaying ? '#22c55e' : '#ef4444';
        const lines = [
            `<b>Tuna ${SCRIPT_VERSION}</b> <span style="color:#94a3b8">(${platform})</span>`,
            `<span style="color:#e5e7eb">${title || 'No track detected'}</span>`,
            `<span style="color:#94a3b8">${Math.floor(progress / 1000)}s / ${Math.floor(duration / 1000)}s</span>`,
            `<span style="color:${statusColor}">${statusText}</span>`,
            `<span style="color:#60a5fa">${buildEndpointUrl()}</span>`
        ];

        if (extra) {
            lines.push(`<span style="color:#cbd5e1">${extra}</span>`);
        }

        panel.innerHTML = lines.join('<br>');
    }

    function registerMenuCommandCompat(label, handler) {
        if (typeof GM_registerMenuCommand === 'function') {
            GM_registerMenuCommand(label, handler);
            return;
        }

        if (typeof GM !== 'undefined' && typeof GM.registerMenuCommand === 'function') {
            GM.registerMenuCommand(label, handler);
        }
    }

    function promptForEndpoint() {
        const current = buildEndpointUrl();
        const input = window.prompt('Tuna endpoint URL', current);
        if (!input) {
            return;
        }

        try {
            const value = input.startsWith('http://') || input.startsWith('https://')
                ? new URL(input)
                : new URL(`http://${input}`);

            saveConfig({
                host: value.hostname,
                port: Number(value.port) || DEFAULT_CONFIG.port,
                endpointPath: normalizePath(value.pathname || '/')
            });
        } catch (error) {
            window.alert('Could not parse endpoint. Use something like 127.0.0.1:1608 or http://127.0.0.1:1608/.');
        }
    }

    function promptForNumber(label, key, minValue) {
        const input = window.prompt(label, String(config[key]));
        if (!input) {
            return;
        }

        const parsed = Number(input);
        if (!Number.isFinite(parsed) || parsed < minValue) {
            window.alert(`Please enter a number >= ${minValue}.`);
            return;
        }

        saveConfig({ [key]: parsed });
    }

    function registerMenuCommands() {
        registerMenuCommandCompat('Tuna: Set endpoint', promptForEndpoint);
        registerMenuCommandCompat('Tuna: Toggle debug logs', () => saveConfig({ debugEnabled: !config.debugEnabled }));
        registerMenuCommandCompat('Tuna: Toggle debug panel', () => saveConfig({ showDebugPanel: !config.showDebugPanel }));
        registerMenuCommandCompat('Tuna: Set refresh rate', () => promptForNumber('Refresh rate in ms', 'refreshRateMs', 250));
        registerMenuCommandCompat('Tuna: Set heartbeat interval', () => promptForNumber('Heartbeat interval in ms', 'heartbeatMs', 2000));
        registerMenuCommandCompat('Tuna: Reset settings', resetConfig);
    }

    function makeRequest(options) {
        if (typeof GM_xmlhttpRequest === 'function') {
            return GM_xmlhttpRequest(options);
        }

        if (typeof GM !== 'undefined' && typeof GM.xmlHttpRequest === 'function') {
            return GM.xmlHttpRequest(options);
        }

        console.error('[Tuna] GM_xmlhttpRequest is unavailable. Install Tampermonkey or Violentmonkey.');
        return null;
    }

    function query(selector, fn, fallback = null) {
        const el = document.querySelector(selector);
        return el ? fn(el) : fallback;
    }

    function timestampToMs(ts) {
        if (!ts) return 0;
        const parts = ts.split(':').map(Number);
        if (parts.length === 2) return (parts[0] * 60 + parts[1]) * 1000;
        if (parts.length === 3) return (parts[0] * 3600 + parts[1] * 60 + parts[2]) * 1000;
        return 0;
    }

    function cleanText(value) {
        return typeof value === 'string' ? value.trim() : '';
    }

    function normalizeArtists(artists) {
        if (!Array.isArray(artists)) {
            return [];
        }
        return artists.map(cleanText).filter(Boolean);
    }

    function normalizeTrack(track) {
        if (!track || typeof track !== 'object') {
            return null;
        }

        const title = cleanText(track.title);
        const artist = cleanText(track.artist);
        const cover = cleanText(track.cover);

        if (!title && !artist && !cover) {
            return null;
        }

        return { title, artist, cover };
    }

    function normalizePayload(data) {
        const artists = normalizeArtists(data.artists);
        const normalized = {
            source: cleanText(data.source),
            title: cleanText(data.title),
            artists,
            album: cleanText(data.album),
            cover: cleanText(data.cover || data.cover_url),
            cover_url: cleanText(data.cover_url || data.cover),
            album_url: cleanText(data.album_url),
            status: data.status === 'playing' ? 'playing' : 'stopped',
            progress: Math.max(0, Math.floor(Number(data.progress) || 0)),
            duration: Math.max(0, Math.floor(Number(data.duration) || 0)),
            next_track: normalizeTrack(data.next_track)
        };

        if (!normalized.title && normalized.status !== 'playing') {
            normalized.artists = [];
            normalized.album = '';
            normalized.cover = '';
            normalized.cover_url = '';
            normalized.album_url = '';
            normalized.next_track = null;
            normalized.progress = 0;
            normalized.duration = 0;
        }

        return normalized;
    }

    function fingerprintFor(data) {
        return JSON.stringify({
            source: data.source,
            title: data.title,
            artists: data.artists,
            album: data.album,
            cover: data.cover,
            status: data.status,
            album_url: data.album_url,
            next_track: data.next_track
        });
    }

    function shouldPost(data, now) {
        const fingerprint = fingerprintFor(data);
        const progressDelta = Math.abs((data.progress || 0) - runtimeState.lastProgress);
        const statusChanged = runtimeState.lastStatus !== data.status;
        const fingerprintChanged = runtimeState.lastFingerprint !== fingerprint;
        const heartbeatExpired = now - runtimeState.lastSentAt >= config.heartbeatMs;
        const progressStepReached = data.status === 'playing' && progressDelta >= config.progressStepMs;

        if (!runtimeState.lastSentAt) {
            return true;
        }

        return statusChanged || fingerprintChanged || progressStepReached || heartbeatExpired;
    }

    function post(data) {
        const normalized = normalizePayload(data);
        const now = Date.now();
        if (!shouldPost(normalized, now)) {
            return;
        }

        runtimeState.lastFingerprint = fingerprintFor(normalized);
        runtimeState.lastSentAt = now;
        runtimeState.lastProgress = normalized.progress || 0;
        runtimeState.lastStatus = normalized.status;

        const payload = {
            data: normalized,
            hostname: window.location.hostname,
            href: window.location.href,
            date: now,
            version: SCRIPT_VERSION
        };

        makeRequest({
            method: 'POST',
            url: buildEndpointUrl(),
            timeout: config.requestTimeoutMs,
            headers: { 'Content-Type': 'application/json' },
            data: JSON.stringify(payload),
            onload: () => {
                log(`Posted ${normalized.status} ${normalized.source} -> ${buildEndpointUrl()}`);
            },
            onerror: () => {
                if (now - runtimeState.lastErrorAt > 15000) {
                    runtimeState.lastErrorAt = now;
                    log(`Connection error while posting to ${buildEndpointUrl()}`, 'warn', true);
                }
            },
            ontimeout: () => {
                if (now - runtimeState.lastErrorAt > 15000) {
                    runtimeState.lastErrorAt = now;
                    log(`Request timeout while posting to ${buildEndpointUrl()}`, 'warn', true);
                }
            }
        });
    }

    let yandexLastProgress = -1;
    let yandexLastChange = Date.now();
    let yandexStatus = 'stopped';

    function handleYandex() {
        const slider = document.querySelector('input[type="range"][aria-label*="time code"]') ||
            document.querySelector('input[type="range"][class*="ChangeTimecode"]');

        let progress = 0;
        let duration = 0;
        if (slider) {
            progress = parseInt(slider.value, 10) * 1000;
            duration = parseInt(slider.max, 10) * 1000;
        } else {
            const pEl = document.querySelector('span[class*="Timecode_root_start"]');
            const dEl = document.querySelector('span[class*="Timecode_root_end"]');
            if (pEl) progress = timestampToMs(pEl.innerText);
            if (dEl) duration = timestampToMs(dEl.innerText);
        }

        const now = Date.now();
        if (progress !== yandexLastProgress && progress > 0) {
            yandexLastChange = now;
            yandexStatus = 'playing';
        } else if (now - yandexLastChange > 1200) {
            yandexStatus = 'stopped';
        }
        yandexLastProgress = progress;

        const meta = navigator.mediaSession?.metadata || {};
        const title = meta.title || 'Unknown';
        const artists = meta.artist ? [meta.artist] : [];
        const album = meta.album || '';

        let cover = '';
        const bigImg = document.querySelector('img[class*="FullscreenPlayer"][class*="cover"]') ||
            document.querySelector('img[class*="DesktopPoster_cover"]');
        if (bigImg) cover = bigImg.src;
        else if (meta.artwork?.length) cover = meta.artwork[meta.artwork.length - 1].src;
        if (cover) cover = cover.replace('%%', '1000x1000').replace(/\/\d+x\d+/, '/1000x1000');

        let nextTrack = null;
        try {
            const queueBlock = document.querySelector('.PlayQueueAfterPlayingBlock_root__A7_wI');
            if (queueBlock) {
                const nextTitle = queueBlock.querySelector('[class*="Meta_title"]');
                const nextArtist = queueBlock.querySelector('[class*="Meta_artistCaption"]');
                const nextImg = queueBlock.querySelector('img');
                if (nextTitle && nextArtist) {
                    let nextCover = nextImg?.src || '';
                    if (nextCover) nextCover = nextCover.replace(/\/\d+x\d+/, '/200x200');
                    nextTrack = {
                        title: nextTitle.innerText.trim(),
                        artist: nextArtist.innerText.trim(),
                        cover: nextCover
                    };
                }
            }
        } catch (error) {
            log(`Yandex next-track parse failed: ${error.message}`, 'warn');
        }

        const extra = nextTrack ? `Next: ${nextTrack.artist} - ${nextTrack.title}` : '';
        updateDebug('Yandex', title, progress, duration, yandexStatus === 'playing', extra);

        post({
            source: 'yandex',
            cover,
            title,
            artists,
            status: yandexStatus,
            progress,
            duration,
            album_url: window.location.href,
            album,
            next_track: nextTrack
        });
    }

    function handleSpotify() {
        if (!navigator.mediaSession?.metadata) return;

        const meta = navigator.mediaSession.metadata;
        const status = query('[data-testid="control-button-playpause"]', (e) =>
            e.getAttribute('aria-label')?.toLowerCase().includes('pause') ? 'playing' : 'stopped', 'stopped');
        const cover = meta.artwork?.[0]?.src || '';
        const title = meta.title;
        const artists = [meta.artist];
        const album = meta.album;
        const progress = query('[data-testid="playback-position"]', (e) => timestampToMs(e.textContent), 0);
        const duration = query('[data-testid="playback-duration"]', (e) => timestampToMs(e.textContent), 0);

        updateDebug('Spotify', title, progress, duration, status === 'playing', '');
        post({ source: 'spotify', cover, title, artists, status, progress, duration, album });
    }

    function handleYouTube() {
        const video = document.querySelector('video');
        if (!video) return;

        let artists = [];
        try {
            const authorEl = document.querySelector('#text > a') ||
                document.querySelector('#owner #channel-name a') ||
                document.querySelector('ytd-video-owner-renderer #channel-name a');
            if (authorEl) artists = [authorEl.textContent?.trim() || authorEl.innerHTML?.trim()];
        } catch (error) {
            log(`YouTube artist parse failed: ${error.message}`, 'warn');
        }

        let title = '';
        try {
            const titleEl = document.querySelector('#container > h1 > yt-formatted-string') ||
                document.querySelector('h1.ytd-video-primary-info-renderer') ||
                document.querySelector('ytd-watch-metadata h1 yt-formatted-string');
            title = titleEl?.textContent?.trim() || titleEl?.innerHTML?.trim() || '';
        } catch (error) {
            log(`YouTube title parse failed: ${error.message}`, 'warn');
        }

        if (!title && navigator.mediaSession?.metadata) {
            title = navigator.mediaSession.metadata.title || '';
            if (!artists.length && navigator.mediaSession.metadata.artist) {
                artists = [navigator.mediaSession.metadata.artist];
            }
        }

        const duration = video.duration * 1000 || 0;
        const progress = video.currentTime * 1000 || 0;
        const status = video.paused ? 'stopped' : 'playing';

        let cover = '';
        let videoId = null;
        const match = window.location.toString().match(/(?:youtu\.be\/|v\/|u\/\w\/|embed\/|watch\?v=|&v=)([^#&?]*)/);
        if (match?.[1]?.length === 11) {
            videoId = match[1];
            cover = `https://i.ytimg.com/vi/${videoId}/maxresdefault.jpg`;
        }

        if (title && artists.length) {
            title = title.replace(`${artists[0]} - `, '').replace(` - ${artists[0]}`, '');
        }
        title = title.replace(/\(Official.*?\)/gi, '').replace(/\[Official.*?\]/gi, '').trim();

        let nextTrack = null;
        try {
            const nextBtn = document.querySelector('a.ytp-next-button[data-preview]');
            if (nextBtn) {
                const nextCover = nextBtn.getAttribute('data-preview') || '';
                const nextTitle = nextBtn.getAttribute('data-tooltip-text') || '';
                if (nextTitle) {
                    nextTrack = {
                        title: nextTitle,
                        artist: '',
                        cover: nextCover.replace('mqdefault', 'hqdefault')
                    };
                }
            }
        } catch (error) {
            log(`YouTube next-track parse failed: ${error.message}`, 'warn');
        }

        const extra = nextTrack ? `Next: ${nextTrack.title}` : '';
        updateDebug('YouTube', title, progress, duration, status === 'playing', extra);

        if (status === 'playing') {
            post({
                source: 'youtube',
                video_id: videoId,
                cover,
                title,
                artists,
                status,
                progress: Math.floor(progress),
                duration,
                next_track: nextTrack
            });
        } else {
            post({ source: 'youtube', status: 'stopped', title: '', artists: [], progress: 0, duration: 0 });
        }
    }

    function handleYouTubeMusic() {
        if (!navigator.mediaSession?.metadata) return;

        const artistsSelectors = [
            '.ytmusic-player-bar.byline [href*="channel/"]:not([href*="channel/MPREb_"])',
            '.ytmusic-player-bar.byline .yt-formatted-string:nth-child(2n+1):not([href*="browse/"])'
        ];
        const albumSelectors = [
            '.ytmusic-player-bar [href*="browse/MPREb_"]'
        ];

        const time = query('.ytmusic-player-bar.time-info', (e) => e.innerText.split(' / '), ['0:00', '0:00']);
        const status = navigator.mediaSession.playbackState === 'playing' ? 'playing' : 'stopped';
        const title = query('.ytmusic-player-bar.title', (e) => e.title, '');
        const artists = Array.from(document.querySelectorAll(artistsSelectors.join(','))).map((x) => x.innerText);
        const album = query(albumSelectors.join(','), (e) => e.textContent, '');
        const artwork = navigator.mediaSession.metadata?.artwork || [];
        const cover = artwork.length ? artwork[artwork.length - 1].src : '';
        const progress = timestampToMs(time[0]);
        const duration = timestampToMs(time[1]);

        let nextTrack = null;
        try {
            const queueItems = document.querySelectorAll('ytmusic-player-queue-item');
            if (queueItems.length > 0) {
                const nextItem = queueItems[0];
                const nextTitle = nextItem.querySelector('.song-title')?.textContent?.trim() || '';
                const nextArtist = nextItem.querySelector('.byline')?.textContent?.trim() || '';
                const nextImg = nextItem.querySelector('yt-img-shadow img');
                let nextCover = nextImg?.src || '';
                if (nextCover) nextCover = nextCover.replace(/\/\w+default\.jpg.*/, '/hqdefault.jpg');

                if (nextTitle) {
                    nextTrack = { title: nextTitle, artist: nextArtist, cover: nextCover };
                }
            }
        } catch (error) {
            log(`YouTube Music next-track parse failed: ${error.message}`, 'warn');
        }

        const extra = nextTrack ? `Next: ${nextTrack.artist} - ${nextTrack.title}` : '';
        updateDebug('YouTube Music', title, progress, duration, status === 'playing', extra);
        post({ source: 'youtube_music', cover, title, artists, status, progress, duration, album, next_track: nextTrack });
    }

    function handleSoundCloud() {
        const status = query('.playControl', (e) => e.classList.contains('playing') ? 'playing' : 'stopped', 'stopped');
        const cover = query('.playbackSoundBadge span.sc-artwork', (e) =>
            e.style.backgroundImage.slice(5, -2).replace('t50x50', 't500x500'), '');
        const title = query('.playbackSoundBadge__titleLink', (e) => e.title, '');
        const artists = [query('.playbackSoundBadge__lightLink', (e) => e.title, '')];
        const progress = query('.playbackTimeline__timePassed span:nth-child(2)', (e) => timestampToMs(e.textContent), 0);
        const duration = query('.playbackTimeline__duration span:nth-child(2)', (e) => timestampToMs(e.textContent), 0);

        if (!title) return;
        updateDebug('SoundCloud', title, progress, duration, status === 'playing', '');
        post({ source: 'soundcloud', cover, title, artists, status, progress, duration });
    }

    function handleDeezer() {
        const pauseBtn = document.querySelector('[data-testid="play_button_pause"]');
        const status = pauseBtn ? 'playing' : 'stopped';

        if (!navigator.mediaSession?.metadata) return;

        const meta = navigator.mediaSession.metadata;
        const cover = meta.artwork?.[0]?.src?.replace(/\d+x\d+/, '512x512') || '';
        const title = meta.title;
        const artists = meta.artist?.split(',').map((x) => x.trim()) || [];
        const album = meta.album;
        const slider = document.querySelector('input.slider-track-input.mousetrap');
        const progress = slider ? Math.round(slider.value * 1000) : 0;
        const duration = slider ? Math.round(slider.max * 1000) : 0;

        updateDebug('Deezer', title, progress, duration, status === 'playing', '');
        post({ source: 'deezer', cover, title, artists, status, progress, duration, album });
    }

    function tick() {
        const host = window.location.hostname;

        if (host.includes('music.yandex')) {
            handleYandex();
        } else if (host === 'open.spotify.com') {
            handleSpotify();
        } else if (host === 'music.youtube.com') {
            handleYouTubeMusic();
        } else if (host.includes('youtube.com')) {
            handleYouTube();
        } else if (host === 'soundcloud.com') {
            handleSoundCloud();
        } else if (host === 'www.deezer.com') {
            handleDeezer();
        }
    }

    registerMenuCommands();
    ensureDebugPanel();
    setInterval(tick, config.refreshRateMs);
    tick();
    log(`Universal script loaded for ${window.location.hostname}. Endpoint: ${buildEndpointUrl()}`, 'info', true);
})();
