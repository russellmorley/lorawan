// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoraDeviceManagerServices
{
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure;
    using Azure.Storage.Blobs;
    using Azure.Storage.Blobs.Models;
    using global::LoraDeviceManagerServices.LoraDeviceManagerServices;
    using global::LoRaTools;
    using global::LoRaTools.AzureBlobStorage;
    using global::LoRaTools.BasicsStation.Processors;
    using global::LoRaTools.IoTHubImpl;
    using LoraDeviceManager;
    using LoRaWan.Tests.Common;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.Extensions.Primitives;
    using Moq;
    using Xunit;

    public class ConcentratorCredentialTests
    {
        private readonly Mock<IDeviceRegistryManager> registryManager;
        //private readonly Mock<IAzureClientFactory<BlobServiceClient>> azureClientFactory;
        private readonly Mock<BlobServiceClient> blobServiceClient;
        private readonly IBlobStorageManager blobStorageManager;
        private readonly FetchConcentratorCredentialsFunction fetchConcentratorCredentialsFunction;
        private readonly StationEui stationEui = StationEui.Parse("001122FFFEAABBCC");
        private const string RawStringContent = "hello";
        private const string Base64EncodedString = "aGVsbG8=";

        public ConcentratorCredentialTests()
        {
            registryManager = new Mock<IDeviceRegistryManager>();
            //this.azureClientFactory = new Mock<IAzureClientFactory<BlobServiceClient>>();
            blobServiceClient = new Mock<BlobServiceClient>();

            blobStorageManager = AzureBlobStorageManager.CreateWithProvider(() =>
                blobServiceClient.Object,
                NullLogger<AzureBlobStorageManager>.Instance);

            this.fetchConcentratorCredentialsFunction = new FetchConcentratorCredentialsFunction(
                new LoraDeviceManagerImpl(
                    registryManager.Object, 
                    null, 
                    blobStorageManager, 
                    null, 
                    null,
                    NullLogger<LoraDeviceManagerImpl>.Instance,
                    null),
                new TestNoValidateTenantStrategy(),
                NullLogger<FetchConcentratorCredentialsFunction>.Instance);

            //this.fetchConcentratorCredentialsFunction = new FetchConcentratorCredentialsFunction(registryManager.Object, azureClientFactory.Object, NullLogger<FetchConcentratorCredentialsFunction>.Instance);
        }

        [Fact]
        public async Task GetBase64EncodedBlobAsync_Succeeds()
        {
            var blobBytes = Encoding.UTF8.GetBytes(RawStringContent);
            using var blobStream = new MemoryStream(blobBytes);
            SetupBlobMock(blobStream);

            var result = await blobStorageManager.GetBase64EncodedBlobAsync("https://storage.blob.core.windows.net/container/blobname", CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(Base64EncodedString, result);
        }

        [Theory]
        [InlineData(ConcentratorCredentialType.Lns)]
        [InlineData(ConcentratorCredentialType.Cups)]
        public async Task RunFetchConcentratorCredentials_Succeeds(ConcentratorCredentialType credentialType)
        {
            var blobBytes = Encoding.UTF8.GetBytes(RawStringContent);
            using var blobStream = new MemoryStream(blobBytes);
            SetupBlobMock(blobStream);

            // http request
            var httpRequest = SetupHttpRequest(credentialType);

            // twin mock
            var twin = SetupDeviceTwin();
            this.registryManager.Setup(m => m.GetTwinAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                                .Returns(Task.FromResult(twin));

            var result = await this.fetchConcentratorCredentialsFunction.RunFetchConcentratorCredentials(httpRequest.Object, CancellationToken.None);

            Assert.NotNull(result);
            Assert.IsType<OkObjectResult>(result);
        }

        [Fact]
        public async Task RunFetchConcentratorCredentials_Returns_NotFound_ForMissingTwin()
        {
            // http request
            var httpRequest = SetupHttpRequest();

            // twin mock
            var twin = SetupDeviceTwin();
            this.registryManager.Setup(m => m.GetTwinAsync("AnotherTwin", It.IsAny<CancellationToken>()))
                                .Returns(Task.FromResult(twin));

            var result = await this.fetchConcentratorCredentialsFunction.RunFetchConcentratorCredentials(httpRequest.Object, CancellationToken.None);

            Assert.NotNull(result);
            Assert.IsType<NotFoundResult>(result);
        }

        [Theory]
        [InlineData(true, false, false)]
        [InlineData(true, true, true)]
        [InlineData(false, true, false)]
        public async Task RunFetchConcentratorCredentials_Returns_BadRequest_ForMissingQueryParams(bool stationEuiAvailable, bool credentialTypeAvailable, bool wrongCredentialType)
        {
            // http request
            var httpRequest = new Mock<HttpRequest>();
            var queryDictionary = new Dictionary<string, StringValues>();
            if (stationEuiAvailable)
            {
                queryDictionary.Add("StationEui", this.stationEui.ToString());
            }
            if (credentialTypeAvailable)
            {
                queryDictionary.Add("CredentialType", wrongCredentialType ? "wrong" : ConcentratorCredentialType.Cups.ToString());
            }
            var queryCollection = new QueryCollection(queryDictionary);
            httpRequest.SetupGet(x => x.Query).Returns(queryCollection);

            var result = await this.fetchConcentratorCredentialsFunction.RunFetchConcentratorCredentials(httpRequest.Object, CancellationToken.None);

            Assert.NotNull(result);
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task RunFetchConcentratorCredentials_Returns_InternalServerError_ForMissingCupsProperty()
        {
            var blobBytes = Encoding.UTF8.GetBytes(RawStringContent);
            using var blobStream = new MemoryStream(blobBytes);
            SetupBlobMock(blobStream);

            // http request
            var httpRequest = SetupHttpRequest();

            // twin mock
            var twin = new Twin();
            twin.Properties.Desired = new TwinCollection(JsonUtil.Strictify(@"{'key': 'value'}"));
            this.registryManager.Setup(m => m.GetTwinAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                                .ReturnsAsync(new IoTHubDeviceTwin(twin));

            var actual = await this.fetchConcentratorCredentialsFunction.RunFetchConcentratorCredentials(httpRequest.Object, CancellationToken.None);

            var result = Assert.IsType<ObjectResult>(actual);
            Assert.Equal(500, result.StatusCode);
            Assert.Equal("failed to read (Parameter 'cups')", result.Value);
        }

        private void SetupBlobMock(MemoryStream blobStream)
        {
            var blobClient = new Mock<BlobClient>();
            blobClient.Setup(m => m.OpenReadAsync(It.IsAny<BlobOpenReadOptions>(), It.IsAny<CancellationToken>()))
                      .Returns(Task.FromResult(blobStream as Stream));

            var blobContainerClient = new Mock<BlobContainerClient>();
            var blobClientResponseMock = new Mock<Response>();
            blobContainerClient.Setup(m => m.GetBlobClient(It.IsAny<string>()))
                               .Returns(Response.FromValue(blobClient.Object, blobClientResponseMock.Object));

            var blobContainerClientResponseMock = new Mock<Response>();
            blobServiceClient.Setup(m => m.GetBlobContainerClient(It.IsAny<string>()))
                             .Returns(Response.FromValue(blobContainerClient.Object, blobContainerClientResponseMock.Object));
        }

        private Mock<HttpRequest> SetupHttpRequest(ConcentratorCredentialType credentialType = ConcentratorCredentialType.Cups)
        {
            var httpRequest = new Mock<HttpRequest>();
            var queryCollection = new QueryCollection(new Dictionary<string, StringValues>()
            {
                { "StationEui", new StringValues(this.stationEui.ToString()) },
                { "CredentialType", credentialType.ToString() }
            });

            httpRequest.SetupGet(x => x.Query).Returns(queryCollection);
            return httpRequest;
        }

        private static IDeviceTwin SetupDeviceTwin()
        {
            var twin = new Twin();
            twin.Properties.Desired = new TwinCollection(JsonUtil.Strictify(@"{'cups': {
                'cupsUri': 'https://localhost:5002',
                'tcUri': 'wss://localhost:5001',
                'cupsCredCrc': 1234,
                'tcCredCrc': 5678,
                'cupsCredentialUrl': 'https://storage.blob.core.windows.net/container/blob',
                'tcCredentialUrl': 'https://storage.blob.core.windows.net/container/blob'
            }}"));

            return new IoTHubDeviceTwin(twin);
        }
    }
}
