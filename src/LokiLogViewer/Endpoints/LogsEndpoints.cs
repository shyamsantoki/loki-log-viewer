using LokiLogViewer.Search;
using LokiLogViewer.Shared.Search;

namespace LokiLogViewer.Endpoints;

public static class LogsEndpoints
{
    public static void MapLogsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/logs");
        group.AddEndpointFilter(HandleLokiFailuresAsync);

        group.MapGet("/search-fields", (IReadOnlyList<SearchFieldDefinition> fields) => Results.Ok(fields));

        // POST (not GET) because the label-discovery context is a small selector, not just an id.
        // Used for the "additional filters" label-name autocomplete - the 4 configured fields
        // already know their own label, so they never need this one.
        group.MapPost("/labels", async (LabelsRequest request, LogSearchService service, CancellationToken ct)
            => Results.Ok(await service.GetLabelsAsync(request, ct)));

        group.MapPost("/labels/{name}/values", async (string name, LabelsRequest request, LogSearchService service, CancellationToken ct)
            => Results.Ok(await service.GetLabelValuesAsync(name, request, ct)));

        group.MapPost("/query", async (LogQueryRequest request, LogSearchService service, CancellationToken ct)
            => Results.Ok(await service.QueryAsync(request, ct)));
    }

    private static async ValueTask<object?> HandleLokiFailuresAsync(
        EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
        catch (HttpRequestException)
        {
            return Results.Problem("Could not reach Loki.", statusCode: StatusCodes.Status502BadGateway);
        }
        catch (TaskCanceledException) when (!context.HttpContext.RequestAborted.IsCancellationRequested)
        {
            return Results.Problem("Loki request timed out.", statusCode: StatusCodes.Status504GatewayTimeout);
        }
    }
}
