using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PrivateCaptcha;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace PrivateCaptcha.Tests
{
    [TestClass]
    public class PrivateCaptchaClientTests
    {
        private const int SolutionsCount = 16;
        private const int SolutionLength = 8;

        private static string _testPuzzle;
        private static readonly SemaphoreSlim _puzzleSemaphore = new SemaphoreSlim(1, 1);
        private static readonly HttpClient _httpClient = new HttpClient();

        private static async Task<string> FetchTestPuzzleAsync()
        {
            if (!string.IsNullOrEmpty(_testPuzzle))
            {
                return _testPuzzle;
            }

            await _puzzleSemaphore.WaitAsync();
            try
            {
                if (!string.IsNullOrEmpty(_testPuzzle))
                {
                    return _testPuzzle;
                }

                var request = new HttpRequestMessage(HttpMethod.Get, "https://api.privatecaptcha.com/puzzle?sitekey=aaaaaaaabbbbccccddddeeeeeeeeeeee");
                request.Headers.Add("Origin", "not.empty");

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                _testPuzzle = await response.Content.ReadAsStringAsync();
                return _testPuzzle;
            }
            finally
            {
                _puzzleSemaphore.Release();
            }
        }

        private static string GetApiKey()
        {
            var apiKey = Environment.GetEnvironmentVariable("PC_API_KEY");
            if (string.IsNullOrEmpty(apiKey))
            {
                Assert.Inconclusive("PC_API_KEY environment variable not set. Skipping integration test.");
            }
            return apiKey;
        }

        [TestMethod]
        public async Task VerifyAsync_WithTestPuzzle_ShouldSucceed()
        {
            var puzzle = await FetchTestPuzzleAsync();
            var client = new PrivateCaptchaClient(new PrivateCaptchaConfiguration
            {
                ApiKey = GetApiKey()
            });

            var emptySolutions = new byte[SolutionsCount * SolutionLength];
            var solutionsStr = Convert.ToBase64String(emptySolutions);
            var payload = $"{solutionsStr}.{puzzle}";

            var output = await client.VerifyAsync(new VerifyInput { Solution = payload });

            Assert.IsTrue(output.Success);
            Assert.IsFalse(output.OK());
            Assert.AreEqual(VerifyCode.TestProperty, output.Code);
        }

        [TestMethod]
        public async Task VerifyAsync_WithInvalidSolution_ShouldFail()
        {
            var puzzle = await FetchTestPuzzleAsync();
            var client = new PrivateCaptchaClient(new PrivateCaptchaConfiguration
            {
                ApiKey = GetApiKey()
            });

            // Use only half the required solutions
            var emptySolutions = new byte[SolutionsCount * SolutionLength / 2];
            var solutionsStr = Convert.ToBase64String(emptySolutions);
            var payload = $"{solutionsStr}.{puzzle}";

            var exception = await Assert.ThrowsExceptionAsync<PrivateCaptchaHttpException>(async () =>
            {
                await client.VerifyAsync(new VerifyInput { Solution = payload });
            });

            Assert.AreEqual(400, exception.StatusCode);
        }

        [TestMethod]
        public async Task VerifyAsync_WithEmptySolution_ShouldThrowArgumentException()
        {
            var client = new PrivateCaptchaClient(new PrivateCaptchaConfiguration
            {
                ApiKey = "test-key"
            });

            await Assert.ThrowsExceptionAsync<ArgumentException>(async () =>
            {
                await client.VerifyAsync(new VerifyInput());
            });
        }

        [TestMethod]
        public async Task VerifyAsync_WithUnresolvableHost_ShouldRetryAndFail()
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };
            var client = new PrivateCaptchaClient(new PrivateCaptchaConfiguration
            {
                ApiKey = "test-key",
                Domain = "does-not-exist.qwerty12345-asdfjkl.net",
                HttpClient = httpClient
            });

            var input = new VerifyInput
            {
                Solution = "asdf",
                MaxBackoffSeconds = 1,
                MaxAttempts = 4
            };

            var exception = await Assert.ThrowsExceptionAsync<VerificationFailedException>(async () =>
            {
                await client.VerifyAsync(input);
            });

            Assert.AreEqual(input.MaxAttempts, exception.Attempts);
        }

        [TestMethod]
        public void Constructor_WithEmptyApiKey_ShouldThrowArgumentException()
        {
            Assert.ThrowsException<ArgumentException>(() =>
            {
                new PrivateCaptchaClient(new PrivateCaptchaConfiguration());
            });
        }

        [TestMethod]
        public async Task Middleware_WithCustomFailedStatusCode_ShouldReturnIt()
        {
            var customStatusCode = HttpStatusCode.InternalServerError;
            var config = new PrivateCaptchaConfiguration
            {
                ApiKey = "test-key",
                Domain = "does-not-exist.qwerty12345-asdfjkl.net",
                FailedStatusCode = customStatusCode,
                HttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(1) }
            };
            var client = new PrivateCaptchaClient(config);

            var nextCalled = false;
            RequestDelegate next = (ctx) =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            };

            var middleware = new PrivateCaptchaMiddleware(next);

            var context = new DefaultHttpContext();
            context.Request.Method = "POST";
            context.Request.ContentType = "application/x-www-form-urlencoded";
            var form = new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                { Constants.DefaultFormField, "invalid-solution" }
            };
            context.Request.Form = new FormCollection(form);

            await middleware.InvokeAsync(context, client);

            Assert.IsFalse(nextCalled, "Next delegate should not be called on failure.");
            Assert.AreEqual((int)customStatusCode, context.Response.StatusCode);
        }

        [TestMethod]
        public async Task Middleware_WithTestPuzzle_ShouldRejectTestProperty()
        {
            var puzzle = await FetchTestPuzzleAsync();
            var customFieldName = "my-custom-captcha-field";

            var config = new PrivateCaptchaConfiguration
            {
                ApiKey = GetApiKey(),
                FormField = customFieldName,
            };
            var client = new PrivateCaptchaClient(config);

            var nextCalled = false;
            RequestDelegate next = (ctx) =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            };

            VerifyOutput capturedOutput = null;
            Func<HttpContext, VerifyOutput, Task> onVerificationFailed = (ctx, output) =>
            {
                capturedOutput = output;
                return Task.CompletedTask;
            };

            var middleware = new PrivateCaptchaMiddleware(next, onVerificationFailed);

            var emptySolutions = new byte[SolutionsCount * SolutionLength];
            var solutionsStr = Convert.ToBase64String(emptySolutions);
            var payload = $"{solutionsStr}.{puzzle}";

            // Test that middleware rejects test puzzle (TestProperty code)
            var context = new DefaultHttpContext();
            context.Request.Method = "POST";
            context.Request.ContentType = "application/x-www-form-urlencoded";
            var form = new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                { customFieldName, payload }
            };
            context.Request.Form = new FormCollection(form);

            await middleware.InvokeAsync(context, client);

            Assert.IsFalse(nextCalled, "Next delegate should not be called when verification returns TestProperty.");
            Assert.AreEqual((int)HttpStatusCode.Forbidden, context.Response.StatusCode);

            // Verify the error code is TestProperty via the failure handler
            Assert.IsNotNull(capturedOutput, "Failure handler should have been called.");
            Assert.IsFalse(capturedOutput.OK(), "Output.OK() should return false for TestProperty.");
            Assert.AreEqual(VerifyCode.TestProperty, capturedOutput.Code);
            Assert.AreEqual("property-test", capturedOutput.GetErrorMessage());
        }

        [TestMethod]
        public async Task Middleware_WithDefaultFormField_ShouldRejectTestProperty()
        {
            var puzzle = await FetchTestPuzzleAsync();

            var config = new PrivateCaptchaConfiguration
            {
                ApiKey = GetApiKey(),
            };
            var client = new PrivateCaptchaClient(config);

            var nextCalled = false;
            RequestDelegate next = (ctx) =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            };

            VerifyOutput capturedOutput = null;
            Func<HttpContext, VerifyOutput, Task> onVerificationFailed = (ctx, output) =>
            {
                capturedOutput = output;
                return Task.CompletedTask;
            };

            var middleware = new PrivateCaptchaMiddleware(next, onVerificationFailed);

            var emptySolutions = new byte[SolutionsCount * SolutionLength];
            var solutionsStr = Convert.ToBase64String(emptySolutions);
            var payload = $"{solutionsStr}.{puzzle}";

            // Test that middleware rejects test puzzle with default field name
            var context = new DefaultHttpContext();
            context.Request.Method = "POST";
            context.Request.ContentType = "application/x-www-form-urlencoded";
            var form = new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                { Constants.DefaultFormField, payload }
            };
            context.Request.Form = new FormCollection(form);

            await middleware.InvokeAsync(context, client);

            Assert.IsFalse(nextCalled, "Next delegate should not be called when verification returns TestProperty.");
            Assert.AreEqual((int)HttpStatusCode.Forbidden, context.Response.StatusCode);

            // Verify the error code is TestProperty via the failure handler
            Assert.IsNotNull(capturedOutput, "Failure handler should have been called.");
            Assert.IsFalse(capturedOutput.OK(), "Output.OK() should return false for TestProperty.");
            Assert.AreEqual(VerifyCode.TestProperty, capturedOutput.Code);
            Assert.AreEqual("property-test", capturedOutput.GetErrorMessage());
        }
    }
}
