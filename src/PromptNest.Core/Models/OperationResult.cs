namespace PromptNest.Core.Models;

public sealed record OperationResult
{
    public bool Succeeded { get; init; }

    public string? ErrorCode { get; init; }

    public string? Message { get; init; }

    public static OperationResult Success() => new() { Succeeded = true };

    public static OperationResult Failure(string errorCode, string message) =>
        new() { Succeeded = false, ErrorCode = errorCode, Message = message };
}

public sealed record OperationResult<T>
{
    public bool Succeeded { get; init; }

    public T? Value { get; init; }

    public string? ErrorCode { get; init; }

    public string? Message { get; init; }

}

public static class OperationResultFactory
{
    public static OperationResult<T> Success<T>(T value) => new() { Succeeded = true, Value = value };

    public static OperationResult<T> Failure<T>(string errorCode, string message) =>
        new() { Succeeded = false, ErrorCode = errorCode, Message = message };
}