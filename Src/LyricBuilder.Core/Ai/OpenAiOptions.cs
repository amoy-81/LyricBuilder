namespace LyricBuilder.Core.Ai;

/// <summary>
/// Bound from the <c>OpenAI</c> configuration section. Any OpenAI-compatible provider works:
/// set <see cref="Endpoint"/> to its URL (Groq's is <c>https://api.groq.com/openai/v1</c>), or
/// leave it empty for OpenAI itself.
/// </summary>
public class OpenAiOptions
{
    public const string SectionName = "OpenAI";

    /// <summary>Keep it out of source control; set it with user-secrets.</summary>
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>Base URL of an OpenAI-compatible API. Empty means OpenAI's own.</summary>
    public string? Endpoint { get; init; }

    /// <summary>A chat model the endpoint serves.</summary>
    public string Model { get; init; } = string.Empty;

    /// <summary>
    /// Lower keeps the output close to the reference examples; higher gives freer imagery.
    /// </summary>
    public float Temperature { get; init; } = 0.7f;

    /// <summary>
    /// Covers a reasoning model's hidden reasoning as well as the reply, so it is set well
    /// above what a section needs. Too low, and such a model returns an empty reply.
    /// </summary>
    public int MaxOutputTokens { get; init; } = 4096;

    /// <summary>
    /// <c>low</c>, <c>medium</c> or <c>high</c>, for reasoning models such as
    /// <c>openai/gpt-oss-120b</c>. Leave empty for other models, which reject it.
    /// </summary>
    public string? ReasoningEffort { get; init; }

    public int TimeoutSeconds { get; init; } = 60;
}
