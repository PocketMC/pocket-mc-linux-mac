using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using PocketMC.Core.Services;
using PocketMC.Infrastructure.Services;

namespace PocketMC.Tests.Services
{
    public class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _responseJson;
        private readonly HttpStatusCode _statusCode;

        public MockHttpMessageHandler(string responseJson, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            _responseJson = responseJson;
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseJson)
            });
        }
    }

    public class UpdateServiceTests
    {
        [Fact]
        public async Task CheckForUpdatesAsync_DetectsNewerRelease()
        {
            string mockJson = @"{ ""tag_name"": ""v2.0.0.0"", ""html_url"": ""https://github.com/PocketMC/pocket-mc-linux-mac/releases/tag/v2.0.0.0"" }";
            var handler = new MockHttpMessageHandler(mockJson);
            var httpClient = new HttpClient(handler);

            var service = new UpdateService("1.0.0.0", httpClient);
            var result = await service.CheckForUpdatesAsync();

            Assert.True(result.IsUpdateAvailable);
            Assert.Equal("v2.0.0.0", result.LatestVersionTag);
        }

        [Fact]
        public async Task CheckForUpdatesAsync_ReportsUpToDate_WhenSameVersion()
        {
            string mockJson = @"{ ""tag_name"": ""v1.0.0.0"", ""html_url"": ""https://github.com/PocketMC/pocket-mc-linux-mac/releases/tag/v1.0.0.0"" }";
            var handler = new MockHttpMessageHandler(mockJson);
            var httpClient = new HttpClient(handler);

            var service = new UpdateService("1.0.0.0", httpClient);
            var result = await service.CheckForUpdatesAsync();

            Assert.False(result.IsUpdateAvailable);
        }
    }
}
