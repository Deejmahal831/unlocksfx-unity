using System;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace UnlockSfx
{
    [Serializable]
    public class GenerateRequest
    {
        public string originalPrompt;
        public string category;
        public float durationSeconds;
        public bool loopable;
        public string outputFormat; // "mp3" | "wav"
        public float promptInfluence;
    }

    [Serializable]
    public class GenerateResponse
    {
        public string id;
        public string title;
        public string format;
        public string downloadUrl;
        public int creditsRemaining;
    }

    [Serializable]
    internal class CreditsResponse
    {
        public int creditsRemaining;
    }

    [Serializable]
    internal class ApiError
    {
        public string error;
    }

    /// <summary>
    /// Thin client for the UnlockSFX public API. The plugin never talks to the
    /// audio provider directly — it calls unlocksfx.com/api/v1 with the user's
    /// API key, exactly like the Godot plugin. Server-side handles credits,
    /// rate limits, storage, and licensing.
    /// </summary>
    internal static class UnlockSfxClient
    {
        const string ApiBase = "https://www.unlocksfx.com/api/v1";

        /// <summary>
        /// Generate one sound. On success returns the metadata plus the raw audio
        /// bytes (downloaded from the short-lived signed URL the API hands back).
        /// All callbacks run on the main thread via the request's completed event.
        /// </summary>
        public static void GenerateSound(
            GenerateRequest request,
            Action<GenerateResponse, byte[]> onSuccess,
            Action<string> onError)
        {
            var apiKey = UnlockSfxSettings.ApiKey;
            if (string.IsNullOrEmpty(apiKey))
            {
                onError("Add your API key first.");
                return;
            }

            var json = JsonUtility.ToJson(request);
            var post = new UnityWebRequest(ApiBase + "/generate", "POST");
            post.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            post.downloadHandler = new DownloadHandlerBuffer();
            post.SetRequestHeader("Content-Type", "application/json");
            post.SetRequestHeader("Authorization", "Bearer " + apiKey);

            post.SendWebRequest().completed += _ =>
            {
                if (!IsOk(post))
                {
                    var message = ExtractError(post);
                    post.Dispose();
                    onError(message);
                    return;
                }

                GenerateResponse meta = null;
                try { meta = JsonUtility.FromJson<GenerateResponse>(post.downloadHandler.text); }
                catch { /* handled below */ }
                post.Dispose();

                if (meta == null || string.IsNullOrEmpty(meta.downloadUrl))
                {
                    onError("The server did not return a download link.");
                    return;
                }

                // Second hop: fetch the actual audio from the signed URL.
                var download = UnityWebRequest.Get(meta.downloadUrl);
                download.SendWebRequest().completed += __ =>
                {
                    if (!IsOk(download))
                    {
                        download.Dispose();
                        onError("The sound was generated but could not be downloaded.");
                        return;
                    }

                    var bytes = download.downloadHandler.data;
                    download.Dispose();

                    if (bytes == null || bytes.Length == 0)
                    {
                        onError("The downloaded audio was empty.");
                        return;
                    }

                    onSuccess(meta, bytes);
                };
            };
        }

        /// <summary>Read the account's remaining credit balance.</summary>
        public static void FetchCredits(Action<int> onSuccess, Action<string> onError)
        {
            var apiKey = UnlockSfxSettings.ApiKey;
            if (string.IsNullOrEmpty(apiKey))
            {
                onError("Add your API key first.");
                return;
            }

            var request = UnityWebRequest.Get(ApiBase + "/credits");
            request.SetRequestHeader("Authorization", "Bearer " + apiKey);

            request.SendWebRequest().completed += _ =>
            {
                if (!IsOk(request))
                {
                    var message = ExtractError(request);
                    request.Dispose();
                    onError(message);
                    return;
                }

                CreditsResponse parsed = null;
                try { parsed = JsonUtility.FromJson<CreditsResponse>(request.downloadHandler.text); }
                catch { /* handled below */ }
                request.Dispose();

                if (parsed == null)
                {
                    onError("Unexpected response from the server.");
                    return;
                }

                onSuccess(parsed.creditsRemaining);
            };
        }

        static bool IsOk(UnityWebRequest request)
        {
            return request.result == UnityWebRequest.Result.Success;
        }

        // Surface the API's JSON { "error": "..." } message when present, so users
        // see "You do not have enough credits..." instead of a raw HTTP code.
        static string ExtractError(UnityWebRequest request)
        {
            var body = request.downloadHandler != null ? request.downloadHandler.text : null;
            if (!string.IsNullOrEmpty(body))
            {
                try
                {
                    var parsed = JsonUtility.FromJson<ApiError>(body);
                    if (parsed != null && !string.IsNullOrEmpty(parsed.error))
                        return parsed.error;
                }
                catch { /* fall through */ }
            }

            return string.IsNullOrEmpty(request.error) ? "The request failed." : request.error;
        }
    }
}
