using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
public sealed class ModCPH {
 public Dictionary<string,string> Values=new Dictionary<string,string>{{"ModBlacklist","blocked"},{"ModTimeoutSeconds","60"}};
 public string TimedOut, Banned;
 public T GetGlobalVar<T>(string name,bool persist=true) {return Values.ContainsKey(name)?(T)Convert.ChangeType(Values[name],typeof(T)):default(T);}
 public void SetGlobalVar(string name,string value,bool persist=true){Values[name]=value;}
 public void LogInfo(string m){} public void LogWarn(string m){} public void LogError(string m){} public void LogDebug(string m){}
 public void SendMessage(string m){} public void WebsocketBroadcastJson(string m){} public double PlaySound(string p,float v,bool wait){return 0;}
 public bool TwitchTimeoutUser(string name,int duration,string reason){TimedOut=name;return true;}
 public bool TwitchBanUser(string name,string reason){Banned=name;return true;}
}
public partial class CPHInline {public ModCPH CPH=new ModCPH();public Dictionary<string,object> args=new Dictionary<string,object>();}
class ModeratorTests {
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 static void Main(){
  var a=new CPHInline();a.args["userName"]="viewer";a.args["userId"]="12345";a.args["message"]="blocked";
  a.args["action"]="log";a.Execute();
  foreach(var item in a.CPH.Values)Check(!item.Key.ToLowerInvariant().Contains("strike"),"logger added a strike");
  a.args["action"]="analyze";a.Execute();Check(a.CPH.TimedOut==null,"first strike timed out");
  a.Execute();Check(a.CPH.TimedOut=="viewer","timeout must use username");a.Execute();Check(a.CPH.Banned=="viewer","ban must use username");
  Console.WriteLine("Moderator tests passed (no Twitch requests).");
 }
}
