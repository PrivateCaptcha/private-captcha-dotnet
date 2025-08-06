namespace PrivateCaptcha;

internal static class Constants
{
    public const string DefaultFormField = "private-captcha-solution";
    public const string Version = "0.0.1";
    public const string UserAgent = "private-captcha-dotnet/" + Version;
    public const int MinBackoffMillis = 500;

    public const string HeaderApiKey = "X-Api-Key";
    public const string HeaderTraceId = "X-Trace-ID";
    public const string HeaderUserAgent = "User-Agent";
}
