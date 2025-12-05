# Private Captcha for .NET

[![CI](https://github.com/PrivateCaptcha/private-captcha-dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/PrivateCaptcha/private-captcha-dotnet/actions/workflows/ci.yml)

Official .NET SDK for server-side verification of Private Captcha solutions.

<mark>Please check the [official documentation](https://docs.privatecaptcha.com/docs/integrations/dotnet/) for the in-depth and up-to-date information.</mark>

## Quick start

- Install the package via the .NET CLI or the NuGet Package Manager Console.
   ```bash
   dotnet add package PrivateCaptcha
   ```
- To verify a CAPTCHA solution, instantiate `PrivateCaptchaClient` with your configuration and call `VerifyAsync`.
	```csharp
	var config = new PrivateCaptchaConfiguration
	{
	    ApiKey = "YOUR_API_KEY"
	};
	var captchaClient = new PrivateCaptchaClient(config);
	
	var result = await captchaClient.VerifyAsync(new VerifyInput { Solution = captchaSolution });
	if (result.OK())
	{
	    Console.WriteLine("Captcha verification succeeded!");
	}
	else
	{
	    // Verification failed, you can check the reason.
	    Console.WriteLine($"Verification failed: {result.GetErrorMessage()}");
	}
	```
- To use ASP.NET Core Middleware, add `app.UsePrivateCaptcha()` to your request pipeline.

## Requirements

- .NET 6+
- No external dependencies (uses only standard library)

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Support

For issues with this .NET/C# client, please open an issue on GitHub.
For Private Captcha service questions, visit [privatecaptcha.com](https://privatecaptcha.com).
