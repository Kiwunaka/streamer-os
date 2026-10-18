// ==UserScript==
// @name         Tuna Universal v7.1
// @namespace    univrsal
// @version      7.1.0
// @description  Universal music info bridge for OBS runtime - Yandex, YouTube, YouTube Music, Spotify, Deezer, SoundCloud
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
// @grant        GM.registerMenuCommand
// @grant        GM_getValue
// @grant        GM.getValue
// @grant        GM_setValue
// @grant        GM.setValue
// @connect      localhost
// @connect      127.0.0.1
// @license      GPLv2
// ==/UserScript==

(function () {
    'use strict';

    const SCRIPT_VERSION = '7.1.0';
    const STORAGE_PREFIX = 'tuna-universal-v7';
    const DEFAULTS = Object.freeze({
        host: '127.0.0.1',
        port: 1608,
        endpoint: '/',
        refreshRateMs: 500,
        playingHeartbeatMs: 4000,
        stoppedHeartbeatMs: 12000,
        progressThresholdMs: 1500,
        debugEnabled: false,
        consoleLogging: false
    });

    let config = { ...DEFAULTS };
    let debugBox = null;
    let lastPostedFingerprint = '';
    let lastPostedAt = 0;
    let lastStatus = 'stopped';
    let requestFailures = 0;
    let isPosting = false;
    let lastProgress = 0;

    function log(message, extra) {
        if (!config.consoleLogging) {
            return;
        }

        if (typeof extra === 'undefined') {
            console.log(`[Tuna v${SCRIPT_VERSION}] ${message}`);
        } else {
            console.log(`[Tuna v${SCRIPT_VERSION}] ${message}`, extra);
        }
    }

    function warn(message, extra) {
        if (typeof extra === 'undefined') {
            console.warn(`[Tuna v${SCRIPT_VERSION}] ${message}`);
        } else {
            console.warn(`[Tuna v${SCRIPT_VERSION}] ${message}`, extra);
        }
    }

    function gmGetValue(key, fallbackValue) {
        try {
            if (typeof GM_getValue === 'function') {
                return GM_getValue(key, fallbackValue);
            }
            if (typeof GM !== 'undefined' && typeof GM.getValue === 'function') {
                return GM.getValue(key, fallbackValue);
            }
        } catch (error) {
            warn(`Failed to read setting "${key}".`, error);
        }
        return fallbackValue;
    }

    function gmSetValue(key, value) {
        try {
            if (typeof GM_setValue === 'function') {
                GM_setValue(key, value);
                return;
            }
            if (typeof GM !== 'undefined' && typeof GM.setValue === 'function') {
                GM.setValue(key, value);
                return;
            }
        } catch (error) {
            warn(`Failed to write setting "${key}".`, error);
        }
    }

    function registerMenuCommand(label, handler) {
        try {
            if (typeof GM_registerMenuCommand === 'function') {
                GM_registerMenuCommand(label, handler);
                return;
            }
            if (typeof GM !== 'undefined' && typeof GM.registerMenuCommand === 'function') {
                GM.registerMenuCommand(label, handler);
            }
        } catch (error) {
            warn(`Failed to register menu command "${label}".`, error);
        }
    }

    async function loadConfig() {
        const loaded = {};
        for (const [key, fallback] of Object.entries(DEFAULTS)) {
            const stored = await gmGetValue(`${STORAGE_PREFIX}:${key}`, fallback);
            loaded[key] = normalizeConfigValue(stored, fallback);
        }
        config = loaded;
    }

    function normalizeConfigValue(value, fallback) {
        if (typeof fallback === 'number') {
            const numeric = Number(value);
            return Number.isFinite(numeric) ? numeric : fallback;
        }
        if (typeof fallback === 'boolean') {
            return Boolean(value);
        }
        return typeof value === 'string' && value.length > 0 ? value : fallback;
    }

    function saveConfigEntry(key, value) {
        config[key] = value;
        gmSetValue(`${STORAGE_PREFIX}:${key}`, value);
        if (key === 'debugEnabled') {
            if (value) {
                ensureDebugBox();
            } else if (debugBox) {
                debugBox.remove();
                debugBox = null;
            }
        }
    }

    function ensureDebugBox() {
        if (!config.debugEnabled || debugBox) {
            return;
        }

        debugBox = document.createElement('div');
        debugBox.style.cssText = [
            'position: fixed',
            'top: 0',
            'left: 0',
            'width: 340px',
            'background: rgba(10, 10, 10, 0.92)',
            'color: #f8fafc',
            'z-index: 2147483647',
            'padding: 10px 12px',
            'font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace',
            'font-size: 11px',
            'line-height: 1.45',
            'border-left: 4px solid #0ea5e9',
            'border-radius: 0 0 10px 0',
            'backdrop-filter: blur(10px)',
            'box-shadow: 0 10px 30px rgba(0, 0, 0, 0.35)'
        ].join(';');
        debugBox.innerHTML = 'TUNA v7.1 is loading...';
        document.body.appendChild(debugBox);
    }

    function updateDebug(state) {
        if (!config.debugEnabled) {
            return;
        }

        ensureDebugBox();
        if (!debugBox) {
            return;
        }

        const isPlaying = state.status === 'playing';
        const color = isPlaying ? '#22c55e' : '#ef4444';
        const nextTrack = state.next_track && state.next_track.title
            ? `Next: ${[state.next_track.artist, state.next_track.title].filter(Boolean).join(' - ')}`
            : 'Next: none';
        const progress = `${Math.floor((state.progress || 0) / 1000)}s`;
        const duration = `${Math.floor((state.duration || 0) / 1000)}s`;

        debugBox.innerHTML = [
            `<b>TUNA v${SCRIPT_VERSION}</b> <span style="color:#94a3b8">(${state.source || 'unknown'})</span>`,
            `<div style="margin-top:4px;color:#ffffff">${escapeHtml(state.title || '(no track)')}</div>`,
            `<div style="color:#cbd5e1">${escapeHtml((state.artists || []).join(', ') || '(no artist)')}</div>`,
            `<div style="margin-top:4px;color:#94a3b8">${progress} / ${duration}</div>`,
            `<div style="color:${color};font-weight:700">${isPlaying ? 'PLAYING' : 'STOPPED'}</div>`,
            `<div style="margin-top:4px;color:#94a3b8">${escapeHtml(nextTrack)}</div>`,
            `<div style="margin-top:6px;color:#64748b">POST ${escapeHtml(buildUrl())}</div>`
        ].join('');
    }

    function escapeHtml(value) {
        return String(value)
            .replaceAll('&', '&amp;')
            .replaceAll('<', '&lt;')
            .replaceAll('>', '&gt;')
            .replaceAll('"', '&quot;')
            .replaceAll("'", '&#39;');
    }

    function buildUrl() {
        const endpoint = (config.endpoint || '/').startsWith('/') ? config.endpoint : `/${config.endpoint}`;
        return `http://${config.host}:${config.port}${endpoint}`;
    }

    function makeRequest(data) {
        if (typeof GM_xmlhttpRequest === 'function') {
            return GM_xmlhttpRequest(data);
        }
        if (typeof GM !== 'undefined' && GM.xmlHttpRequest) {
            return GM.xmlHttpRequest(data);
        }

        warn('No GM_xmlhttpRequest implementation available.');
        return null;
    }

    function query(selector, getter, fallback = null) {
        const element = document.querySelector(selector);
        return element ? getter(element) : fallback;
    }

    function timestampToMs(timestamp) {
        if (!timestamp) {
            return 0;
        }

        const parts = String(timestamp)
            .trim()
            .split(':')
            .map(Number)
            .filter(part => Number.isFinite(part));

        if (parts.length === 2) {
            return (parts[0] * 60 + parts[1]) * 1000;
        }
        if (parts.length === 3) {
            return (parts[0] * 3600 + parts[1] * 60 + parts[2]) * 1000;
        }

        return 0;
    }

    function sanitizeText(value) {
        return typeof value === 'string' ? value.trim() : '';
    }

    function sanitizeArray(value) {
        if (!Array.isArray(value)) {
            return [];
        }
        return value.map(sanitizeText).filter(Boolean);
    }

    function normalizeNextTrack(nextTrack) {
        if (!nextTrack || typeof nextTrack !== 'object') {
            return null;
        }

        const normalized = {
            title: sanitizeText(nextTrack.title),
            artist: sanitizeText(nextTrack.artist),
            cover: sanitizeText(nextTrack.cover)
        };

        return normalized.title ? normalized : null;
    }

    function normalizePayload(data) {
        return {
            source: sanitizeText(data.source),
            title: sanitizeText(data.title),
            artists: sanitizeArray(data.artists),
            album: sanitizeText(data.album),
            cover: sanitizeText(data.cover),
            cover_url: sanitizeText(data.cover_url || data.cover),
            album_url: sanitizeText(data.album_url),
            status: sanitizeText(data.status || 'stopped') || 'stopped',
            progress: Math.max(0, Math.round(Number(data.progress) || 0)),
            duration: Math.max(0, Math.round(Number(data.duration) || 0)),
            next_track: normalizeNextTrack(data.next_track),
            video_id: sanitizeText(data.video_id)
        };
    }

    function buildFingerprint(data) {
        return JSON.stringify({
            source: data.source,
            status: data.status,
            title: data.title,
            artists: data.artists,
            album: data.album,
            cover_url: data.cover_url,
            album_url: data.album_url,
            video_id: data.video_id,
            next_track: data.next_track
        });
    }

    function shouldPost(data) {
        const now = Date.now();
        const fingerprint = buildFingerprint(data);
        const trackChanged = fingerprint !== lastPostedFingerprint;
        const stateChanged = data.status !== lastStatus;
        const progressDelta = Math.abs(data.progress - lastProgress);
        const heartbeatWindow = data.status === 'playing'
            ? config.playingHeartbeatMs
            : config.stoppedHeartbeatMs;
        const heartbeatDue = now - lastPostedAt >= heartbeatWindow;
        const progressSignificant = data.status === 'playing' && progressDelta >= config.progressThresholdMs;

        lastProgress = data.progress;

        if (trackChanged || stateChanged) {
            return true;
        }
        if (heartbeatDue) {
            return true;
        }
        if (progressSignificant && now - lastPostedAt >= Math.min(config.refreshRateMs * 2, 1200)) {
            return true;
        }

        return false;
    }

    function post(data) {
        const normalized = normalizePayload(data);
        updateDebug(normalized);

        if (isPosting || !shouldPost(normalized)) {
            return;
        }

        const requestBody = {
            data: normalized,
            hostname: window.location.hostname,
            pathname: window.location.pathname,
            date: Date.now(),
            scriptVersion: SCRIPT_VERSION
        };

        isPosting = true;
        makeRequest({
            method: 'POST',
            url: buildUrl(),
            headers: { 'Content-Type': 'application/json' },
            data: JSON.stringify(requestBody),
            onload: response => {
                isPosting = false;
                requestFailures = 0;
                lastPostedFingerprint = buildFingerprint(normalized);
                lastPostedAt = Date.now();
                lastStatus = normalized.status;
                log(`POST ${response.status} ${buildUrl()}`, normalized);
            },
            onerror: error => {
                isPosting = false;
                requestFailures += 1;
                if (requestFailures <= 3 || requestFailures % 10 === 0) {
                    warn(`POST failed (${requestFailures}) ${buildUrl()}`, error);
                }
            },
            ontimeout: () => {
                isPosting = false;
                requestFailures += 1;
                warn(`POST timeout (${requestFailures}) ${buildUrl()}`);
            },
            timeout: 3000
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
            const progressElement = document.querySelector('span[class*="Timecode_root_start"]');
            const durationElement = document.querySelector('span[class*="Timecode_root_end"]');
            if (progressElement) {
                progress = timestampToMs(progressElement.innerText);
            }
            if (durationElement) {
                duration = timestampToMs(durationElement.innerText);
            }
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
        const bigCover = document.querySelector('img[class*="FullscreenPlayer"][class*="cover"]') ||
            document.querySelector('img[class*="DesktopPoster_cover"]');
        if (bigCover) {
            cover = bigCover.src;
        } else if (meta.artwork?.length) {
            cover = meta.artwork[meta.artwork.length - 1].src;
        }
        if (cover) {
            cover = cover.replace('%%', '1000x1000').replace(/\/\d+x\d+/, '/1000x1000');
        }

        let nextTrack = null;
        try {
            const queueBlock = document.querySelector('.PlayQueueAfterPlayingBlock_root__A7_wI');
            if (queueBlock) {
                const nextTitle = queueBlock.querySelector('[class*="Meta_title"]');
                const nextArtist = queueBlock.querySelector('[class*="Meta_artistCaption"]');
                const nextImage = queueBlock.querySelector('img');
                if (nextTitle && nextArtist) {
                    let nextCover = nextImage?.src || '';
                    if (nextCover) {
                        nextCover = nextCover.replace(/\/\d+x\d+/, '/200x200');
                    }
                    nextTrack = {
                        title: nextTitle.innerText.trim(),
                        artist: nextArtist.innerText.trim(),
                        cover: nextCover
                    };
                }
            }
        } catch (error) {
            log('Yandex next-track parsing failed.', error);
        }

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
        if (!navigator.mediaSession?.metadata) {
            return;
        }

        const meta = navigator.mediaSession.metadata;
        const status = query('[data-testid="control-button-playpause"]', element =>
            element.getAttribute('aria-label')?.toLowerCase().includes('pause') ? 'playing' : 'stopped', 'stopped');
        const cover = meta.artwork?.[0]?.src || '';
        const title = meta.title || '';
        const artists = meta.artist ? [meta.artist] : [];
        const album = meta.album || '';
        const progress = query('[data-testid="playback-position"]', element => timestampToMs(element.textContent), 0);
        const duration = query('[data-testid="playback-duration"]', element => timestampToMs(element.textContent), 0);

        post({ source: 'spotify', cover, title, artists, status, progress, duration, album });
    }

    function handleYouTube() {
        const video = document.querySelector('video');
        if (!video) {
            return;
        }

        let artists = [];
        try {
            const author = document.querySelector('#text > a') ||
                document.querySelector('#owner #channel-name a') ||
                document.querySelector('ytd-video-owner-renderer #channel-name a');
            if (author) {
                artists = [author.textContent?.trim() || author.innerHTML?.trim() || ''];
            }
        } catch (error) {
            log('YouTube artist parsing failed.', error);
        }

        let title = '';
        try {
            const titleElement = document.querySelector('#container > h1 > yt-formatted-string') ||
                document.querySelector('h1.ytd-video-primary-info-renderer') ||
                document.querySelector('ytd-watch-metadata h1 yt-formatted-string');
            title = titleElement?.textContent?.trim() || titleElement?.innerHTML?.trim() || '';
        } catch (error) {
            log('YouTube title parsing failed.', error);
        }

        if (!title && navigator.mediaSession?.metadata) {
            title = navigator.mediaSession.metadata.title || '';
            if (!artists.length && navigator.mediaSession.metadata.artist) {
                artists = [navigator.mediaSession.metadata.artist];
            }
        }

        const duration = Math.round((video.duration || 0) * 1000);
        const progress = Math.round((video.currentTime || 0) * 1000);
        const status = video.paused ? 'stopped' : 'playing';

        let cover = '';
        let videoId = '';
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
            const nextButton = document.querySelector('a.ytp-next-button[data-preview]');
            if (nextButton) {
                const nextTitle = nextButton.getAttribute('data-tooltip-text') || '';
                const nextCover = (nextButton.getAttribute('data-preview') || '').replace('mqdefault', 'hqdefault');
                if (nextTitle) {
                    nextTrack = {
                        title: nextTitle,
                        artist: '',
                        cover: nextCover
                    };
                }
            }
        } catch (error) {
            log('YouTube next-track parsing failed.', error);
        }

        if (status === 'playing') {
            post({
                source: 'youtube',
                video_id: videoId,
                cover,
                title,
                artists,
                status,
                progress,
                duration,
                next_track: nextTrack
            });
            return;
        }

        post({
            source: 'youtube',
            status: 'stopped',
            title: '',
            artists: [],
            progress: 0,
            duration: 0
        });
    }

    function handleYouTubeMusic() {
        if (!navigator.mediaSession?.metadata) {
            return;
        }

        const artistsSelectors = [
            '.ytmusic-player-bar.byline [href*="channel/"]:not([href*="channel/MPREb_"])',
            '.ytmusic-player-bar.byline .yt-formatted-string:nth-child(2n+1):not([href*="browse/"])'
        ];
        const albumSelectors = ['.ytmusic-player-bar [href*="browse/MPREb_"]'];
        const time = query('.ytmusic-player-bar.time-info', element => element.innerText.split(' / '), ['0:00', '0:00']);
        const status = navigator.mediaSession.playbackState === 'playing' ? 'playing' : 'stopped';
        const title = query('.ytmusic-player-bar.title', element => element.title, '');
        const artists = Array.from(document.querySelectorAll(artistsSelectors.join(','))).map(element => element.innerText);
        const album = query(albumSelectors.join(','), element => element.textContent, '');
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
                let nextCover = nextItem.querySelector('yt-img-shadow img')?.src || '';
                if (nextCover) {
                    nextCover = nextCover.replace(/\/\w+default\.jpg.*/, '/hqdefault.jpg');
                }
                if (nextTitle) {
                    nextTrack = { title: nextTitle, artist: nextArtist, cover: nextCover };
                }
            }
        } catch (error) {
            log('YouTube Music next-track parsing failed.', error);
        }

        post({ source: 'youtube_music', cover, title, artists, status, progress, duration, album, next_track: nextTrack });
    }

    function handleSoundCloud() {
        const status = query('.playControl', element => element.classList.contains('playing') ? 'playing' : 'stopped', 'stopped');
        const cover = query('.playbackSoundBadge span.sc-artwork', element =>
            element.style.backgroundImage.slice(5, -2).replace('t50x50', 't500x500'), '');
        const title = query('.playbackSoundBadge__titleLink', element => element.title, '');
        const artist = query('.playbackSoundBadge__lightLink', element => element.title, '');
        const progress = query('.playbackTimeline__timePassed span:nth-child(2)', element => timestampToMs(element.textContent), 0);
        const duration = query('.playbackTimeline__duration span:nth-child(2)', element => timestampToMs(element.textContent), 0);

        if (!title) {
            return;
        }

        post({ source: 'soundcloud', cover, title, artists: artist ? [artist] : [], status, progress, duration });
    }

    function handleDeezer() {
        const pauseButton = document.querySelector('[data-testid="play_button_pause"]');
        const status = pauseButton ? 'playing' : 'stopped';

        if (!navigator.mediaSession?.metadata) {
            return;
        }

        const meta = navigator.mediaSession.metadata;
        const cover = meta.artwork?.[0]?.src?.replace(/\d+x\d+/, '512x512') || '';
        const title = meta.title || '';
        const artists = meta.artist?.split(',').map(value => value.trim()).filter(Boolean) || [];
        const album = meta.album || '';
        const slider = document.querySelector('input.slider-track-input.mousetrap');
        const progress = slider ? Math.round(Number(slider.value || 0) * 1000) : 0;
        const duration = slider ? Math.round(Number(slider.max || 0) * 1000) : 0;

        post({ source: 'deezer', cover, title, artists, status, progress, duration, album });
    }

    function promptNumber(label, currentValue, minimumValue) {
        const raw = window.prompt(label, String(currentValue));
        if (raw === null) {
            return null;
        }

        const value = Number(raw);
        if (!Number.isFinite(value) || value < minimumValue) {
            window.alert(`Invalid value: ${raw}`);
            return null;
        }
        return value;
    }

    function promptText(label, currentValue) {
        const raw = window.prompt(label, currentValue);
        if (raw === null) {
            return null;
        }
        return raw.trim();
    }

    function installMenu() {
        registerMenuCommand('Tuna: Show current settings', () => {
            window.alert([
                `Host: ${config.host}`,
                `Port: ${config.port}`,
                `Endpoint: ${config.endpoint}`,
                `Refresh rate: ${config.refreshRateMs} ms`,
                `Playing heartbeat: ${config.playingHeartbeatMs} ms`,
                `Stopped heartbeat: ${config.stoppedHeartbeatMs} ms`,
                `Progress threshold: ${config.progressThresholdMs} ms`,
                `Debug: ${config.debugEnabled ? 'on' : 'off'}`,
                `Console logging: ${config.consoleLogging ? 'on' : 'off'}`
            ].join('\n'));
        });

        registerMenuCommand('Tuna: Set OBS host', () => {
            const value = promptText('OBS runtime host', config.host);
            if (value) {
                saveConfigEntry('host', value);
            }
        });

        registerMenuCommand('Tuna: Set OBS port', () => {
            const value = promptNumber('OBS runtime port', config.port, 1);
            if (value !== null) {
                saveConfigEntry('port', value);
            }
        });

        registerMenuCommand('Tuna: Set endpoint', () => {
            const value = promptText('HTTP endpoint path', config.endpoint);
            if (value) {
                saveConfigEntry('endpoint', value.startsWith('/') ? value : `/${value}`);
            }
        });

        registerMenuCommand('Tuna: Set refresh rate', () => {
            const value = promptNumber('Polling interval in ms', config.refreshRateMs, 100);
            if (value !== null) {
                saveConfigEntry('refreshRateMs', value);
            }
        });

        registerMenuCommand(`Tuna: Toggle debug (${config.debugEnabled ? 'on' : 'off'})`, () => {
            saveConfigEntry('debugEnabled', !config.debugEnabled);
            window.alert(`Debug is now ${config.debugEnabled ? 'on' : 'off'}. Reload the page if the menu label did not update.`);
        });

        registerMenuCommand(`Tuna: Toggle console logging (${config.consoleLogging ? 'on' : 'off'})`, () => {
            saveConfigEntry('consoleLogging', !config.consoleLogging);
            window.alert(`Console logging is now ${config.consoleLogging ? 'on' : 'off'}. Reload the page if the menu label did not update.`);
        });

        registerMenuCommand('Tuna: Reset settings', () => {
            Object.entries(DEFAULTS).forEach(([key, value]) => {
                saveConfigEntry(key, value);
            });
            window.alert('Tuna settings were reset. Reload the page to rebuild the menu labels.');
        });

        registerMenuCommand('Tuna: Send test payload', () => {
            post({
                source: 'userscript_test',
                title: 'Tuna test payload',
                artists: ['Browser bridge'],
                album: 'Diagnostics',
                status: 'playing',
                progress: 15000,
                duration: 60000,
                cover: '',
                next_track: {
                    title: 'Second test track',
                    artist: 'Queue preview',
                    cover: ''
                }
            });
            window.alert(`Test payload sent to ${buildUrl()}`);
        });
    }

    function tick() {
        const host = window.location.hostname;

        if (host.includes('music.yandex')) {
            handleYandex();
            return;
        }
        if (host === 'open.spotify.com') {
            handleSpotify();
            return;
        }
        if (host === 'music.youtube.com') {
            handleYouTubeMusic();
            return;
        }
        if (host.includes('youtube.com')) {
            handleYouTube();
            return;
        }
        if (host === 'soundcloud.com') {
            handleSoundCloud();
            return;
        }
        if (host === 'www.deezer.com') {
            handleDeezer();
        }
    }

    async function main() {
        await loadConfig();
        if (config.debugEnabled) {
            ensureDebugBox();
        }

        installMenu();
        window.setInterval(tick, config.refreshRateMs);
        log(`Universal bridge loaded for ${window.location.hostname}.`);
    }

    main().catch(error => {
        warn('Initialization failed.', error);
    });
})();
