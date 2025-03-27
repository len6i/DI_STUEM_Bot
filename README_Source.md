[[_TOC_]]

# Demo Project for LLM in Unity

There are two scenes in this project which showcase current LLM capabilities and let you play around with them
by changing prompts and other settings.

## Scene 1: Knowledge Base Game

Showcases a small detective game with LLM characters that have a knowledge base.

### What Is It?

The scene is a changed variant of an example scene from the Unity plugin LLMUnity. LLMUnity allows you to use local LLM models in Unity.
Local means that an internet connection is not required to run the model and the model is stored on the device.

LLMUnity also implements RAG (Retrieval-Augmented Generation) which allows you to query a knowledge base and generate a response based on the query.

Each character in the scene has their own knowledge base and considers it when responding. Through the character's prompt you decide how the character deals with this knowledge.

I have extended the plugin to also support a connection to OpenAI's GPT API, so you can use that if you prefer.
However, to use this in an app that you intend to ship, you must not include the API key directly, which requires you to set up a separate server to deal with user authentication and change the code in this demo.

### Extending the Demo

You could extend the demo to work with audio input by using a Speech-to-Text (STT) and Text-to-Speech (TTS) plugin or service to work more naturally.

#### STT (Speech-to-Text))

For local STT, you could start with whisper.unity which is based on the open-source Whisper model. 

Be aware that you might require a Voice Activity Detector (VAD) to detect when the user is speaking.

- Cloud services
	- OpenAI
	- many other cloud services
- Local implementations
	- whisper.unity (based on Whisper)
	- Sherpa-onnx (based on Whisper)

#### TTS (Text-to-Speech)

For TTS you can start with the Piper implementation in ![Speech Generation System](https://assetstore.unity.com/packages/tools/audio/speech-generation-system-offline-text-to-speech-conversion-255039).
LLMUnity is also planning a TTS extension (OuteTTS). 
There are implementations of Kokoro TTS for C# but these are possibly not easy to get to work in Unity. (Sherpa-onnx (also includes STT) and Kokorosharp).

- Cloud services
	- OpenAI
	- Elevenlabs
	- many other cloud services
- Local implementations
	- OuteTTS (planned for LLMUnity)
	- Kokoro TTS (C# implementation)
	- Sherpa-onnx (C# implementation of Kokoro; also includes STT)
	- Speech Generation System (Piper)


## Scene 2: GPT Realtime Demo

Showcases OpenAI's speech-to-speech LLM which responds to user input in real-time.

### What Is It?

The scene features a rudimentary setup of a "character" that you can approach. When you enter the colored area,
a connection with GPT Realtime is established and the area turns green.
Press a button (left mouse button) to unmute the microphone. 
If you leave the area, the connection is severed and the area turns red.

### Notes

As here too an API key is required, shipping it in an app is not recommended.

To use the scene, you have to create a file "Assets/StreamingAssets/api-keys.json" which should contain

```json
{
	"OPENAI_API_KEY": "your-api-key"
}
```

You can also just copy the sample file "Assets/StreamingAssets/api-keys.json.sample" and rename it.


Watch out for the costs of using the API! Currently, it is rather expensive, especially if you do not use the "mini" model.
Set a hard limit in your OpenAI account to prevent getting out of this poor.

# AEC (Acoustic Echo Cancellation)

If you are not using a mute button or headphones, you have to deal with the audio coming from the speakers being picked up by the microphone.
This can be done with Acoustic Echo Cancellation (AEC). As of now, no AEC plugin for Unity exists. 
Smartphones implement hardware AEC but I am unsure on how to use it.

- ![StarTrinity AEC](http://startrinity.com/OpenSource/Aec/AecVadNoiseSuppressionLibrary.aspx) (C# implementation, apparently slow).
- ![Speex](https://www.speex.org/docs/api/speex-api-reference/group__SpeexEchoState.html) (Older C implementation, might be hard to get to work in Unity. There is a C# implementation.).
- ![WebRTC](https://github.com/jhgorse/webrtc-audio-processing/tree/master/webrtc/modules/audio_processing/aec) (C++ implementation). The Unity WebRTC plugin does not support AEC.
- ![PJSIP](https://docs.pjsip.org/en/2.15.1/api/generated/pjmedia/group/group__PJMEDIA__Echo__Cancel.html) (C implementation, includes 


# Character Animation

If you want to animate a character from audio input, you can start with the following plugins.

- ![Salsa LipSync](https://assetstore.unity.com/packages/tools/animation/salsa-lipsync-suite-148442)]
- ![SpeechBlend Lipsync](https://assetstore.unity.com/packages/tools/animation/speechblend-lipsync-149023)
- ![Nvidia Audio2face](https://build.nvidia.com/nvidia/audio2face-3d) (would require additional implementation to stream the results to Unity)


# Alternative Solutions

If you want to create avatars in Unity that can talk but use a more comprehensive solution,
you can take a look at the following plugins.
There may be high running costs associated with these plugins.

- ![Inworld AI](https://docs.inworld.ai/docs/tutorial-integrations/unity/)
- ![ConvAI](https://assetstore.unity.com/packages/tools/behavior-ai/npc-ai-engine-dialog-actions-voice-and-lipsync-convai-235621)
- ![AI Studio](https://assetstore.unity.com/packages/tools/ai-ml-integration/ai-studio-259885)

