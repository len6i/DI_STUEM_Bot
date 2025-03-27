// LLMCharacterOpenAI.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace LLMUnity
{
    /// <summary>
    /// Subclass of LLMCharacter that adds OpenAI Chat Completions support
    /// by overriding the Chat method.
    /// </summary>
    public class LLMCharacterOpenAI : LLMCharacter
    {
        [LLM] public bool useOpenAI = false;
        [LLM] public string openAIKey = "";         // your "sk-..." key
        [LLM] public string openAIModel = "gpt-3.5-turbo";

        /// <summary>
        /// Override the Chat method so that, if useOpenAI = true, we build & send
        /// an OpenAI /chat/completions request with a 'messages' array instead
        /// of the single concatenated prompt. If useOpenAI = false, just do base.Chat().
        /// </summary>
        public override async Task<string> Chat(
            string query, 
            Callback<string> callback = null,
            EmptyCallback completionCallback = null,
            bool addToHistory = true)
        {
            // 1) If user doesn't want OpenAI, fallback to original logic
            if (!useOpenAI)
            {
                return await base.Chat(query, callback, completionCallback, addToHistory);
            }

            // 2) For OpenAI, skip base template logic. Instead, build messages[] ourselves
            // Skip LoadTemplate, CheckTemplate, and InitNKeep if using OpenAI
            if (!useOpenAI)
            {
                await LoadTemplate();        // if you need to load or validate your local template
                if (!CheckTemplate()) return null;
                if (!await InitNKeep()) return null;
            }

            // We'll lock the chat list to safely read from it
            await chatLock.WaitAsync();
            List<OpenAIMessage> openAIMessages = new List<OpenAIMessage>();
            try
            {
                // Convert your existing chat history into OpenAI's "messages" array
                // e.g. if chat[0] is system, add as role="system"
                // and so on for user vs. assistant.
                
                for (int i = 0; i < chat.Count; i++)
                {
                    // chat[i].role can be e.g. "system", "user", or "assistant".
                    string openAiRole = chat[i].role;
                    if (openAiRole == playerName) openAiRole = "user";
                    else if (openAiRole == AIName) openAiRole = "assistant";

                    openAIMessages.Add(new OpenAIMessage
                    {
                        role    = openAiRole,
                        content = chat[i].content
                    });
                }

                // Finally, add the new user message
                openAIMessages.Add(new OpenAIMessage
                {
                    role    = "user",
                    content = query
                });
            }
            finally
            {
                chatLock.Release();
            }

            // 3) Build the OpenAI request object
            OpenAIChatRequest openAIRequest = new OpenAIChatRequest
            {
                model       = openAIModel,
                messages    = openAIMessages,
                max_tokens  = (numPredict > 0 ? numPredict : 256),
                temperature = temperature,
                top_p       = topP,
                stop        = (stop != null && stop.Count > 0)
                              ? stop.ToArray()
                              : null,
                stream      = false // handle SSE yourself if you want streaming
            };

            // Convert to JSON
            string openAIJson = JsonUtility.ToJson(openAIRequest);

            Debug.Log(openAIJson);

            // 4) Send to OpenAI
            string result = await PostRequestOpenAI<OpenAIChatResponse, string>(
                openAIJson,
                "chat/completions",
                GetOpenAIChatContent,
                callback
            );

            // 5) Optionally add to your local chat history & save
            if (addToHistory && !string.IsNullOrEmpty(result))
            {
                await chatLock.WaitAsync();
                try
                {
                    AddPlayerMessage(query);
                    AddAIMessage(result);
                }
                finally
                {
                    chatLock.Release();
                }
                if (!string.IsNullOrEmpty(save)) _ = Save(save);
            }

            completionCallback?.Invoke();
            Debug.Log(result);
            return result;
        }

        protected async Task<Ret> PostRequestOpenAI<Res, Ret>(
            string json,
            string endpoint,
            Func<Res, Ret> getContent,
            Callback<Ret> callback = null)
        {
            using (UnityWebRequest request =
                new UnityWebRequest("https://api.openai.com/v1/" + endpoint, "POST"))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
                request.uploadHandler   = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type",  "application/json");
                request.SetRequestHeader("Authorization", $"Bearer {openAIKey}");

                await request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"OpenAI Error: {request.error}\n{request.downloadHandler.text}");
                    callback?.Invoke(default);
                    return default;
                }

                string responseText = request.downloadHandler.text;
                Res resObj          = JsonUtility.FromJson<Res>(responseText);
                Ret finalVal        = getContent(resObj);
                callback?.Invoke(finalVal);
                return finalVal;
            }
        }

        protected string GetOpenAIChatContent(OpenAIChatResponse response)
        {
            if (response.choices != null && response.choices.Length > 0)
            {
                return response.choices[0].message.content.Trim();
            }
            return "";
        }
    }

    [Serializable]
    public class OpenAIChatRequest
    {
        public string model;
        public List<OpenAIMessage> messages;
        public int max_tokens;
        public float temperature;
        public float top_p;
        public bool stream;
        public string[] stop;
    }

    [Serializable]
    public class OpenAIMessage
    {
        public string role;
        public string content;
    }

    [Serializable]
    public class OpenAIChatResponse
    {
        public string id;
        public string @object;
        public long created;
        public OpenAIChoice[] choices;
    }

    [Serializable]
    public class OpenAIChoice
    {
        public int index;
        public OpenAIMessage message;
        public string finish_reason;
    }
}
