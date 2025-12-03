using System;
using System.Text.Json.Serialization;

namespace PrivateCaptcha;

public enum VerifyCode
{
    NoError = 0,
    ErrorOther = 1,
    DuplicateSolutions = 2,
    InvalidSolution = 3,
    ParseResponse = 4,
    PuzzleExpired = 5,
    InvalidProperty = 6,
    WrongOwner = 7,
    VerifiedBefore = 8,
    MaintenanceMode = 9,
    TestProperty = 10,
    Integrity = 11
}

public static class VerifyCodeExtensions
{
    public static string GetErrorString(this VerifyCode code)
    {
        switch (code)
        {
            case VerifyCode.NoError:
                return string.Empty;
            case VerifyCode.ErrorOther:
                return "error-other";
            case VerifyCode.DuplicateSolutions:
                return "solution-duplicates";
            case VerifyCode.InvalidSolution:
                return "solution-invalid";
            case VerifyCode.ParseResponse:
                return "solution-bad-format";
            case VerifyCode.PuzzleExpired:
                return "puzzle-expired";
            case VerifyCode.InvalidProperty:
                return "property-invalid";
            case VerifyCode.WrongOwner:
                return "property-owner-mismatch";
            case VerifyCode.VerifiedBefore:
                return "solution-verified-before";
            case VerifyCode.MaintenanceMode:
                return "maintenance-mode";
            case VerifyCode.TestProperty:
                return "property-test";
            case VerifyCode.Integrity:
                return "integrity-error";
            default:
                return "error";
        }
    }
}

public class VerifyOutput
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("code")]
    public VerifyCode Code { get; set; }

    [JsonPropertyName("origin")]
    public string Origin { get; set; }

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; }

    [JsonIgnore]
    public string TraceID { get; internal set; }

    [JsonIgnore]
    public int Attempts { get; internal set; }

    public bool OK()
    {
        return Success && (Code == VerifyCode.NoError);
    }

    public string GetErrorMessage()
    {
        return Code.GetErrorString();
    }
}
