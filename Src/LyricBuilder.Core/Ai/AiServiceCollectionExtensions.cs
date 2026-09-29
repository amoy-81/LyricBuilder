using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LyricBuilder.Core.Ai;

public static class AiServiceCollectionExtensions
{
    /// <summary>
    /// Registers the OpenAI chat client. Unlike JWT settings, a missing API key does not stop
    /// startup: the rest of the app works without it, and AI requests fail with a clear error.
    /// </summary>
    public static IServiceCollection AddOpenAiChat(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<OpenAiOptions>(configuration.GetSection(OpenAiOptions.SectionName));
        services.AddSingleton<IChatCompletionClient, OpenAiChatClient>();

        return services;
    }
}
