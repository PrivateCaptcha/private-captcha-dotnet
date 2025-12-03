namespace PrivateCaptcha;

public class VerifyInput
{
    public VerifyInput()
    {
        Solution = string.Empty;
        Sitekey = string.Empty;
        MaxBackoffSeconds = 20;
        MaxAttempts = 5;
    }

    /// <summary>
    /// CAPTCHA solution obtained from the client-side
    /// </summary>
    public string Solution { get; set; }

    /// <summary>
    /// An optional sitekey to verify solution against
    /// </summary>
    public string Sitekey { get; set; }

    /// <summary>
    /// Maximum backoff time in seconds (default: 20)
    /// </summary>
    public int MaxBackoffSeconds { get; set; }

    /// <summary>
    /// Maximum number of retry attempts (default: 5)
    /// </summary>
    public int MaxAttempts { get; set; }
}
