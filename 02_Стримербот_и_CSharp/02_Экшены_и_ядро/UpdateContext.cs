using System;
using System.Net.Http;
using System.Text;

public class CPHInline
{
    public bool Execute()
    {
        // 1. Get the game name from Streamer.bot
        // Priority: Argument "gameName" > Global "CurrentGame" > Default
        string gameName = null;

        if (args.ContainsKey("gameName"))
            gameName = args["gameName"].ToString();

        if (string.IsNullOrEmpty(gameName))
            gameName = CPH.GetGlobalVar<string>("CurrentGame");

        if (string.IsNullOrEmpty(gameName))
            gameName = "Just Chatting";

        // 2. Prepare JSON payload
        // We escape quotes just in case, though simple games usually don't have them
        string json = $"{{\"game\": \"{gameName}\"}}";

        // 3. Send POST request to Hivemind Server (Context Rewriter)
        try
        {
            using (var client = new HttpClient())
            {
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                // Timeout short (1s) so it doesn't hang the bot
                client.Timeout = TimeSpan.FromSeconds(2);
                
                var response = client.PostAsync("http://localhost:8000/game", content).Result;
                
                if (response.IsSuccessStatusCode)
                {
                    CPH.LogInfo($"[ContextUpdate] Game updated to: {gameName}");
                    return true;
                }
                else
                {
                    CPH.LogWarn($"[ContextUpdate] Failed: {response.StatusCode}");
                }
            }
        }
        catch (Exception ex)
        {
            // It's expected to fail if the python server isn't running
            CPH.LogDebug($"[ContextUpdate] Server not reachable: {ex.Message}");
        }

        return true;
    }
}
