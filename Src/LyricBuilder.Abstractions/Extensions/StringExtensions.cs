using System.Diagnostics.CodeAnalysis;

namespace LyricBuilder.Abstractions.Extensions;

public static class StringExtensions
{
    public static bool IsNullOrEmpty([NotNullWhen(false)] this string? value) => string.IsNullOrWhiteSpace(value);

    public static bool IsNotNullOrEmpty([NotNullWhen(true)] this string? value) => !string.IsNullOrWhiteSpace(value);
}
