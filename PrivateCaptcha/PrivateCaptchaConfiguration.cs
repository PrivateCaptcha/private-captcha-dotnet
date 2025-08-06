using System.Net;
using System.Net.Http;

namespace PrivateCaptcha;

public class PrivateCaptchaConfiguration
{
    public PrivateCaptchaConfiguration()
    {
        Domain = Domains.Global;
        ApiKey = string.Empty;
        FormField = Constants.DefaultFormField;
        FailedStatusCode = HttpStatusCode.Forbidden;
    }

    /// <summary>
    /// (Optional) Domain name when used with self-hosted version of Private Captcha
    /// </summary>
    public string Domain { get; set; }

    /// <summary>
    /// (Required) API key created in Private Captcha account settings
    /// </summary>
    public string ApiKey { get; set; }

    /// <summary>
    /// (Optional) Custom form field to read puzzle solution from (only used for VerifyRequest helper)
    /// </summary>
    public string FormField { get; set; }

    /// <summary>
    /// (Optional) Custom HttpClient to use with requests
    /// </summary>
    public HttpClient HttpClient { get; set; }

    /// <summary>
    /// (Optional) HTTP status to return for failed verifications (defaults to 403 Forbidden)
    /// </summary>
    public HttpStatusCode FailedStatusCode { get; set; }
}
