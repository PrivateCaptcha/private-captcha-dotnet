using System;

namespace PrivateCaptcha;

public class PrivateCaptchaException : Exception
{
    public PrivateCaptchaException(string message) : base(message) { }
    public PrivateCaptchaException(string message, Exception innerException) : base(message, innerException) { }
}

public class PrivateCaptchaHttpException : PrivateCaptchaException
{
    public int StatusCode { get; }
    public int? RetryAfterSeconds { get; }

    public PrivateCaptchaHttpException(int statusCode, int? retryAfterSeconds = null)
        : base($"HTTP error {statusCode}")
    {
        StatusCode = statusCode;
        RetryAfterSeconds = retryAfterSeconds;
    }
}

public class VerificationFailedException : PrivateCaptchaException
{
    public int Attempts { get; }

    public VerificationFailedException(int attempts, Exception innerException)
        : base($"Captcha verification failed after {attempts} attempts", innerException)
    {
        Attempts = attempts;
    }
}

