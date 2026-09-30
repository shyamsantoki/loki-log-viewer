using System.Net;

namespace LokiLogViewer.Client.Services;

public sealed class LogApiException(HttpStatusCode statusCode, string message) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}
