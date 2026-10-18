"""
Audio Visualizer Server - Unified Edition
Combines:
- WASAPI Loopback FFT (WebSocket port 8765)
- Track Info API (HTTP port 1608 - Tuna compatible)

No external Tuna plugin needed!
"""

import argparse
import asyncio
import json
import numpy as np
import websockets
import pyaudiowpatch as pyaudio
from collections import deque
from aiohttp import web
import aiohttp_cors

# ========================================
# CONFIGURATION
# ========================================
WEBSOCKET_PORT = 8765
HTTP_PORT = 1608  # Tuna-compatible port
BLOCK_SIZE = 2048
NUM_BANDS = 128
SMOOTHING = 0.5

DEVICE_NAME = ""
DEVICE_INDEX = None
NO_INPUT = False

def parse_args():
    parser = argparse.ArgumentParser(description="Audio Visualizer Server")
    parser.add_argument("--bands", type=int, default=NUM_BANDS)
    parser.add_argument("--smoothing", type=float, default=SMOOTHING)
    parser.add_argument("--ws-port", type=int, default=WEBSOCKET_PORT)
    parser.add_argument("--http-port", type=int, default=HTTP_PORT)
    parser.add_argument("--device-name", type=str, default="")
    parser.add_argument("--device-index", type=int, default=None)
    parser.add_argument("--no-input", action="store_true", default=False)
    return parser.parse_args()

args = parse_args()
NUM_BANDS = max(8, int(args.bands))
SMOOTHING = min(max(float(args.smoothing), 0.0), 0.95)
WEBSOCKET_PORT = int(args.ws_port)
HTTP_PORT = int(args.http_port)
DEVICE_NAME = args.device_name or ""
DEVICE_INDEX = args.device_index
NO_INPUT = args.no_input

# ========================================
# TRACK INFO STORAGE
# ========================================
current_track = {
    "status": "stopped",
    "title": "",
    "artists": [],
    "album": "",
    "cover": "",
    "cover_url": "",
    "progress": 0,
    "duration": 0,
    "progress_percent": 0,
    "source": ""
}

# ========================================
# AUDIO PROCESSING
# ========================================
class AudioAnalyzer:
    def __init__(self):
        self.fft_data = np.zeros(NUM_BANDS)
        self.smoothed = np.zeros(NUM_BANDS)
        self.buffer = deque(maxlen=BLOCK_SIZE)
        self.sample_rate = 44100
        self.max_peak = 0.01
        self.smoothing = SMOOTHING

    def process_audio(self, audio_data):
        audio = np.frombuffer(audio_data, dtype=np.float32)
        if len(audio) > 1:
            audio = audio.reshape(-1, 2).mean(axis=1)
        self.buffer.extend(audio)
        if len(self.buffer) >= BLOCK_SIZE:
            audio_data = np.array(list(self.buffer)[-BLOCK_SIZE:])
            self.analyze(audio_data)

    def analyze(self, audio_data):
        windowed = audio_data * np.blackman(len(audio_data))
        fft = np.abs(np.fft.rfft(windowed))
        
        bands = []
        log_starts = np.logspace(np.log10(20), np.log10(16000), NUM_BANDS + 1)
        freq_per_bin = self.sample_rate / BLOCK_SIZE
        
        for i in range(NUM_BANDS):
            start_freq = log_starts[i]
            end_freq = log_starts[i + 1]
            low_bin = int(start_freq / freq_per_bin)
            high_bin = int(end_freq / freq_per_bin)
            high_bin = max(high_bin, low_bin + 1)
            high_bin = min(high_bin, len(fft))
            
            if low_bin < len(fft):
                band_value = np.max(fft[low_bin:high_bin]) if high_bin > low_bin else fft[low_bin]
            else:
                band_value = 0
            bands.append(band_value)
        
        self.fft_data = np.array(bands)
        self.smoothed = self.smoothed * self.smoothing + self.fft_data * (1 - self.smoothing)

    def get_bands(self):
        eq_curve = np.logspace(0, 0.5, NUM_BANDS)
        boosted = self.smoothed * eq_curve
        
        current_peak = np.max(boosted)
        if current_peak > self.max_peak:
            self.max_peak = current_peak
        else:
            self.max_peak *= 0.995
        
        if self.max_peak < 0.01:
            self.max_peak = 0.01
        
        normalized = boosted / self.max_peak
        normalized = np.power(normalized, 1.2)
        normalized = np.clip(normalized, 0, 1)
        return normalized.tolist()

# ========================================
# HTTP API (Tuna-compatible)
# ========================================
async def handle_get_track(request):
    """GET / - Return current track info (Tuna API compatible)"""
    return web.json_response(current_track)

async def handle_post_track(request):
    """POST / - Receive track info from userscript (Tuna format)"""
    global current_track
    try:
        raw = await request.json()
        
        # Tuna userscript wraps data in {data: {...}, hostname: ...}
        data = raw.get("data", raw)
        
        # Update track info
        current_track["status"] = data.get("status", "playing")
        current_track["title"] = data.get("title", "")
        current_track["artists"] = data.get("artists", [])
        current_track["album"] = data.get("album", "")
        current_track["cover"] = data.get("cover", data.get("cover_url", ""))
        current_track["cover_url"] = data.get("cover_url", data.get("cover", ""))
        current_track["progress"] = data.get("progress", 0)
        current_track["duration"] = data.get("duration", 0)
        current_track["source"] = data.get("source", "")
        current_track["next_track"] = data.get("next_track", None)
        
        # Calculate progress percent
        if current_track["duration"] > 0:
            current_track["progress_percent"] = (current_track["progress"] / current_track["duration"]) * 100
        else:
            current_track["progress_percent"] = 0
        
        print(f"[Track] {current_track['status']}: {current_track['title']} ({current_track['source']})")
        return web.json_response({"status": "ok"})
    except Exception as e:
        print(f"[Error] POST failed: {e}")
        return web.json_response({"status": "error", "message": str(e)}, status=400)

async def handle_options(request):
    """Handle CORS preflight"""
    return web.Response(status=200)

# ========================================
# WEBSOCKET SERVER
# ========================================
analyzer = AudioAnalyzer()
connected_clients = set()

async def websocket_handler(websocket):
    connected_clients.add(websocket)
    print(f"Client connected. Total: {len(connected_clients)}")
    
    try:
        async for message in websocket:
            pass
    except websockets.exceptions.ConnectionClosed:
        pass
    finally:
        connected_clients.discard(websocket)
        print(f"Client disconnected. Total: {len(connected_clients)}")

async def broadcast_fft():
    while True:
        if connected_clients:
            bands = analyzer.get_bands()
            message = json.dumps({
                "type": "fft",
                "bands": bands,
                "peak": max(bands) if bands else 0
            })
            
            await asyncio.gather(
                *[client.send(message) for client in connected_clients],
                return_exceptions=True
            )
        
        await asyncio.sleep(1 / 60)

# ========================================
# AUDIO SETUP
# ========================================
def list_loopback_devices(p):
    print("\n=== Available Loopback Devices (Outputs) ===")
    devices = []
    
    try:
        wasapi_info = p.get_host_api_info_by_type(pyaudio.paWASAPI)
    except:
        print("WASAPI not available!")
        return devices
    
    for i in range(p.get_device_count()):
        dev = p.get_device_info_by_index(i)
        if dev['hostApi'] == wasapi_info['index'] and dev['maxOutputChannels'] > 0:
            devices.append((i, dev))
            print(f"  [{i}] {dev['name']} (out: {dev['maxOutputChannels']}, rate: {int(dev['defaultSampleRate'])})")
    
    print()
    return devices

def select_device(p, devices):
    if DEVICE_INDEX is not None:
        for idx, dev in devices:
            if idx == DEVICE_INDEX:
                print(f"[OK] Selected by index: [{idx}] {dev['name']}")
                return idx, dev
        print("[WARN] Device index not found.")

    if DEVICE_NAME:
        name_lower = DEVICE_NAME.lower()
        for idx, dev in devices:
            if name_lower in dev['name'].lower():
                print(f"[OK] Selected by name: [{idx}] {dev['name']}")
                return idx, dev
        print("[WARN] Device name not found.")

    for idx, dev in devices:
        if 'sampler' in dev['name'].lower():
            print(f"[OK] Auto-selected: [{idx}] {dev['name']}")
            return idx, dev

    if NO_INPUT:
        if devices:
            idx, dev = devices[0]
            print(f"[WARN] Falling back to first device: [{idx}] {dev['name']}")
            return idx, dev
        return None, None

    print("Enter device number from list above:")
    try:
        user_input = input("> ").strip()
        if user_input:
            device_id = int(user_input)
            device_info = p.get_device_info_by_index(device_id)
            return device_id, device_info
    except Exception:
        print("Invalid input!")
    return None, None

def audio_callback(in_data, frame_count, time_info, status):
    analyzer.process_audio(in_data)
    return (None, pyaudio.paContinue)

# ========================================
# MAIN
# ========================================
async def main():
    print("=" * 50)
    print("Audio Visualizer Server - Unified Edition")
    print("FFT: ws://localhost:8765 | Track: http://localhost:1608")
    print("=" * 50)
    
    # Setup audio
    p = pyaudio.PyAudio()
    devices = list_loopback_devices(p)
    
    if not devices:
        print("No loopback devices found!")
        return
    
    device_id, device_info = select_device(p, devices)
    if device_info is None:
        print("No device selected!")
        return
    
    # Start loopback stream
    try:
        loopback = p.get_wasapi_loopback_analogue_by_index(device_id)
        sample_rate = int(loopback['defaultSampleRate'])
        channels = loopback['maxInputChannels']
        
        analyzer.sample_rate = sample_rate
        
        print(f"Loopback: {loopback['name']}")
        print(f"Sample rate: {sample_rate} Hz, Channels: {channels}")
        
        stream = p.open(
            format=pyaudio.paFloat32,
            channels=channels,
            rate=sample_rate,
            input=True,
            input_device_index=loopback['index'],
            frames_per_buffer=512,
            stream_callback=audio_callback
        )
        stream.start_stream()
        print("[OK] Loopback audio stream started!")
        
    except Exception as e:
        print(f"[ERROR] Failed to start loopback: {e}")
        p.terminate()
        return
    
    # Setup HTTP server (Tuna-compatible API)
    app = web.Application()
    
    # Setup CORS
    cors = aiohttp_cors.setup(app, defaults={
        "*": aiohttp_cors.ResourceOptions(
            allow_credentials=True,
            expose_headers="*",
            allow_headers="*",
            allow_methods=["GET", "POST", "OPTIONS"]
        )
    })
    
    # Add routes (OPTIONS handled by aiohttp_cors automatically)
    resource = cors.add(app.router.add_resource("/"))
    cors.add(resource.add_route("GET", handle_get_track))
    cors.add(resource.add_route("POST", handle_post_track))
    
    runner = web.AppRunner(app)
    await runner.setup()
    site = web.TCPSite(runner, "localhost", HTTP_PORT)
    await site.start()
    print(f"[OK] HTTP Track API running on http://localhost:{HTTP_PORT}")
    
    # Start WebSocket server
    server = await websockets.serve(websocket_handler, "localhost", WEBSOCKET_PORT)
    print(f"[OK] WebSocket FFT server running on ws://localhost:{WEBSOCKET_PORT}")
    print("\nReady! Connect overlay and run userscript in browser.")
    print("Press Ctrl+C to stop.\n")
    
    # Start broadcasting
    try:
        await broadcast_fft()
    except asyncio.CancelledError:
        pass
    finally:
        stream.stop_stream()
        stream.close()
        p.terminate()
        server.close()
        await runner.cleanup()

if __name__ == "__main__":
    try:
        asyncio.run(main())
    except KeyboardInterrupt:
        print("\n\nServer stopped.")
