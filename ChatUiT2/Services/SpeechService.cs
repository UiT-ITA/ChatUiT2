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

    public async Task GenerateSpeechAsync(string text)
    {
        var config = SpeechConfig.FromSubscription(_subscriptionKey, _region,);
        config.SpeechSynthesisVoiceName = "en-US-AndrewMultilingualNeural"; 

        using (var synthesizer = new SpeechSynthesizer(config))
        {
            using (var result = await synthesizer.SpeakTextAsync(text))
            {
                if (result.Reason == ResultReason.SynthesizingAudioCompleted)
                {
                    Console.WriteLine($"Speech synthesized for text [{text}]");
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

    }
}


