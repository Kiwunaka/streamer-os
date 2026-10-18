using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace StreamerOs.Shared.Providers
{
    // Concrete FLUX text-to-image queue client. Submit once; polling never resubmits a job.
    public static class FalImageClient
    {
        private const string QueueRoot = "https://queue.fal.run/";

        public static byte[] Generate(string key, string model, string prompt, Action<string> log)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("FalApiKey is empty.");
            if (model != "fal-ai/flux/schnell" && model != "fal-ai/flux/dev")
                throw new ArgumentException("Supported fal models: fal-ai/flux/schnell, fal-ai/flux/dev.");
            var json = new JavaScriptSerializer();
            string cancelUrl = null;
            bool completed = false;
            using (var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(90)))
            using (var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }))
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "Key " + key);
                client.Timeout = TimeSpan.FromSeconds(95);
                try
                {
                    var body = new { prompt = prompt, num_images = 1, image_size = "square_hd", output_format = "jpeg", enable_safety_checker = true };
                    using (var response = client.PostAsync(QueueRoot + model,
                        new StringContent(json.Serialize(body), Encoding.UTF8, "application/json"), deadline.Token).GetAwaiter().GetResult())
                    {
                        response.EnsureSuccessStatusCode();
                        var job = json.Deserialize<Dictionary<string, object>>(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
                        string id = (string)job["request_id"];
                        log("[fal] request_id=" + id);
                        string statusUrl = QueueUrl((string)job["status_url"]);
                        string resultUrl = QueueUrl((string)job["response_url"]);
                        cancelUrl = QueueUrl((string)job["cancel_url"]);
                        while (true)
                        {
                            var state = ReadJson(client, statusUrl, json, deadline.Token);
                            string status = (string)state["status"];
                            if (status == "COMPLETED")
                            {
                                completed = true;
                                if (state.ContainsKey("error")) throw new InvalidOperationException("fal generation failed: " + state["error"]);
                                break;
                            }
                            if (status != "IN_QUEUE" && status != "IN_PROGRESS")
                                throw new InvalidOperationException("Unknown fal queue status: " + status);
                            System.Threading.Tasks.Task.Delay(1000, deadline.Token).GetAwaiter().GetResult();
                        }
                        var result = ReadJson(client, resultUrl, json, deadline.Token);
                        var images = (IList)result["images"];
                        if (images.Count == 0) throw new InvalidOperationException("fal returned no images.");
                        string url = (string)((Dictionary<string, object>)images[0])["url"];
                        if (new Uri(url).Scheme != "https") throw new InvalidOperationException("fal image URL must use HTTPS.");
                        // Media URLs belong to a CDN. Never forward the API key there.
                        using (var media = new HttpClient())
                        using (var image = media.GetAsync(url, deadline.Token).GetAwaiter().GetResult())
                        {
                            image.EnsureSuccessStatusCode();
                            return image.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                        }
                    }
                }
                finally
                {
                    if (!completed && cancelUrl != null)
                    {
                        try
                        {
                            using (var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
                            using (var response = client.PutAsync(cancelUrl, null, cancel.Token).GetAwaiter().GetResult())
                                log("[fal] cancel: HTTP " + (int)response.StatusCode + ". A running job may still be billed.");
                        }
                        catch (Exception) { log("[fal] Could not cancel; check request_id in the fal dashboard before retrying."); }
                    }
                }
            }
        }

        private static string QueueUrl(string url)
        {
            var root = new Uri(QueueRoot);
            var uri = new Uri(url);
            if (uri.Scheme != root.Scheme || uri.Host != root.Host || uri.Port != root.Port || !string.IsNullOrEmpty(uri.UserInfo))
                throw new InvalidOperationException("Unexpected fal queue URL.");
            return uri.AbsoluteUri;
        }

        private static Dictionary<string, object> ReadJson(HttpClient client, string url, JavaScriptSerializer json, CancellationToken token)
        {
            using (var response = client.GetAsync(url, token).GetAwaiter().GetResult())
            {
                response.EnsureSuccessStatusCode();
                return json.Deserialize<Dictionary<string, object>>(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            }
        }
    }
}
