using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
public class FakeCPH {
 public Dictionary<string,string> Values=new Dictionary<string,string>();
 public void SetGlobalVar(string name,string value,bool persist) { Values[name]=value; }
 public void LogInfo(string value) {} public void LogWarn(string value) {}
}
public partial class CPHInline { public FakeCPH CPH=new FakeCPH(); public Dictionary<string,object> args=new Dictionary<string,object>(); }
class SettingsTests {
 static void Check(bool ok,string text) { if(!ok)throw new Exception(text); }
 static void Main() {
  var action=new CPHInline();action.args["settingsRequestId"]=Guid.NewGuid().ToString();
  action.args["settingsJson"]="{\"FishAudioApiKey\":\"sample\",\"ChatBotMaxChars\":\"25\"}";
  Check(!action.Execute()&&!action.CPH.Values.ContainsKey("FishAudioApiKey"),"partial invalid save");
  action.args["settingsJson"]="{\"AnyGlobal\":\"bad\"}";Check(!action.Execute(),"unknown global allowed");
  action.args["settingsJson"]="{\"TtsProviderPrimary\":\"elevenlabs\",\"ChatBotMaxChars\":\"400\"}";
  Check(action.Execute()&&action.CPH.Values["TtsProviderPrimary"]=="elevenlabs","valid save");
  Check((bool)JObject.Parse(action.CPH.Values["StreamerOsSettingsResult"])["ok"],"ack");
  Console.WriteLine("Settings tests passed.");
 }
}
