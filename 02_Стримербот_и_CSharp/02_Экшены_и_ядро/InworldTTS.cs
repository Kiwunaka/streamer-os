using System;
using System.IO;
using System.Net;
using StreamerOs.Shared.Providers;

// Unified TTS. Add StreamerOs.Shared.dll in Execute C# Code > References.
public class CPHInline
{
    public bool Execute()
    {
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        string text = GetArg("rawInput") ?? GetArg("userInput") ?? GetArg("message") ?? GetArg("arg0");
        var result = SpeechClient.Synthesize(text, key => CPH.GetGlobalVar<string>(key, true), message => CPH.LogWarn(message));
        if (result == null) return false;
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "StreamerBot_AI_Cache", "tts");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, Guid.NewGuid().ToString("N") + "." + result.Extension);
        File.WriteAllBytes(path, result.Data);
        try
        {
            CPH.PlaySound(path, 1f, true);
            CPH.LogInfo("[TTS] Played via " + result.Provider);
            return true;
        }
        finally
        {
            try { File.Delete(path); }
            catch (IOException) { CPH.LogDebug("[TTS] File still in use: " + path); }
        }
    }
    private string GetArg(string name) { return args.ContainsKey(name) ? args[name]?.ToString() : null; }
}
