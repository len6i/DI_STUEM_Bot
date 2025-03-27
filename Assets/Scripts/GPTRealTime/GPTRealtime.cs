using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Unity.WebRTC;
using UnityEngine.Events;
using System.IO;

/* TODO: 
    - send event session.update (make changes to system prompt after start)
    - move to its own package
    - check if we can remove the sendIceCandidate function
    - add additional parameters
    - separate RTC connection from OpenAI connection (RTC needs to be started and stopped just once per game?)
        Maybe create an "RTC Connection Manager".
    - generally refactor, as the functionality will continue to grow
    - test if it works with multiple characters.
 */

namespace GptRealtime
{
    class GPTRealtime : MonoBehaviour
    {

        [SerializeField] private UnityEvent onStatusChange;
        
        [SerializeField] private UnityEvent onConnected;

        [SerializeField] private UnityEvent onDisconnected;


        [SerializeField] private AudioSource inputAudioSource;
        [SerializeField] private AudioSource outputAudioSource;

        [Header("OpenAI Settings")]
        // Remove the apiKey field from the inspector
        // [SerializeField]
        private string apiKey;

        [SerializeField, TextArea(5, 10)] private string prompt = "You are a pigeon-hating squirrel and respond in rhymes.";

        [SerializeField]
        private VoiceOptions voiceOption = VoiceOptions.Alloy;

        [SerializeField]
        private ModelOptions openAIModel = ModelOptions.Gpt4oMiniRealtimePreview20241217;

        [SerializeField, Range(0.6f, 1.2f)]
        private float temperature = 0.8f;

        [SerializeField, Range(1, 100), Tooltip("Discard messages after this maximum. This is a measure to reduce costs.")]
        private int maxConversationItems = 10;

        private RTCPeerConnection _pc;
        private AudioStreamTrack m_audioTrack;
        private string m_deviceName = null;

        private string ephemeralKey;

        private bool _isConnected = false;

        private RTCDataChannel _eventDataChannel;
        private List<ConversationItem> _conversationItems = new List<ConversationItem>();

        private string openAIEndpoint = "https://api.openai.com/v1/realtime";

        public void SelectMicrophoneDevice(string deviceName)
        {
            m_deviceName = deviceName;
            Debug.Log("Selected microphone: " + m_deviceName);
        }

        public void Call()
        {   
            if (_isConnected)
            {
                Debug.Log("Already connected");
                return;
            }
            StartCoroutine(CreateSession());
        }


        void Start()
        {
            // Set Microphone device to first in list if it is not set
            if (m_deviceName == null)
            {
                m_deviceName = Microphone.devices.FirstOrDefault();
            }
            StartCoroutine(Initialize());
        }

        IEnumerator Initialize()
        {
            // Load the API key from streaming assets (works on WebGL/Android/iOS)
            yield return StartCoroutine(LoadStreamingAssetsApiKey());
            yield return StartCoroutine(WebRTC.Update());
        }

        // New coroutine method to load API key asynchronously from StreamingAssets
        IEnumerator LoadStreamingAssetsApiKey()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "api-keys.json");
            string json = "";
            if (path.Contains("://") || path.Contains(":///"))
            {
                UnityWebRequest uwr = UnityWebRequest.Get(path);
                yield return uwr.SendWebRequest();
                if (uwr.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError("Failed to load API keys: " + uwr.error);
                }
                else
                {
                    json = uwr.downloadHandler.text;
                }
            }
            else
            {
                json = System.IO.File.ReadAllText(path);
            }

            ApiKeys apiKeys = JsonUtility.FromJson<ApiKeys>(json);
            apiKey = apiKeys.OPENAI_API_KEY;
            if (string.IsNullOrEmpty(apiKey))
            {
                Debug.LogError("API key not found in " + path);
            }
        }

        [System.Serializable]
        private class ApiKeys
        {
            public string OPENAI_API_KEY;
        }

        IEnumerator InitializeWebRTC()
        {
            Debug.Log("Initializing WebRTC on microphone: " + m_deviceName);
            var clip = Microphone.Start(m_deviceName, true, 1, 48000);
            while (!(Microphone.GetPosition(m_deviceName) > 0)) { }
            inputAudioSource.loop = true;
            inputAudioSource.clip = clip;
            inputAudioSource.Play();

            // Create peer connection
            var config = GetOpenAIConfiguration();
            _pc = new RTCPeerConnection(ref config)
            {
                //OnIceCandidate = candidate => StartCoroutine(SendIceCandidate(candidate)),
                OnTrack = e => OnOpenAITrackReceived(e.Track),
                OnIceConnectionChange = state => OnIceConnectionChange(state),
                OnDataChannel = OnDataChannelReceived
            };

            // Add audio track
            m_audioTrack = new AudioStreamTrack(inputAudioSource);
            _pc.AddTrack(m_audioTrack);

            // Create data channel for events
            _eventDataChannel = _pc.CreateDataChannel("oai-events");
            _eventDataChannel.OnMessage = OnEventMessageReceived;

            // Start negotiation
            yield return StartCoroutine(CreateOffer());

        }

        void OnIceConnectionChange(RTCIceConnectionState state)
        {
            Debug.Log($"ICE Connection: {state}");

            if (state == RTCIceConnectionState.Connected)
            {
                OnConnected();
            }
            else if (state == RTCIceConnectionState.Closed)
            {
                OnDisconnected();
            }
        }

        void OnConnected()
        {
            UpdateStatus("Connected - Speaking with AI");
            _isConnected = true;
            onConnected.Invoke();
        }

        void OnDisconnected()
        {
            UpdateStatus("Disconnected");
            _isConnected = false;
            onDisconnected.Invoke();
        }

        IEnumerator CreateSession()
        {
            var url = $"{openAIEndpoint}/sessions";
            var request = new UnityWebRequest(url, "POST");
            var sessionData = new SessionRequest
            {
                model = GetModelOption(openAIModel),
                modalities = new[] { "audio", "text" },
                instructions = prompt,
                voice = GetVoiceOption(voiceOption),
                temperature = temperature
            };
            Debug.Log(sessionData);
            string json = JsonUtility.ToJson(sessionData);
            Debug.Log(json);
            byte[] body = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {apiKey}");

            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.Log("Create session failed: " + request.error);
                onStatusChange.Invoke(); //("Failed to create session: " + request.error);
            }
            else
            {
                var response = JsonUtility.FromJson<SessionResponse>(request.downloadHandler.text);
                Debug.Log("Session create response: " + request.downloadHandler.text);
                ephemeralKey = response.client_secret.value;
                Debug.Log("Ephemeral Key: " + ephemeralKey);

                yield return StartCoroutine(InitializeWebRTC());

            }
        }

        IEnumerator CreateOffer()
        {
            var op = _pc.CreateOffer();
            yield return op;

            if (!op.IsError)
            {
                yield return StartCoroutine(SetLocalDescription(op.Desc));
                yield return StartCoroutine(SendOffer(op.Desc));
            }
            else
            {
                Debug.LogError("CreateOffer failed: " + op.Error.message);
            }
        }

        IEnumerator SetLocalDescription(RTCSessionDescription desc)
        {
            var op = _pc.SetLocalDescription(ref desc);
            yield return op;
            if (op.IsError) Debug.LogError("SetLocalDescription failed: " + op.Error.message);
        }

        IEnumerator SendOffer(RTCSessionDescription offer)
        {
            string model = GetModelOption(openAIModel);
            var url = $"{openAIEndpoint}?model={model}";

            var request = new UnityWebRequest(url, "POST");
            byte[] body = Encoding.UTF8.GetBytes(offer.sdp);
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/sdp");
            request.SetRequestHeader("Authorization", $"Bearer {ephemeralKey}");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                var answer = new RTCSessionDescription
                {
                    type = RTCSdpType.Answer,
                    sdp = request.downloadHandler.text
                };
                Debug.Log("Answer: " + answer.sdp);
                yield return StartCoroutine(SetRemoteDescription(answer));
            }
            else
            {
                Debug.LogError("Offer failed: " + request.error);
            }
        }

        IEnumerator SetRemoteDescription(RTCSessionDescription desc)
        {
            Debug.Log("SetRemoteDescription: " + desc.sdp);
            var op = _pc.SetRemoteDescription(ref desc);
            yield return op;
            if (op.IsError) Debug.LogError("SetRemoteDescription failed: " + op.Error.message);
        }

        // todo: We do not use this function, can be removed?
        IEnumerator SendIceCandidate(RTCIceCandidate candidate)
        {
            string model = GetModelOption(openAIModel);

            var url = $"{openAIEndpoint}/{model}";
            var request = new UnityWebRequest(url, "POST");
            var candidateData = new
            {
                candidate = candidate.Candidate,
                sdpMid = candidate.SdpMid,
                sdpMLineIndex = candidate.SdpMLineIndex
            };
            string json = JsonUtility.ToJson(candidateData);
            byte[] body = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/sdp");
            request.SetRequestHeader("Authorization", $"Bearer {apiKey}");

            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("ICE candidate failed: " + request.error);
                Debug.LogError(request.result);
            }
        }

        void OnOpenAITrackReceived(MediaStreamTrack track)
        {
            if (track is AudioStreamTrack audioTrack)
            {
                outputAudioSource.SetTrack(audioTrack);
                outputAudioSource.loop = true;
                outputAudioSource.Play();
                UpdateStatus("Received audio track");
            }
            // todo: deal with text track
        }

        void OnDataChannelReceived(RTCDataChannel channel)
        {
            if (channel.Label == "oai-events")
            {
                _eventDataChannel = channel;
                _eventDataChannel.OnMessage = OnEventMessageReceived;
            }
        }

        void OnEventMessageReceived(byte[] bytes)
        {
            var message = Encoding.UTF8.GetString(bytes);
            Debug.Log("Server event received: " + message);

            var eventData = JsonUtility.FromJson<EventItem>(message);
            if (eventData.type == "conversation.item.created")
            {
                var conversationEvent = JsonUtility.FromJson<ConversationItemCreatedEvent>(message);
                var item = new ConversationItem
                {
                    id = conversationEvent.item.id,
                    role = conversationEvent.item.role,
                    content = conversationEvent.item.content
                };
                _conversationItems.Add(item);

                if (_conversationItems.Count > maxConversationItems)
                {
                    var oldestItem = _conversationItems[0];
                    _conversationItems.RemoveAt(0);
                    SendDeleteEvent(oldestItem.id);
                }
            }
        }

        void SendDeleteEvent(string itemId)
        {
            var deleteEvent = new DeleteEvent
            {
                event_id = Guid.NewGuid().ToString(),
                type = "conversation.item.delete",
                item_id = itemId
            };
            SendClientEvent(deleteEvent);
        }

        public void SendClientEvent(object eventObject)
        {
            if (_eventDataChannel != null && _eventDataChannel.ReadyState == RTCDataChannelState.Open)
            {
                string json = JsonUtility.ToJson(eventObject);
                _eventDataChannel.Send(json);
                Debug.Log("Client event sent: " + json);
            }
            else
            {
                Debug.LogWarning("Data channel is not open. Cannot send event.");
            }
        }

        public void HangUp()
        {
            Microphone.End(m_deviceName);

            ephemeralKey = null;
            if (!_isConnected)
            {
                Debug.Log("Not connected");
                return;
            }
            if (m_audioTrack != null)
            {
                m_audioTrack.Dispose();
                m_audioTrack = null;
            }
            if (_pc != null)
            {
                _pc.Close();
                _pc.Dispose();
                _pc = null;
            }

            inputAudioSource.Stop();
            outputAudioSource.Stop();

            _conversationItems.Clear();

            OnDisconnected();
        }

        RTCConfiguration GetOpenAIConfiguration()
        {
            return new RTCConfiguration
            {
                iceServers = new[] { new RTCIceServer { urls = new[] { "stun:stun.l.google.com:19302" } } }
            };
        }

        void UpdateStatus(string message)
        {
            Debug.Log(message);
            onStatusChange.Invoke(); //message);
        }

        void OnDestroy()
        {
            HangUp();
        }

        [Serializable]
        private class SessionRequest
        {
            public string model;
            public string[] modalities;
            public string instructions;
            public string voice;
            public float temperature;
            /*
            public string input_audio_format;
            public string output_audio_format;
            public InputAudioTranscription input_audio_transcription;
            public string turn_detection;
            public string[] tools;
            public string tool_choice;
            public int max_response_output_tokens; */
        }

        [Serializable]
        private class SessionResponse
        {
            public string id;
            public string @object;
            public string model;
            public string[] modalities;
            public string instructions;
            public string voice;
            public string input_audio_format;
            public string output_audio_format;
            public InputAudioTranscription input_audio_transcription;
            public TurnDetection turn_detection;
            public string[] tools;
            public string tool_choice;
            public float temperature;
            public int max_response_output_tokens;
            public ClientSecret client_secret;
        }

        [Serializable]
        private class InputAudioTranscription
        {
            public string model;
        }

        [Serializable]
        private class ClientSecret
        {
            public string value;
            public long expires_at;
        }

        private class TurnDetection
        {
            public string type;
            public float threshold;
            public string prefix_padding_ms;

            public string silence_duration_ms;
            public bool create_response;
        }

        private string GetModelOption(ModelOptions modelOption)
        {
            switch (modelOption)
            {
                case ModelOptions.Gpt4oRealtimePreview20241217:
                    return "gpt-4o-realtime-preview-2024-12-17";
                case ModelOptions.Gpt4oMiniRealtimePreview20241217:
                    return "gpt-4o-mini-realtime-preview-2024-12-17";
                default:
                    return "gpt-4o-mini-realtime-preview-2024-12-17";
            }
        }

        private string GetVoiceOption(VoiceOptions voiceOption)
        {
            switch (voiceOption)
            {
                case VoiceOptions.Alloy:
                    return "alloy";
                case VoiceOptions.Ash:
                    return "ash";
                case VoiceOptions.Ballad:
                    return "ballad";
                case VoiceOptions.Coral:
                    return "coral";
                case VoiceOptions.Echo:
                    return "echo";
                case VoiceOptions.Sage:
                    return "sage";
                case VoiceOptions.Shimmer:
                    return "shimmer";
                case VoiceOptions.Verse:
                    return "verse";
                default:
                    return "alloy";
            }
        }

        [Serializable]
        private class EventItem
        {
            public string event_id;
            public string type;
        }

        [Serializable]
        private class ConversationItemCreatedEvent : EventItem
        {
            public EventItemDetails item;
        }

        [Serializable]
        private class EventItemDetails
        {
            public string id;
            public string @object;
            public string type;
            public string status;
            public string role;
            public List<Content> content;
        }

        [Serializable]
        private class Content
        {
            public string type;
            public string transcript;
            public string audio;
        }

        [Serializable]
        private class ConversationItem
        {
            public string id;
            public string role;
            public List<Content> content;
        }

        [Serializable]
        private class DeleteEvent : EventItem
        {
            public string item_id;
        }
    }

    public enum VoiceOptions
    {
        Alloy,
        Ash,
        Ballad,
        Coral,
        Echo,
        Sage,
        Shimmer,
        Verse
    }

    public enum ModelOptions
    {
        Gpt4oRealtimePreview20241217,
        Gpt4oMiniRealtimePreview20241217,
    }

    public class StatusEvent : UnityEvent { }

}