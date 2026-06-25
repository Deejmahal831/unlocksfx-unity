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
    public class BankRequest
    {
        public string originalPrompt;
        public float durationSeconds;
        public bool loopable;
        public string outputFormat; // "mp3" | "wav"
        public float promptInfluence;
        public int count; // 2–8 variants
    }

    [Serializable]
    public class BankClip
    {
        public string id;
        public string title;
        public string format;
        public string downloadUrl;
    }

    [Serializable]
    public class BankResponse
    {
        public string bankName;
        public int count;
        public int creditsCharged;
        public int creditsRemaining;
        public BankClip[] clips;
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
            PostWithRetry(ApiBase + "/generate", json, apiKey, RetryCount, onError, body =>
            {
                GenerateResponse meta = null;
                try { meta = JsonUtility.FromJson<GenerateResponse>(body); }
                catch { /* handled below */ }

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
            });
        }

        /// <summary>
        /// Generate a variation bank (2–8 clips). On success returns the bank
        /// metadata plus the downloaded bytes for each clip, aligned to
        /// meta.clips by index (a clip that failed to download is null). Clips
        /// are downloaded one at a time to stay gentle on the signed-URL host.
        /// </summary>
        public static void GenerateBank(
            BankRequest request,
            Action<BankResponse, byte[][]> onSuccess,
            Action<string> onError)
        {
            var apiKey = UnlockSfxSettings.ApiKey;
            if (string.IsNullOrEmpty(apiKey))
            {
                onError("Add your API key first.");
                return;
            }

            var json = JsonUtility.ToJson(request);
            PostWithRetry(ApiBase + "/bank", json, apiKey, RetryCount, onError, body =>
            {
                BankResponse meta = null;
                try { meta = JsonUtility.FromJson<BankResponse>(body); }
                catch { /* handled below */ }

                if (meta == null || meta.clips == null || meta.clips.Length == 0)
                {
                    onError("The server did not return any clips.");
                    return;
                }

                var bytes = new byte[meta.clips.Length][];
                DownloadBankClip(meta, bytes, 0, onSuccess, onError);
            });
        }

        // Recursive sequential download: fetch clip `index`, then the next, until
        // all are done. A single failed clip is left null and we keep going, so a
        // partial bank still imports rather than throwing the whole batch away.
        static void DownloadBankClip(
            BankResponse meta,
            byte[][] bytes,
            int index,
            Action<BankResponse, byte[][]> onSuccess,
            Action<string> onError)
        {
            if (index >= meta.clips.Length)
            {
                onSuccess(meta, bytes);
                return;
            }

            var url = meta.clips[index].downloadUrl;
            if (string.IsNullOrEmpty(url))
            {
                DownloadBankClip(meta, bytes, index + 1, onSuccess, onError);
                return;
            }

            var download = UnityWebRequest.Get(url);
            download.SendWebRequest().completed += _ =>
            {
                if (IsOk(download))
                {
                    var data = download.downloadHandler.data;
                    if (data != null && data.Length > 0) bytes[index] = data;
                }
                download.Dispose();
                DownloadBankClip(meta, bytes, index + 1, onSuccess, onError);
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

        // How many times to automatically retry a failed POST before giving up.
        // Generation runs on serverless functions, so the first request after an
        // idle spell can cold-start and time out; a single retry usually self-heals.
        const int RetryCount = 1;

        // POST `json` to `url`, retrying transient failures up to `retriesLeft` times,
        // then hand the raw response body to `onBody`. Connection drops / timeouts /
        // 5xx are retried; real API errors (4xx, e.g. "not enough credits") are not —
        // those go straight to onError with the server's own message.
        static void PostWithRetry(
            string url, string json, string apiKey, int retriesLeft,
            Action<string> onError, Action<string> onBody)
        {
            var post = new UnityWebRequest(url, "POST");
            post.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            post.downloadHandler = new DownloadHandlerBuffer();
            post.SetRequestHeader("Content-Type", "application/json");
            post.SetRequestHeader("Authorization", "Bearer " + apiKey);

            post.SendWebRequest().completed += _ =>
            {
                if (IsOk(post))
                {
                    var text = post.downloadHandler.text;
                    post.Dispose();
                    onBody(text);
                    return;
                }

                var transient = IsTransient(post);
                var apiMessage = ExtractError(post);
                post.Dispose();

                if (transient && retriesLeft > 0)
                {
                    PostWithRetry(url, json, apiKey, retriesLeft - 1, onError, onBody);
                    return;
                }

                onError(transient
                    ? "Couldn't reach UnlockSFX — the request may have timed out. " +
                      "The first generation after a while can be slow; please try again."
                    : apiMessage);
            };
        }

        static bool IsOk(UnityWebRequest request)
        {
            return request.result == UnityWebRequest.Result.Success;
        }

        // A failure worth retrying: a dropped/timed-out connection, a response we
        // couldn't read, no HTTP status at all, or a 5xx server error. A 4xx is the
        // server deliberately rejecting us (bad key, no credits) — never retry those.
        static bool IsTransient(UnityWebRequest request)
        {
            if (request.result == UnityWebRequest.Result.ConnectionError) return true;
            if (request.result == UnityWebRequest.Result.DataProcessingError) return true;

            var code = request.responseCode;
            return code == 0 || code >= 500;
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
