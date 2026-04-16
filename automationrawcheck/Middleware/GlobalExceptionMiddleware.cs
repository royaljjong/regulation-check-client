// GlobalExceptionMiddleware.cs - Global Exception Handler
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace AutomationRawCheck.Api.Middleware;

/// <summary>
/// Catches unhandled exceptions and returns ProblemDetails JSON (RFC 7807).
/// </summary>
public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    { _next = next; _logger = logger; }

    public async Task InvokeAsync(HttpContext ctx)
    {
        try { await _next(ctx); }
        catch (OperationCanceledException) when (ctx.RequestAborted.IsCancellationRequested)
        { _logger.LogInformation("Request cancelled."); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception: {Msg}", ex.Message);
            ctx.Response.StatusCode = 500;
            ctx.Response.ContentType = "application/problem+json";
            var prob = new ProblemDetails
            {
                Status = 500,
                Title = "Internal server error.",
                Detail = "A temporary error occurred. Please try again later.",
                Instance = ctx.Request.Path
            };
            var json = JsonSerializer.Serialize(prob, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            await ctx.Response.WriteAsync(json);
        }
    }
}