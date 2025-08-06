# Private Captcha for .NET

Official .NET SDK for server-side verification of Private Captcha solutions.

## Installation

You can install the package via the .NET CLI or the NuGet Package Manager Console.

**.NET CLI**
```bash
dotnet add package PrivateCaptcha
```

**Package Manager Console**
```powershell
Install-Package PrivateCaptcha
```

## Usage

### Basic Verification

To verify a CAPTCHA solution, instantiate `PrivateCaptchaClient` with your configuration and call `VerifyAsync`.

```csharp
var config = new PrivateCaptchaConfiguration
{
    ApiKey = "YOUR_API_KEY"
};
var captchaClient = new PrivateCaptchaClient(config);

var result = await captchaClient.VerifyAsync(new VerifyInput { Solution = captchaSolution });
if (result.Success)
{
    Console.WriteLine("Captcha verification succeeded!");
}
else
{
    // Verification failed, you can check the reason.
    Console.WriteLine($"Verification failed: {result.GetErrorMessage()}");
}
```

### ASP.NET Core Middleware

For web applications, the most convenient way to protect your endpoints is by using the provided middleware. It automatically intercepts POST requests with form data and verifies the CAPTCHA solution.

**1. Register `PrivateCaptchaClient` for Dependency Injection**

In your `Program.cs` (for .NET 6+ minimal APIs) or `Startup.cs`, register the `PrivateCaptchaClient`.

```csharp
// Program.cs
using PrivateCaptcha;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(sp =>
{
    var config = new PrivateCaptchaConfiguration { ApiKey = "YOUR_API_KEY" };
    return new PrivateCaptchaClient(config);
});

```

**2. Add the Middleware to the Request Pipeline**

Add `app.UsePrivateCaptcha()` to your request pipeline. It should be placed before the endpoints you want to protect.

```csharp
var app = builder.Build();

// ... other middleware

// Use the PrivateCaptcha middleware. This will automatically check POST requests
// with a form content type for a valid captcha solution.
app.UsePrivateCaptcha();

// This endpoint will now be protected by the CAPTCHA middleware.
app.MapPost("/register", () => "Registration successful!");

app.Run();
```

If verification fails, the middleware will short-circuit the request and return a `403 Forbidden` status code by default (can be changed in the configuration).

### Configuration

You can customize the client's behavior by passing a `PrivateCaptchaConfiguration` object to the constructor.

| Property           | Description                                                                                             | Default Value                  |
| ------------------ | ------------------------------------------------------------------------------------------------------- | ------------------------------ |
| `ApiKey`           | **(Required)** Your API key from the Private Captcha dashboard.                                         | `string.Empty`                 |
| `Domain`           | The API domain to use (use `Domains.EU` or an override in case of self-hosting)                                   | `Domains.Global`               |
| `FormField`        | The name of the form field containing the CAPTCHA solution. Used by the middleware.                     | `"private-captcha-solution"`   |
| `FailedStatusCode` | The `HttpStatusCode` to return when middleware verification fails.                                      | `HttpStatusCode.Forbidden` (403) |
| `HttpClient`       | An optional `HttpClient` instance. If not provided, a new one will be created and managed by the client. | `null`                         |

#### Non-standard backend domains

For EU isolation you can use built-in constant `Domains.EU` or, in case of self-hosting, custom override:

```csharp
var config = new PrivateCaptchaConfiguration
{
    ApiKey = "YOUR_API_KEY",
    Domain = Domains.EU
};
var client = new PrivateCaptchaClient(config);
```

#### Retry configuration

When verifying puzzle solutions, you can also specify some retry and backoff options.

```csharp
var input = new VerifyInput
{
	Solution = "captcha-solution",
	MaxBackoffSeconds = 30,
	MaxAttempts = 10
};
```

### Error Handling

- **`VerifyOutput`**: The `VerifyAsync` method returns a `VerifyOutput` object. Check the `Success` property to see if verification passed. If `Success` is `false`, the `Code` property and `GetErrorMessage()` method provide more details.
- **`VerificationFailedException`**: Thrown when the client cannot get a definitive success/fail response from the API after all retry attempts. This typically indicates a network issue.
- **`ArgumentException`**: Thrown by `VerifyAsync` if the provided `VerifyInput.Solution` is null or empty.

## Requirements

- .NET 6+
- No external dependencies (uses only standard library)

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Support

For issues with this .NET/C# client, please open an issue on GitHub.
For Private Captcha service questions, visit [privatecaptcha.com](https://privatecaptcha.com).
