"""Local API fixture. Uses sample keys; never calls external services."""
import json
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

state = {'scenario': 'ok', 'submit': 0, 'fish': 0, 'eleven': 0, 'inworld': 0, 'cdn': 0}
class Handler(BaseHTTPRequestHandler):
    def log_message(self, *args): pass
    def send(self, value, code=200):
        data = value if isinstance(value, bytes) else json.dumps(value).encode()
        self.send_response(code); self.send_header('Content-Length', str(len(data))); self.end_headers(); self.wfile.write(data)
    def do_GET(self):
        if self.path.startswith('/scenario/'):
            state.update(scenario=self.path.split('/')[-1], submit=0, cdn=0); self.send({}); return
        if self.path == '/state': self.send(state); return
        if self.path == '/status':
            self.send({'status':'COMPLETED', **({'error':'fixture generation error'} if state['scenario']=='error' else {})}); return
        if self.path == '/result': self.send({'images':[{'url':'http://127.0.0.1:18409/cdn'}]}); return
        if self.path == '/cdn':
            state['cdn'] += 1
            if self.headers.get('Authorization'): self.send({'error':'credential leaked'},400)
            else: self.send(b'image-fixture')
            return
        self.send({},404)
    def do_PUT(self): self.send({},202)
    def do_POST(self):
        body=json.loads(self.rfile.read(int(self.headers['Content-Length'])))
        if self.path.startswith('/fal-ai/'):
            state['submit'] += 1
            if self.headers.get('Authorization') != 'Key fal-key': self.send({},401); return
            if state['scenario']=='http-error': self.send({},500); return
            root='http://127.0.0.1:18409/'
            self.send({'request_id':'fixture-job','status_url':root+'status','response_url':root+'result','cancel_url':root+'cancel'}); return
        if self.path == '/fish':
            state['fish'] += 1
            if self.headers.get('model') == 'fail': self.send({},401); return
            assert self.headers.get('Authorization') == 'Bearer fish-key'
            assert body['reference_id']=='persona-fish'
            self.send(b'fish-audio'); return
        if self.path.startswith('/eleven/'):
            state['eleven'] += 1
            assert self.headers.get('xi-api-key') == 'eleven-key'
            assert body['model_id']=='eleven_multilingual_v2'
            assert self.path.startswith('/eleven/persona-eleven?')
            self.send(b'eleven-audio'); return
        if self.path == '/inworld':
            state['inworld'] += 1
            assert self.headers.get('Authorization') == 'Basic sample'
            assert body['audioConfig']['speakingRate'] == 1.25
            assert body['voiceId'] == 'Ashley'
            self.send({'audioContent':'YXVkaW8='}); return
        self.send({},404)
ThreadingHTTPServer(('127.0.0.1',18409), Handler).serve_forever()
