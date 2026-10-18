using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Web.Script.Serialization;
using StreamerOs.Shared.Providers;
class ProviderTests
{
 static void Check(bool ok,string message) { if(!ok) throw new Exception(message); }
 static string Ctl(string path) { using(var c=new HttpClient()) return c.GetStringAsync("http://127.0.0.1:18409/"+path).GetAwaiter().GetResult(); }
 static int Count(string key) { return Convert.ToInt32(new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(Ctl("state"))[key]); }
 static void Main() {
  Ctl("scenario/ok");var image=FalImageClient.Generate("fal-key","fal-ai/flux/schnell","test",Console.WriteLine);
  Check(Encoding.UTF8.GetString(image)=="image-fixture","image download");Check(Count("submit")==1&&Count("cdn")==1,"fal submit count");
  foreach(var scenario in new[]{"error","http-error"}){
   Ctl("scenario/"+scenario);bool failed=false;
   try{FalImageClient.Generate("fal-key","fal-ai/flux/schnell","test",Console.WriteLine);}catch(Exception){failed=true;}
   Check(failed&&Count("submit")==1,"fal failure retried or ignored");
  }
  var globals=new Dictionary<string,string>{{"TtsProviderPrimary","fish"},{"TtsProviderSecondary","elevenlabs"},{"TtsProviderTertiary","none"},{"FishAudioApiKey","fish-key"},{"FishAudioModel","fail"},{"ElevenLabsKey","eleven-key"},{"CurrentPersona","Skuf"},{"Persona_Skuf_Voice","persona-eleven"},{"Persona_Skuf_FishRefId","persona-fish"}};
  Func<string,string> get=k=>globals.ContainsKey(k)?globals[k]:null;
  var audio=SpeechClient.Synthesize("test",get,Console.WriteLine);
  Check(audio!=null&&audio.Provider=="elevenlabs"&&audio.Extension=="mp3","fish -> eleven route");
  globals["FishAudioModel"]="s2.1-pro";audio=SpeechClient.Synthesize("test",get,Console.WriteLine);
  Check(audio.Provider=="fish"&&Count("eleven")==1,"fish voice or unexpected fallback");
  globals["TtsProviderPrimary"]="inworld";globals["TtsProviderSecondary"]="none";globals["InworldTTSAuth"]="Basic sample";globals["InworldSpeakingRate"]="1.25";globals["InworldVoiceId"]="Ashley";globals["InworldAudioEncoding"]="MP3";
  audio=SpeechClient.Synthesize("test",get,Console.WriteLine);Check(audio.Extension=="mp3"&&Encoding.UTF8.GetString(audio.Data)=="audio","inworld decode/format");
  int before=Count("inworld");Check(SpeechClient.Synthesize(new string('x',2001),get,Console.WriteLine)==null&&Count("inworld")==before,"inworld length");
  Console.WriteLine("Provider tests passed (local HTTP only).");
 }
}
