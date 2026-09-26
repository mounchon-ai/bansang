namespace Bansang.Application.Abstractions;

public sealed class NotFoundException(string what, object key) : Exception($"ไม่พบ{what}: {key}");

public sealed class ConflictException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
