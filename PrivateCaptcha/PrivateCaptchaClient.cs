using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PrivateCaptcha;

internal static class TimeSpanExtensions
{
    public static TimeSpan Max(this TimeSpan t1, TimeSpan t2)
    {
        return t1 > t2 ? t1 : t2;
    }

    public static TimeSpan Min(this TimeSpan t1, TimeSpan t2)
    {
        return t1 < t2 ? t1 : t2;
    }
}

public class PrivateCaptchaClient : IDisposable
{
    private static readonly Random _random = new Random();

    private readonly string _endpoint;
    private readonly string _apiKey;
    public string FormField { get; }
    public HttpStatusCode FailedStatusCode { get; }
    private readonly HttpClient _httpClient;
    private readonly bool _ownHttpClient;

    public PrivateCaptchaClient(PrivateCaptchaConfiguration configuration)
    {
        if (string.IsNullOrEmpty(configuration.ApiKey))
            throw new ArgumentException("API key cannot be empty", nameof(configuration));

        _apiKey = configuration.ApiKey;
        FormField = configuration.FormField;
        FailedStatusCode = configuration.FailedStatusCode;

        var domain = string.IsNullOrEmpty(configuration.Domain) ? Domains.Global : configuration.Domain;
        if (domain.StartsWith("http"))
        {
            domain = domain.Replace("https://", "").Replace("http://", "");
        }
        domain = domain.Trim('/');

        _endpoint = $"https://{domain}/verify";

        if (configuration.HttpClient != null)
        {
            _httpClient = configuration.HttpClient;
            _ownHttpClient = false;
        }
        else
        {
            _httpClient = new HttpClient();
            _ownHttpClient = true;
        }
    }

    public async Task<VerifyOutput> VerifyAsync(VerifyInput input, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(input.Solution))
            throw new ArgumentException("Solution cannot be empty", nameof(input));

        var maxAttempts = input.MaxAttempts > 0 ? input.MaxAttempts : 5;
        var maxBackoff = TimeSpan.FromSeconds(input.MaxBackoffSeconds > 0 ? input.MaxBackoffSeconds : 20);

        var backoffDelay = TimeSpan.FromMilliseconds(Constants.MinBackoffMillis);
        VerifyOutput response = null;
        Exception lastException = null;

        Debug.WriteLine($"[PrivateCaptcha] Starting verification with max {maxAttempts} attempts, max backoff {maxBackoff.TotalSeconds}s");

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            if (attempt > 0)
            {
                var delay = backoffDelay;
                if (lastException is PrivateCaptchaHttpException httpEx && httpEx.RetryAfterSeconds.HasValue)
                    delay = delay.Max(TimeSpan.FromSeconds(httpEx.RetryAfterSeconds.Value));

                Debug.WriteLine($"[PrivateCaptcha] Attempt {attempt} failed with error {lastException.Message} ");
                await Task.Delay(delay.Min(maxBackoff), cancellationToken);

                backoffDelay = maxBackoff.Min(TimeSpan.FromMilliseconds(backoffDelay.TotalMilliseconds * 2));
            }

            try
            {
                response = await DoVerifyAsync(input.Solution, cancellationToken);
                response.Attempts = attempt + 1;

                Debug.WriteLine($"[PrivateCaptcha] Verification request completed successfully on attempt {attempt + 1}");
                return response;
            }
            catch (HttpRequestException ex)
            {
                lastException = ex;
                continue;
            }
            catch (PrivateCaptchaHttpException ex)
            {
                if (IsRetriableStatusCode(ex.StatusCode))
                {
                    lastException = ex;
                    continue;
                }

                throw;
            }
            catch (TaskCanceledException ex)
            {
                lastException = ex;
                if (ex.InnerException is TimeoutException || !cancellationToken.IsCancellationRequested)
                    continue;

                Debug.WriteLine($"[PrivateCaptcha] Verification cancelled by token.");
                break;
            }
        }

        throw new VerificationFailedException(maxAttempts, lastException);
    }

    private static bool IsRetriableStatusCode(int statusCode)
    {
        switch (statusCode)
        {
            case (int)HttpStatusCode.TooManyRequests:
            case (int)HttpStatusCode.InternalServerError:
            case (int)HttpStatusCode.ServiceUnavailable:
            case (int)HttpStatusCode.BadGateway:
            case (int)HttpStatusCode.GatewayTimeout:
            case (int)HttpStatusCode.RequestTimeout:
                return true;
            default:
                return false;
        }
    }

    private async Task<VerifyOutput> DoVerifyAsync(string solution, CancellationToken cancellationToken)
    {
        using (var request = new HttpRequestMessage(HttpMethod.Post, _endpoint))
        {
            request.Content = new StringContent(solution, Encoding.UTF8, "text/plain");

            request.Headers.Add(Constants.HeaderApiKey, _apiKey);
            request.Headers.Add(Constants.HeaderUserAgent, Constants.UserAgent);

            Debug.WriteLine($"[PrivateCaptcha] Sending HTTP request to {_endpoint}");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var requestId = response.Headers.GetValues(Constants.HeaderTraceId).FirstOrDefault();

            Debug.WriteLine($"[PrivateCaptcha] Received HTTP response. code={(int)response.StatusCode} traceID={requestId}");

            if (!response.IsSuccessStatusCode)
            {
                int? retryAfterSeconds = null;
                if ((response.StatusCode == HttpStatusCode.TooManyRequests) &&
                    response.Headers.RetryAfter != null &&
                    response.Headers.RetryAfter.Delta.HasValue)
                {
                    retryAfterSeconds = (int)response.Headers.RetryAfter.Delta.Value.TotalSeconds;
                    Debug.WriteLine($"[PrivateCaptcha] Rate limited, retry after {retryAfterSeconds}s");
                }
                throw new PrivateCaptchaHttpException((int)response.StatusCode, retryAfterSeconds, requestId);
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<VerifyOutput>(responseBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            if (result == null)
            {
                result = new VerifyOutput { Success = false, Code = VerifyCode.ParseResponse };
            }

            result.RequestId = requestId;
            return result;
        }
    }

    public void Dispose()
    {
        if (_ownHttpClient)
            _httpClient?.Dispose();
    }
}
