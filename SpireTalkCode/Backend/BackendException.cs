namespace SpireTalk.Backend;

/// <summary>A failure talking to the model endpoint, with a code Loc.DescribeError can localize.</summary>
public sealed class BackendException : Exception
{
    public int? StatusCode { get; }
    public string? Code { get; }

    public BackendException(string message, int? statusCode = null, string? code = null, Exception? inner = null)
        : base(message, inner)
    {
        StatusCode = statusCode;
        Code = code;
    }
}
