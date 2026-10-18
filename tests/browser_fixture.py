"""Browser fixture for authenticated Streamer.bot requests; all credentials are samples."""
import asyncio, base64, hashlib, json
import websockets
variables={'OpenRouterKey':{'value':'sample-openrouter-key'},'ChatBotMaxChars':{'value':'500'},'TtsProviderPrimary':{'value':'inworld'}}
result={}
def digest(value):return base64.b64encode(hashlib.sha256(value.encode()).digest()).decode()
async def connection(ws,path):
    authenticated=False
    await ws.send(json.dumps({'request':'Hello', **({} if path=='/noauth' else {'authentication':{'salt':'fixture-salt','challenge':'fixture-challenge'}})}))
    async for raw in ws:
        request=json.loads(raw);kind=request['request'];response={'id':request['id'],'status':'ok'}
        if kind=='Authenticate':
            authenticated=request['authentication']==digest(digest('test-passwordfixture-salt')+'fixture-challenge')
            if not authenticated:response.update(status='error',error='Wrong password')
        elif not authenticated:response.update(status='error',error='Not authenticated')
        elif kind=='GetGlobals':response['variables']=variables
        elif kind=='DoAction':
            if request['action']['name']=='Streamer OS | Settings':
                changes=json.loads(request['args']['settingsJson'])
                variables.update({k:{'value':v} for k,v in changes.items()})
                result.update(id=request['args']['settingsRequestId'],ok=True)
        elif kind=='GetGlobal':response['variable']={'value':json.dumps(result)}
        else:response.update(status='error',error='Unknown request')
        await ws.send(json.dumps(response))
async def main():
    async with websockets.serve(connection,'127.0.0.1',18080):
        print('Streamer.bot fixture 18080',flush=True);await asyncio.Future()
asyncio.run(main())
