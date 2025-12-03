using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;

namespace PrivateCaptcha
{
    public class PrivateCaptchaMiddleware
    {
        private readonly RequestDelegate _next;

        public PrivateCaptchaMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, PrivateCaptchaClient client)
        {
            // Only POST requests with a form content type are checked
            if (context.Request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase) && context.Request.HasFormContentType)
            {
                var solution = context.Request.Form[client.FormField];
                var verified = false;
                try
                {
                    var output = await client.VerifyAsync(new VerifyInput { Solution = solution }, context.RequestAborted);
                    verified = output.OK();
                }
                catch (Exception ex) when (ex is PrivateCaptchaException || ex is ArgumentException)
                {
                    // Exceptions from the client (e.g. after all retries fail, or for an empty solution) are treated as verification failures.
                    verified = false;
                }

                if (!verified)
                {
                    context.Response.StatusCode = (int)client.FailedStatusCode;
                    return;
                }
            }

            await _next(context);
        }
    }

    public static class PrivateCaptchaMiddlewareExtensions
    {
        public static IApplicationBuilder UsePrivateCaptcha(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<PrivateCaptchaMiddleware>();
        }
    }
}
