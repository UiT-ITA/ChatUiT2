using Microsoft.Azure.Cosmos.Serialization.HybridRow;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;

namespace ChatUiT2.Services;

public class SpeechService
{
    private string _subscriptionKey;
    private string _region;
    public SpeechService(IConfiguration configuration)
    {
        _subscriptionKey = configuration["SpeechService:SubscriptionKey"] ?? "";
        _region = configuration["SpeechService:Region"] ?? "";

        if (string.IsNullOrEmpty(_subscriptionKey) || string.IsNullOrEmpty(_region))
        {
            Console.WriteLine("SpeechService:SubscriptionKey and SpeechService:Region are required.");
            throw new InvalidOperationException("SpeechService:SubscriptionKey and SpeechService:Region are required.");
        }
    }

    public async Task<string> RecognizeSpeechAsync()
    {
        var speechConfig = SpeechConfig.FromSubscription(_subscriptionKey, _region);
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
        throw new NotImplementedException();
        var config = SpeechConfig.FromSubscription(_subscriptionKey, _region);
        config.SpeechSynthesisVoiceName = "en-US-AndrewMultilingualNeural";
        config.SetSpeechSynthesisOutputFormat(SpeechSynthesisOutputFormat.Riff16Khz16BitMonoPcm); // Set output format to WAV
        var memoryStreamPushAudioOutputStream = new MemoryStreamPushAudioOutputStream();
        var streamConfig = AudioConfig.FromStreamOutput(memoryStreamPushAudioOutputStream);
        using (var synthesizer = new SpeechSynthesizer(config, streamConfig))
        {
            using (var result = await synthesizer.SpeakTextAsync(text))
            {
                if (result.Reason == ResultReason.SynthesizingAudioCompleted)
                {
                    Console.WriteLine($"Speech synthesized for text [{text}]");
                    // Get the audio data from the custom stream
                    var audioData = memoryStreamPushAudioOutputStream.GetAudioData();
                    var base64Audio = Convert.ToBase64String(audioData);
                    Console.WriteLine($"Base64 Audio Data Length: {base64Audio.Length}"); // Log length for verification
                    Console.WriteLine($"First 100 characters of Base64 Audio Data: {base64Audio.Substring(0, 100)}"); // Log first 100 characters for verification
                    return base64Audio;
                }
                else if (result.Reason == ResultReason.Canceled)
                {
                    var cancellation = SpeechSynthesisCancellationDetails.FromResult(result);
                    Console.WriteLine($"CANCELED: Reason={cancellation.Reason}");
                    if (cancellation.Reason == CancellationReason.Error)
                    {
                        Console.WriteLine($"CANCELED: ErrorCode={cancellation.ErrorCode}");
                        Console.WriteLine($"CANCELED: ErrorDetails=[{cancellation.ErrorDetails}]");
                        Console.WriteLine($"CANCELED: Did you update the subscription info?");
                    }
                }
            }
        }
        return null;
    }
}

public class MemoryStreamPushAudioOutputStream : PushAudioOutputStreamCallback
{
    private readonly MemoryStream _memoryStream = new MemoryStream();
    public override uint Write(byte[] dataBuffer)
    {
        _memoryStream.Write(dataBuffer, 0, dataBuffer.Length);
        return (uint)dataBuffer.Length;
    }
    public override void Close()
    {
        _memoryStream.Close();
    }
    public byte[] GetAudioData()
    {
        return _memoryStream.ToArray();
    }
}


