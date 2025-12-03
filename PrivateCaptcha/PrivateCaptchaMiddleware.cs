using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;

namespace PrivateCaptcha
{
    public class PrivateCaptchaMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly Func<HttpContext, VerifyOutput, Task> _onVerificationFailed;

        public PrivateCaptchaMiddleware(RequestDelegate next, Func<HttpContext, VerifyOutput, Task> onVerificationFailed = null)
        {
            _next = next;
            _onVerificationFailed = onVerificationFailed;
        }

        public async Task InvokeAsync(HttpContext context, PrivateCaptchaClient client)
        {
            // Check any request with form content type
            if (context.Request.HasFormContentType)
            {
                var solution = context.Request.Form[client.FormField];
                VerifyOutput output = null;
                var verified = false;
                try
                {
                    output = await client.VerifyAsync(new VerifyInput { Solution = solution }, context.RequestAborted);
                    verified = output.OK();
                }
                catch (Exception ex) when (ex is PrivateCaptchaException || ex is ArgumentException)
                {
                    // Exceptions from the client (e.g. after all retries fail, or for an empty solution) are treated as verification failures.
                    verified = false;
                }

                if (!verified)
                {
                    if (_onVerificationFailed != null && output != null)
                    {
                        await _onVerificationFailed(context, output);
                    }

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

        public static IApplicationBuilder UsePrivateCaptcha(this IApplicationBuilder builder, Func<HttpContext, VerifyOutput, Task> onVerificationFailed)
        {
            return builder.UseMiddleware<PrivateCaptchaMiddleware>(onVerificationFailed);
        }
    }
}
