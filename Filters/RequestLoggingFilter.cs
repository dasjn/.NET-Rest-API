using Microsoft.AspNetCore.Mvc.Filters;
using System.Diagnostics;

namespace IA.WebAPI.Filters
{
    public class RequestLoggingFilter : IActionFilter
    {
        private readonly ILogger<RequestLoggingFilter> _logger;
        private readonly Stopwatch _stopwatch = new();

        public RequestLoggingFilter(ILogger<RequestLoggingFilter> logger)
        {
            _logger = logger;
        }

        public void OnActionExecuting(ActionExecutingContext context)
        {
            _stopwatch.Start();

            var request = context.HttpContext.Request;
            var user = context.HttpContext.User.Identity?.Name ?? "anonymous";
            var controller = context.ActionDescriptor.RouteValues["controller"];
            var action = context.ActionDescriptor.RouteValues["action"];

            _logger.LogInformation(
                "Request: {Method} {Path} | Controller: {Controller} | Action: {Action} | User: {User}",
                request.Method, request.Path, controller, action, user);
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
            _stopwatch.Stop();

            var statusCode = context.HttpContext.Response.StatusCode;
            var elapsed = _stopwatch.ElapsedMilliseconds;
            var controller = context.ActionDescriptor.RouteValues["controller"];
            var action = context.ActionDescriptor.RouteValues["action"];

            if (context.Exception != null)
            {
                _logger.LogError(
                    context.Exception,
                    "Response: {StatusCode} | Controller: {Controller} | Action: {Action} | Elapsed: {Elapsed}ms | Error: {ErrorMessage}",
                    statusCode, controller, action, elapsed, context.Exception.Message);
            }
            else
            {
                _logger.LogInformation(
                    "Response: {StatusCode} | Controller: {Controller} | Action: {Action} | Elapsed: {Elapsed}ms",
                    statusCode, controller, action, elapsed);
            }
        }
    }
}