'use strict';
const llm = ['','openrouter','fireworks'];
const tts = ['inworld','fish','elevenlabs','ntts','none'];
// [global, label, type/options, default]. Only these fields can be saved by the settings action.
const groups = [
 ['providers','Ключи сервисов','Нужны ключи только тех сервисов, которые ты выбрал. Пустое поле удаляет сохранённый ключ.',[
  ['OpenRouterKey','OpenRouter API key','password',''],['FireworksKey','Fireworks API key','password',''],['FalApiKey','fal.ai API key','password',''],['InworldTTSAuth','Inworld · полный заголовок Basic …','password',''],['FishAudioApiKey','Fish Audio API key','password',''],['ElevenLabsKey','ElevenLabs API key','password',''],['NttsApiKey','NTTS API key','password','']
 ]],
 ['text','Текст и промпты','Провайдер модуля «По умолчанию» использует общий выбор. Model ID должен принадлежать выбранному провайдеру.',[
  ['LlmProvider','Общий провайдер',['openrouter','fireworks'],'openrouter'],['OpenRouterDefaultTextModel','OpenRouter · общая модель','text','google/gemini-2.5-flash-lite'],['FireworksDefaultTextModel','Fireworks · общая модель','text','accounts/fireworks/models/kimi-k2-instruct-0905'],['ChatBotProvider','Провайдер чата',llm,''],['ChatBotModel','Модель чата','text','google/gemini-2.5-flash-lite'],['ChatBotEnabled','Ответы бота включены','checkbox','true'],['ChatBotAutoEnabled','Автоматические ответы чату','checkbox','false'],['ChatBotTtsEnabled','Озвучивать ответы чата','checkbox','true'],['ChatBotMaxChars','Максимум символов ответа','number','500'],['ChatBotMinIntervalSec','Интервал автоответов, секунд','number','20'],['ChatBotTemperature','Температура','number','0.7'],['ChatBotMaxTokens','Лимит токенов ответа','number','140'],['ChatBotSystemPrompt','Промпт чата · пусто = встроенный','textarea',''],['ModProvider','Провайдер модерации',llm,''],['ModModel','Модель модерации','text','google/gemini-2.5-flash'],['ModLlmMessageScan','Проверять сообщения через LLM','checkbox','false'],['ModBlacklist','Стоп-слова через запятую','text',''],['ModResponsePrompt','Промпт ответа модератора · пусто = встроенный','textarea',''],['OracleProvider','Провайдер оракула',llm,''],['OracleModel','Модель оракула','text','google/gemini-2.5-flash-lite'],['DonationProvider','Провайдер текста донатов',llm,''],['DonationModel','Модель текста донатов','text','google/gemini-2.5-flash-lite'],['StreamRecapProvider','Провайдер итогов стрима',llm,''],['StreamRecapModel','Модель итогов','text','google/gemini-2.5-flash-lite']
 ]],
 ['speech','Озвучка','Маршрут общий для чата, реакций и донатов. При ошибке сервис сменяется на следующий; none отключает позицию. Не используемые сервисы не вызываются.',[
  ['TtsProviderPrimary','Основной TTS',tts,'inworld'],['TtsProviderSecondary','Резервный TTS',tts,'ntts'],['TtsProviderTertiary','Третий TTS',tts,'fish'],['InworldModelId','Inworld · модель','text','inworld-tts-2'],['InworldVoiceId','Inworld · voice ID','text','Ashley'],['InworldAudioEncoding','Inworld · формат',['LINEAR16','WAV','MP3','OGG_OPUS','FLAC'],'LINEAR16'],['InworldSampleRateHertz','Inworld · частота, Гц','number','24000'],['InworldSpeakingRate','Inworld · скорость 0.5–1.5','number','1.0'],['FishAudioModel','Fish · модель',['s2.1-pro','s2.1-pro-free','s2-pro','s1','drama-3'],'s2.1-pro'],['FishAudioVoiceRefId','Fish · ID голоса / модели (reference_id)','text',''],['FishAudioFormat','Fish · формат',['mp3','wav','opus'],'mp3'],['FishAudioLatency','Fish · latency',['normal','balanced'],'normal'],['ElevenLabsModel','ElevenLabs · model ID','text','eleven_multilingual_v2'],['ElevenLabsVoiceId','ElevenLabs · voice ID','text',''],['NttsVoiceList','NTTS · голоса через запятую (пусто = список API)','text',''],['NttsExt','NTTS · формат',['wav','mp3'],'wav'],['NttsEffect','NTTS · эффект (необязательно)','text','']
 ]],
 ['images','Изображения','fal подключён для FLUX Schnell/Dev. Ключ создаётся на fal.ai/dashboard/keys. Генерация и уточнение промпта оплачиваются отдельно.',[
  ['ImageGenerationProvider','Картинка за баллы канала',['fal','fireworks'],'fireworks'],['FalImageModel','fal · FLUX',['fal-ai/flux/schnell','fal-ai/flux/dev'],'fal-ai/flux/schnell'],['ImageGenerationModel','Fireworks · модель изображения','text','accounts/fireworks/models/flux-kontext-max'],['ImageRefineProvider','Уточнение промпта',['openrouter','fireworks'],'openrouter'],['ImageRefineModel','Модель уточнения промпта','text','google/gemini-2.5-flash'],['ImageRefinePrompt','Промпт уточнения · пусто = встроенный','textarea',''],['ImageReactionEnabled','Реакция персонажа на картинку','checkbox','true'],['ImageReactionProvider','Провайдер реакции',['openrouter','fireworks'],'fireworks'],['ImageReactionOpenRouterModel','OpenRouter · модель с поддержкой зрения','text','google/gemini-2.5-flash'],['ImageReactionFireworksModel','Fireworks · модель с поддержкой зрения','text','accounts/fireworks/models/kimi-k2p5'],['ImageShareToChat','Отправлять изображение на uguu.se и публиковать ссылку в чат','checkbox','false'],['DonationImageProvider','Картинка доната',['fal','openrouter','none'],'openrouter'],['DonationImageModel','OpenRouter · модель изображения доната','text','google/gemini-2.5-flash-image']
 ]],
 ['personas','Персоны','Пустое поле использует встроенный характер или общий голос. Voice ID ElevenLabs должен быть доступен твоему аккаунту.',[]],
 ['memes','MemeAlerts','Сначала импортируй memealerts.cs из streamer.bot-donationAlerts. Приложение и ключи выдаёт поддержка MemeAlerts; здесь задаются его параметры, затем запускается Connect.',[
  ['maClientId','MemeAlerts · clientId','text',''],['maClientSecret','MemeAlerts · clientSecret','password','']
 ]]
];
for (const persona of ['Skuf','Toxic','UwU','Mistress']) {
 groups.find(g=>g[0]==='personas')[3].push(
  [`Persona_${persona}_System`,`${persona} · характер`,'textarea',''],
  [`Persona_${persona}_Style`,`${persona} · стиль изображения`,'textarea',''],
  [`Persona_${persona}_Voice`,`${persona} · ElevenLabs voice ID`,'text',''],
  [`Persona_${persona}_FishRefId`,`${persona} · Fish reference_id`,'text',''],
  [`Persona_${persona}_InworldVoiceId`,`${persona} · Inworld voice ID`,'text','']);
}
const $ = id => document.getElementById(id);
const definitions = new Map();
const initial = new Map();
let ws, authenticated = false, loaded = false, saving = false;
const pending = new Map();
function notice(message,error=false){$('notice').textContent=message;$('notice').classList.toggle('error',error);}
for(const [id,title,description,fields] of groups){
 const section=document.createElement('section');section.id=id;
 const h=document.createElement('h2');h.textContent=title;section.append(h);
 const p=document.createElement('p');p.textContent=description;section.append(p);
 const grid=document.createElement('fieldset');grid.className='fields';grid.disabled=true;grid.style.border='0';grid.style.padding='0';grid.style.margin='0';
 for(const def of fields){
  const [key,label,type,value]=def; definitions.set(key,def);
  const el=document.createElement(Array.isArray(type)?'select':type==='textarea'?'textarea':'input');el.id=key;el.name=key;
  if(Array.isArray(type))for(const option of type){const node=document.createElement('option');node.value=option;node.textContent=option||'По умолчанию';el.append(node);}
  else if(type!=='textarea'){el.type=type;if(type==='number')el.step='any';}
  el.autocomplete='off';el.maxLength=12000;
  const wrapper=document.createElement('label');wrapper.htmlFor=key;
  const text=document.createElement('span');text.textContent=label;
  if(type==='checkbox'){wrapper.className='check wide';wrapper.append(el,text);}else{wrapper.append(text,el);if(type==='textarea')wrapper.className='wide';}
  const hint=document.createElement('small');hint.textContent=key;wrapper.append(hint);grid.append(wrapper);
 }
 section.append(grid);$('settings-groups').append(section);
}
function value(key){const el=$(key);return el.type==='checkbox'?String(el.checked):el.value.trim();}
function setValue(key,val){const el=$(key);if(el.type==='checkbox')el.checked=String(val).toLowerCase()==='true';else el.value=String(val);}
function updateState(){
 document.querySelectorAll('[data-bot]').forEach(b=>b.disabled=!loaded||saving);
 document.querySelectorAll('fieldset').forEach(f=>f.disabled=!loaded||saving);
 const count=[...definitions.keys()].filter(k=>value(k)!==initial.get(k)).length;
 $('dirty').textContent=loaded?(count?`Изменено полей: ${count}`:'Все изменения сохранены'):'Подключись, чтобы загрузить настройки';
 $('save').disabled=!loaded||saving||!count;
}
$('settings-form').addEventListener('input',updateState);
function request(requestName,parameters={}){
 return new Promise((resolve,reject)=>{
  if(!ws||ws.readyState!==WebSocket.OPEN){reject(new Error('Нет соединения со Streamer.bot.'));return;}
  const id=crypto.randomUUID();const timeout=setTimeout(()=>{pending.delete(id);reject(new Error(`Streamer.bot не ответил на ${requestName}. Проверь журнал и очередь действий.`));},10000);
  pending.set(id,{resolve,reject,timeout});ws.send(JSON.stringify({request:requestName,...parameters,id}));
 });
}
async function hash(value){const bytes=await crypto.subtle.digest('SHA-256',new TextEncoder().encode(value));return btoa(String.fromCharCode(...new Uint8Array(bytes)));}
async function load(){
 const data=await request('GetGlobals',{persisted:true});
 const vars=data.variables||{};
 for(const [key,def] of definitions){setValue(key,vars[key]?.value??def[3]);initial.set(key,value(key));}
 loaded=true;updateState();notice('Настройки загружены из Streamer.bot.');
}
function drop(){
 authenticated=false;loaded=false;saving=false;initial.clear();
 for(const key of definitions.keys())setValue(key,'');
 $('connection').textContent='Не подключено';$('connection').classList.remove('online');$('bot-status').textContent='Нет соединения';updateState();
 for(const p of pending.values()){clearTimeout(p.timeout);p.reject(new Error('Соединение закрыто.'));}pending.clear();
}
$('connect-form').addEventListener('submit',event=>{
 event.preventDefault();if(ws)ws.close();drop();
 const url=new URL($('ws-url').value);
 if(url.protocol!=='ws:'||!['127.0.0.1','localhost','[::1]'].includes(url.hostname)){notice('Используй локальный WebSocket: ws://127.0.0.1:8080/',true);return;}
 const socket=new WebSocket(url);ws=socket;notice('Подключаюсь…');
 const helloTimeout=setTimeout(()=>{if(!authenticated&&ws===socket){notice('Не получен ответ аутентификации. Проверь сервер и пароль.',true);socket.close();}},10000);
 socket.onmessage=async event=>{
  if(ws!==socket)return;
  try{
   const data=JSON.parse(event.data);
   if(data.request==='Hello'){
    if(!data.authentication){notice('Включи Authentication в WebSocket Server Streamer.bot и задай пароль. Без него панель не читает ключи.',true);socket.close();return;}
    const password=$('ws-password').value;
    const secret=await hash(password+data.authentication.salt);
    const authentication=await hash(secret+data.authentication.challenge);
    await request('Authenticate',{authentication});authenticated=true;clearTimeout(helloTimeout);
    $('ws-password').value='';$('connection').textContent='Подключено';$('connection').classList.add('online');$('bot-status').textContent='Соединение с паролем';await load();return;
   }
   const p=pending.get(data.id);if(!p)return;clearTimeout(p.timeout);pending.delete(data.id);
   if(data.status==='ok')p.resolve(data);else p.reject(new Error(data.error||'Streamer.bot отклонил запрос.'));
  }catch(error){notice(error.message,true);}
 };
 socket.onerror=()=>{if(ws===socket)notice('Не удалось подключиться. Запусти WebSocket Server в Streamer.bot.',true);};
 socket.onclose=()=>{clearTimeout(helloTimeout);if(ws===socket)drop();};
});
$('disconnect').onclick=()=>{if(ws)ws.close();drop();notice('Отключено. Поля с ключами очищены.');};
async function doAction(name,args={}){if(!authenticated)throw new Error('Сначала подключись.');await request('DoAction',{action:{name},args});}
$('settings-form').addEventListener('submit',async event=>{
 event.preventDefault();if(!loaded||saving)return;
 const changes=Object.fromEntries([...definitions.keys()].filter(k=>value(k)!==initial.get(k)).map(k=>[k,value(k)]));
 if(!Object.keys(changes).length)return;
 saving=true;updateState();notice('Сохраняю…');
 try{
  const id=crypto.randomUUID();await doAction('Streamer OS | Settings',{settingsRequestId:id,settingsJson:JSON.stringify(changes)});
  let result;
  for(let attempt=0;attempt<30;attempt++){
   await new Promise(r=>setTimeout(r,300));
   try{const response=await request('GetGlobal',{persisted:false,variable:'StreamerOsSettingsResult'});const parsed=JSON.parse(response.variable.value);if(parsed.id===id){result=parsed;break;}}catch(error){if(!authenticated)throw error;}
  }
  if(!result)throw new Error('Нет подтверждения сохранения. Создай действие Streamer OS | Settings и выдели ему отдельную очередь. Проверь журнал перед повтором.');
  if(!result.ok)throw new Error(result.error);
  await load();notice('Изменения сохранены в Streamer.bot.');
 }catch(error){notice(error.message,true);}finally{saving=false;updateState();}
});
$('reload').onclick=()=>load().catch(e=>notice(e.message,true));
async function runtime(path,options){const res=await fetch(path,{...options,signal:AbortSignal.timeout(3000)});if(!res.ok)throw new Error(`OBS runtime: HTTP ${res.status}`);return res.json();}
async function refreshRuntime(){
 try{const state=await runtime('/runtime/state');$('runtime-status').textContent='Работает';$('queue-status').textContent=`В очереди: ${state.pendingCount??0} · ${state.activeEvent?.type||'экран свободен'}`;}
 catch(e){$('runtime-status').textContent='Недоступен';$('queue-status').textContent='Запусти сервер в доке Audio Visualizer';}
 setTimeout(refreshRuntime,3000);
}
$('preview').onclick=async()=>{
 try{await runtime('/events',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({type:'system.toast.v1',version:1,eventId:crypto.randomUUID(),timestamp:new Date().toISOString(),source:'admin.preview',target:'runtime',priority:40,ttlMs:5000,payload:{message:'Streamer OS на связи. Экран готов.',level:'info'},queue:{interruptible:true,replaceByKey:'admin-preview',minDisplayMs:1000}})});notice('Тест отправлен. В OBS должен быть источник Visual Runtime.');}catch(e){notice(e.message,true);}
};
$('clear').onclick=()=>runtime('/runtime/clear',{method:'POST',headers:{'Content-Type':'application/json'},body:'{}'}).then(()=>notice('Очередь очищена.')).catch(e=>notice(e.message,true));
$('health').onclick=()=>doAction('Streamer OS | Check').then(()=>notice('Проверка запущена. Результат — на экране и в журнале Streamer.bot.')).catch(e=>notice(e.message,true));
$('test-tts').onclick=async()=>{
 const button=$('test-tts');button.disabled=true;
 try{await doAction('TTS',{rawInput:$('test-text').value});notice('Текст отправлен в TTS. Проверь звук и журнал Streamer.bot.');}catch(e){notice(e.message,true);}finally{setTimeout(()=>{button.disabled=!loaded;},5000);}
};
window.addEventListener('beforeunload',event=>{if(loaded&&[...definitions.keys()].some(k=>value(k)!==initial.get(k))){event.preventDefault();event.returnValue='';}});
updateState();refreshRuntime();
