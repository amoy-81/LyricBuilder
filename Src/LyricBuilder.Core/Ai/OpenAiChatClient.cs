using System.ClientModel;
using LyricBuilder.Abstractions.Domain.Exceptions;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;

namespace LyricBuilder.Core.Ai;

/// <summary>
/// One round trip to a chat model: a system prompt and a user message in, the reply out.
/// </summary>
public interface IChatCompletionClient
{
    /// <summary>
    /// Returns the model's reply as plain text. Throws <see cref="LyricBuilderException"/> when
    /// the model is unavailable or not configured.
    /// </summary>
    /// <remarks>
    /// No JSON mode on purpose. Models tend to put raw line breaks inside JSON strings when
    /// they write long multi-line text, and the provider then rejects the reply. Callers
    /// ask for delimiters in the prompt instead.
    /// </remarks>
    Task<string> CompleteAsync(string systemPrompt, string userMessage, CancellationToken ct);
}

/// <summary>
/// <see cref="IChatCompletionClient"/> over the OpenAI SDK, against whichever OpenAI-compatible
/// endpoint <see cref="OpenAiOptions"/> names.
/// </summary>
/// <remarks>
/// Singleton: the SDK client is thread-safe and holds the connection pool. The provider's error
/// bodies are logged here and kept out of the thrown exception, because an exception's message
/// reaches the client in the error response.
/// </remarks>
public sealed class OpenAiChatClient : IChatCompletionClient
{
    private readonly OpenAiOptions _options;
    private readonly ChatClient? _chatClient;
    private readonly ILogger<OpenAiChatClient> _logger;

    public OpenAiChatClient(IOptions<OpenAiOptions> options, ILogger<OpenAiChatClient> logger)
    {
        _options = options.Value;
        _logger = logger;

        // Without a key the SDK client cannot be built; requests fail with a clear error instead.
        if (_options.ApiKey.IsNullOrEmpty())
            return;

        var clientOptions = new OpenAIClientOptions { NetworkTimeout = TimeSpan.FromSeconds(_options.TimeoutSeconds) };
        if (_options.Endpoint.IsNotNullOrEmpty())
            clientOptions.Endpoint = new Uri(_options.Endpoint);

        _chatClient = new ChatClient(_options.Model, new ApiKeyCredential(_options.ApiKey), clientOptions);
    }

    public async Task<string> CompleteAsync(string systemPrompt, string userMessage, CancellationToken ct)
    {
        if (_chatClient is null)
            throw new LyricBuilderException(
                $"{OpenAiOptions.SectionName}:ApiKey is not configured",
                friendlyMessage: "AI writing is not available");

        var completionOptions = new ChatCompletionOptions
        {
            Temperature = _options.Temperature,
            MaxOutputTokenCount = _options.MaxOutputTokens
        };
        // The SDK marks reasoning effort experimental (OPENAI001); it maps to reasoning_effort,
        // which Groq and OpenAI both accept.
#pragma warning disable OPENAI001
        if (_options.ReasoningEffort.IsNotNullOrEmpty())
            completionOptions.ReasoningEffortLevel = new ChatReasoningEffortLevel(_options.ReasoningEffort);
#pragma warning restore OPENAI001

        ChatCompletion completion;
        try
        {
            completion = await _chatClient.CompleteChatAsync(
                [new SystemChatMessage(systemPrompt), new UserChatMessage(userMessage)],
                completionOptions,
                ct);
        }
        catch (ClientResultException e)
        {
            _logger.LogError(e, "chat model returned {StatusCode}", e.Status);

            throw e.Status == 429
                ? new LyricBuilderException("chat model rate limit reached", InternalErrorCode.ToManyRequest,
                    "the AI writer is busy, try again in a moment")
                : new LyricBuilderException($"chat model returned {e.Status}",
                    friendlyMessage: "the AI writer is unavailable");
        }

        var text = string.Concat(completion.Content.Select(part => part.Text));
        if (text.IsNotNullOrEmpty())
            return text;

        // Usually a reasoning model that spent the whole token budget before replying.
        _logger.LogWarning(
            "chat model returned no text: finish reason {FinishReason}, {OutputTokens} output tokens of which {ReasoningTokens} reasoning",
            completion.FinishReason,
            completion.Usage?.OutputTokenCount,
            completion.Usage?.OutputTokenDetails?.ReasoningTokenCount);

        throw completion.FinishReason == ChatFinishReason.Length
            ? new LyricBuilderException(
                $"chat model hit {OpenAiOptions.SectionName}:MaxOutputTokens ({_options.MaxOutputTokens}) before replying",
                friendlyMessage: "the AI writer ran out of room, try again")
            : new LyricBuilderException("chat model returned no text",
                friendlyMessage: "the AI writer returned nothing");
    }
}
