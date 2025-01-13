using ChatUiT2.Interfaces;
using Microsoft.Azure.Cosmos.Serialization.HybridRow;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Extensions.Configuration;
using Microsoft.JSInterop;

namespace ChatUiT2.Services;

public class SpeechService
{
    private Action? audioCallback;

    private string _subscriptionKey;
    private string _serviceRegion;
    private IJSRuntime JS;
    private IUpdateService _updateService;

    public SpeechService(IConfiguration configuration, IJSRuntime jSRuntime, IUpdateService updateService)
    {
        _subscriptionKey = configuration["SpeechServiceKey"] ?? "";
        _serviceRegion = configuration["SpeechServiceRegion"] ?? "";
        JS = jSRuntime;
        _updateService = updateService;

        if (string.IsNullOrEmpty(_subscriptionKey) || string.IsNullOrEmpty(_serviceRegion))
        {
            Console.WriteLine("SpeechServiceKey and SpeechServiceRegion are required.");
            throw new InvalidOperationException("SpeechServiceKey and SpeechServiceRegion are required.");
        }
    }

    public async Task<string> RecognizeSpeechAsync()
    {
        var speechConfig = SpeechConfig.FromSubscription(_subscriptionKey, _serviceRegion);
        using (var recognizer = new SpeechRecognizer(speechConfig))
        {
            var result = await recognizer.RecognizeOnceAsync();
            if (result.Reason == ResultReason.RecognizedSpeech)
            {
                Console.WriteLine($"We recognized: {result.Text}");
                return result.Text;
            }
            else if (result.Reason == ResultReason.NoMatch)
            {
                Console.WriteLine($"NOMATCH: Speech could not be recognized.");
            }
            else if (result.Reason == ResultReason.Canceled)
            {
                var cancellation = CancellationDetails.FromResult(result);
                Console.WriteLine($"CANCELED: Reason={cancellation.Reason}");

                if (cancellation.Reason == CancellationReason.Error)
                {
                    Console.WriteLine($"CANCELED: ErrorCode={cancellation.ErrorCode}");
                    Console.WriteLine($"CANCELED: ErrorDetails={cancellation.ErrorDetails}");
                    Console.WriteLine($"CANCELED: Did you update the subscription info?");
                }
            }
        }
        return "";


    }
    //var languageConfig = AutoDetectSourceLanguageConfig.FromLanguages(new string[] { "en-US", "nb-NO" });

    public async Task<string> GenerateSpeechAsync(string text)
    {
        var config = SpeechConfig.FromSubscription(_subscriptionKey, _serviceRegion);
        config.SpeechSynthesisVoiceName = "en-US-AndrewMultilingualNeural";
        config.SetSpeechSynthesisOutputFormat(SpeechSynthesisOutputFormat.Audio16Khz32KBitRateMonoMp3);
        using var memoryStream = new MemoryStream();
        using var audioOutputStream = AudioOutputStream.CreatePushStream(new CustomPushAudioOutputStream(memoryStream));
        using var audioConfig = AudioConfig.FromStreamOutput(audioOutputStream);
        using var synthesizer = new SpeechSynthesizer(config, audioConfig);

        var result = await synthesizer.SpeakTextAsync(text);

        if (result.Reason == ResultReason.SynthesizingAudioCompleted)
        {
            var audioBytes = memoryStream.ToArray();
            var base64 =  Convert.ToBase64String(audioBytes);
            return $"data:audio/mp3;base64,{base64}";
        }
        else if (result.Reason == ResultReason.Canceled)
        {
            var cancellation = SpeechSynthesisCancellationDetails.FromResult(result);
            throw new Exception($"Speech synthesis canceled: {cancellation.Reason}, {cancellation.ErrorDetails}");
        }

        throw new Exception("Speech synthesis failed.");
    }

    public async void PlayAudio(string base64, Action callback)
    {
        Console.WriteLine("Playing audio");
        if (audioCallback != null)
        {
            audioCallback();
        }
        audioCallback  = callback;

        var objRef = DotNetObjectReference.Create(this);

        await JS.InvokeVoidAsync("playAudio", base64, objRef);
        _updateService.Update(UpdateType.Global);

    }

    // Input: base64 audio string
    public void PauseAudio()
    {
        JS.InvokeVoidAsync("pauseAudio");
        if (audioCallback != null)
        {
            audioCallback();
            audioCallback = null;
        }
        _updateService.Update(UpdateType.Global);
    }

    public void RestartAudio()
    {
        JS.InvokeVoidAsync("restartAudio");
    }

    [JSInvokable]
    public void OnAudioEnded()
    {
        if (audioCallback != null)
        {
            audioCallback();
            audioCallback = null;
        }
        _updateService.Update(UpdateType.Global);
    }
}


public class CustomPushAudioOutputStream : PushAudioOutputStreamCallback
{
    private readonly MemoryStream _stream;

    public CustomPushAudioOutputStream(MemoryStream stream)
    {
        _stream = stream;
    }

    public override uint Write(byte[] dataBuffer)
    {
        _stream.Write(dataBuffer, 0, dataBuffer.Length);
        return (uint)dataBuffer.Length;
    }

    public override void Close()
    {
        _stream.Close();
        base.Close();
    }
}


