﻿using ChatUiT2.Interfaces;
using ChatUiT2.Models.Mediatr;
using ChatUiT2.Models;
using ChatUiT2.Models.RagProject;
using Microsoft.Extensions.Logging;
using MediatR;
using System.Text.Json;
using System.Text.RegularExpressions;
using Ganss.Xss;
using OpenAI.Embeddings;

namespace ChatUiT2.Services;

/// <summary>
/// Class for common rag generation operations like generate rag db create embeddings.
/// Depends on the rag database class.
/// This is separate from the database service because the database service is
/// a singleton for efficiency.
/// </summary>
public class RagGeneratorService : IRagGeneratorService
{
    private readonly IRagDatabaseService _ragDatabaseService;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<RagGeneratorService> _logger;
    private readonly IMediator _mediator;
    private readonly IUsernameService _usernameService;

    public RagGeneratorService(IRagDatabaseService ragDatabaseService,
                                ISettingsService settingsService,
                                ILogger<RagGeneratorService> logger,
                                IMediator mediator,
                                IUsernameService usernameService)
    {
        this._ragDatabaseService = ragDatabaseService;
        this._settingsService = settingsService;
        this._logger = logger;
        this._mediator = mediator;
        this._usernameService = usernameService;
    }

    public async Task GenerateRagQuestionsFromContent(RagProject ragProject, ContentItem item)
    {
        try
        {
            string textContent = _ragDatabaseService.GetItemContentString(item);
            var questionsFromLlm = await GenerateQuestionsFromContent(textContent,
                                                                      ragProject.Configuration?.MinNumberOfQuestionsPerItem ?? 5,
                                                                      ragProject.Configuration?.MaxNumberOfQuestionsPerItem ?? 20);
            var model = _settingsService.EmbeddingModel;
            var openAIService = new OpenAIService(model, "System", _logger, _mediator, null!);
            var embedding = await openAIService.GetEmbedding(textContent);
            if (questionsFromLlm != null)
            {
                foreach (var question in questionsFromLlm.Questions)
                {
                    await _ragDatabaseService.AddRagTextEmbedding(ragProject, item.Id, EmbeddingSourceType.Question, embedding, question);
                }
            }
            else
            {
                throw new Exception("No questions generated by LLM");
            }
        }
        catch (Exception e)
        {
            throw new Exception($"Noe feilet ved generering av spørsmål for item {item.Id} {e.Message}");
        }
    }

    public async Task<QuestionsFromTextResult?> GenerateQuestionsFromContent(string content, int numToGenerateMin = 5, int numToGenerateMax = 20)
    {
        AiModel gpt4MiniModel = _settingsService.GetModel("GPT-4o-Mini");
        WorkItemChat chat = new();
        chat.Settings = new ChatSettings()
        {
            MaxTokens = gpt4MiniModel.MaxTokens,
            Model = gpt4MiniModel.DeploymentName,
            Temperature = 0.5f
        };
        chat.Type = WorkItemType.Chat;
        chat.Settings.Prompt = $"Using the input that is a knowledge article, generate between {numToGenerateMin} and {numToGenerateMax} questions a person may ask that this article answers. Generate the questions in norwegian language. Give me the answer as json in the following format: {{ \"Questions\" : [ \"question1\", \"question2\" ] }}. Return the json string only no other information. Do not include ```json literal.";
        chat.Messages.Add(new ChatUiT2.Models.ChatMessage()
        {
            Role = ChatMessageRole.User,
            Content = content
        });
        var chatResponse = await GetChatResponseAsString(chat, gpt4MiniModel);
        return JsonSerializer.Deserialize<QuestionsFromTextResult>(chatResponse);
    }

    /// <summary>
    /// When you just want the response as a string
    /// No streaming handling needed
    /// </summary>
    /// <param name="chat"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public async Task<string> GetChatResponseAsString(WorkItemChat chat, AiModel? model = null)
    {
        string result = string.Empty;
        if (model == null)
        {
            model = _settingsService.DefaultModel;
        }

        if (model.DeploymentType == DeploymentType.AzureOpenAI)
        {
            var openAIService = new OpenAIService(model, await _usernameService.GetUsername(), _logger, null!, null!);

            result = await openAIService.GetResponse(chat);
        }
        else
        {
            throw new Exception("Unsupported deployment type: " + model.DeploymentType);
        }

        return result;
    }

    public async Task GenerateRagParagraphsFromContent(RagProject ragProject, ContentItem item, int minParagraphSize = 150)
    {
        try
        {
            var model = _settingsService.EmbeddingModel;
            string textContent = _ragDatabaseService.GetItemContentString(item);
            var paragraphs = SplitTextIntoParagraphs(textContent);
            foreach (var paragraph in paragraphs)
            {
                if (paragraph.Length < minParagraphSize)
                {
                    continue;
                }
                var openAIService = new OpenAIService(model, "System", _logger, _mediator, null!);
                var embedding = await openAIService.GetEmbedding(textContent);
                await _ragDatabaseService.AddRagTextEmbedding(ragProject, item.Id, EmbeddingSourceType.Paragraph, embedding, paragraph);
            }
        }
        catch (Exception e)
        {
            throw new Exception($"Noe feilet ved generering av paragraph embeddings for item {item.Id} {e.Message}");
        }
    }

    public IEnumerable<string> SplitTextIntoParagraphs(string text, bool removeHtmlTags = true, bool convertBrTagsToNewlines = true)
    {
        if (convertBrTagsToNewlines)
        {
            text = ReplaceHtmlLinebreaksWithNewline(text);
        }
        if (removeHtmlTags)
        {
            text = RemoveAllHtmlTagsFromString(text);
        }
        string pattern = @"\n\s*\n";
        string strWithNormalizedDoubleNewline = Regex.Replace(text, pattern, "\n\n", RegexOptions.IgnoreCase);
        return strWithNormalizedDoubleNewline.Split(new string[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
    }

    public string RemoveAllHtmlTagsFromString(string text)
    {
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedTags.Clear();
        sanitizer.KeepChildNodes = true;
        return sanitizer.Sanitize(text);
    }

    public string ReplaceHtmlLinebreaksWithNewline(string text)
    {
        // Regular expression to match all variants of <br> tags
        string pattern = @"<br\s*/?>";
        string result = Regex.Replace(text, pattern, "\n", RegexOptions.IgnoreCase);
        return result;
    }

    public async Task<OpenAIEmbedding> GetEmbeddingForText(string text, string username)
    {
        var model = _settingsService.EmbeddingModel;
        var openAIService = new OpenAIService(model, username, _logger, _mediator, null!);
        return await openAIService.GetEmbedding(text);
    }
}
