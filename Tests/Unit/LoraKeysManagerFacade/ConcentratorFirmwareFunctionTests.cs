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
    using global::LoRaTools.IoTHubImpl;
    using global::LoRaTools.Version;
    using LoraDeviceManager;
    using LoraDeviceManager.Services;
    using LoRaWan.Tests.Common;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.Extensions.Primitives;
    using Moq;
    using Xunit;

    public class ConcentratorFirmwareFunctionTests
    {
        private const string BlobContent = "testcontents";

        private readonly StationEui testStationEui = StationEui.Parse("11-11-11-11-11-11-11-11");

        private readonly Mock<IDeviceRegistryManager> registryManager;
        private readonly Mock<BlobClient> blobClient;
        private readonly FetchConcentratorFirmwareFunction fetchConcentratorFirwareFunction;
        private readonly LoraDeviceManagerServices loraDeviceManagerServices;

        public ConcentratorFirmwareFunctionTests()
        {
            this.blobClient = new Mock<BlobClient>();

            var blobContainerClient = new Mock<BlobContainerClient>();
            var blobClientResponseMock = new Mock<Response>();
            blobContainerClient.Setup(m => m.GetBlobClient(It.IsAny<string>()))
                               .Returns(Response.FromValue(this.blobClient.Object, blobClientResponseMock.Object));

            var blobServiceClient = new Mock<BlobServiceClient>();
            var blobContainerClientResponseMock = new Mock<Response>();
            blobServiceClient.Setup(m => m.GetBlobContainerClient(It.IsAny<string>()))
                             .Returns(Response.FromValue(blobContainerClient.Object, blobContainerClientResponseMock.Object));

            this.registryManager = new Mock<IDeviceRegistryManager>();


            var blobStorageManager = AzureBlobStorageManager.CreateWithProvider(() =>
                blobServiceClient.Object,
                NullLogger<AzureBlobStorageManager>.Instance);

            var loraDeviceManager = new LoraDeviceManagerImpl(this.registryManager.Object, null, blobStorageManager, null, null, NullLogger<LoraDeviceManagerImpl>.Instance, null);
            this.fetchConcentratorFirwareFunction = new FetchConcentratorFirmwareFunction(
                loraDeviceManager,
                new TestNoValidateTenantStrategy(), 
                NullLogger<FetchConcentratorFirmwareFunction>.Instance);
            this.loraDeviceManagerServices = new LoraDeviceManagerServices(loraDeviceManager, NullLogger<LoraDeviceManagerServices>.Instance, null);

        }

        [Fact]
        public async Task RunFetchConcentratorFirmware_Succeeds()
        {
            var httpRequest = new DefaultHttpContext().Request;
            var queryCollection = new QueryCollection(new Dictionary<string, StringValues>()
            {
                { ApiVersion.QueryStringParamName, ApiVersion.LatestVersion.Name },
                { "StationEui", new StringValues(this.testStationEui.ToString()) }
            });
            httpRequest.Query = queryCollection;

            var twin = new Twin();
            twin.Properties.Desired = new TwinCollection(JsonUtil.Strictify(/*lang=json*/ @"{'cups': {
                'package': '1.0.1',
                'fwUrl': 'https://storage.blob.core.windows.net/container/blob',
                'fwKeyChecksum': 123456,
                'fwSignature': '123'
            }}"));
            this.registryManager.Setup(m => m.GetTwinAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                                .Returns(Task.FromResult<IDeviceTwin>(new IoTHubDeviceTwin(twin)));

            var blobBytes = Encoding.UTF8.GetBytes(BlobContent);
            using var blobContentStream = new MemoryStream(blobBytes);
            using var streamingResult = BlobsModelFactory.BlobDownloadStreamingResult(blobContentStream);
            this.blobClient.Setup(m => m.DownloadStreamingAsync(It.IsAny<BlobDownloadOptions>(), It.IsAny<CancellationToken>()))
                           .Returns(Task.FromResult(Response.FromValue(streamingResult, new Mock<Response>().Object)));

            var blobProperties = BlobsModelFactory.BlobProperties(contentLength: BlobContent.Length);

            this.blobClient.Setup(m => m.GetPropertiesAsync(null, It.IsAny<CancellationToken>()))
                           .Returns(Task.FromResult(Response.FromValue(blobProperties, new Mock<Response>().Object)));

            var actual = await this.fetchConcentratorFirwareFunction.FetchConcentratorFirmware(httpRequest, CancellationToken.None);

            Assert.NotNull(actual);
            var result = Assert.IsType<FileStreamWithContentLengthResult>(actual);

            //result.FileStream.Position = 0;
            using var reader = new StreamReader(result.FileStream);
            var fileContents = await reader.ReadToEndAsync();
            Assert.Equal(BlobContent, fileContents);

            var httpContentDirect = await loraDeviceManagerServices.FetchStationFirmwareAsync(testStationEui, default);
            Assert.NotNull(httpContentDirect);
            var streamCopy = new MemoryStream();
            await httpContentDirect.CopyToAsync(streamCopy);
            var contents = Encoding.UTF8.GetString(streamCopy.ToArray());
            Assert.Equal(BlobContent, contents);
            Assert.True(httpContentDirect.Headers.ContentLength > 0);

        }

        [Fact]
        public async Task RunFetchConcentratorFirmware_Returns_NotFound_ForMissingTwin()
        {
            var httpRequest = new DefaultHttpContext().Request;
            var queryCollection = new QueryCollection(new Dictionary<string, StringValues>()
            {
                { ApiVersion.QueryStringParamName, ApiVersion.LatestVersion.Name },
                { "StationEui", new StringValues(this.testStationEui.ToString()) }
            });
            httpRequest.Query = queryCollection;

            var twin = new Twin();
            twin.Properties.Desired = new TwinCollection(JsonUtil.Strictify(/*lang=json*/ @"{'cups': {
                'package': '1.0.1',
                'fwUrl': 'https://storage.blob.core.windows.net/container/blob',
                'fwKeyChecksum': 123456,
                'fwSignature': '123'
            }}"));
            this.registryManager.Setup(m => m.GetTwinAsync("AnotherTwin", It.IsAny<CancellationToken>()))
                                .Returns(Task.FromResult<IDeviceTwin>(new IoTHubDeviceTwin(twin)));

            var result = await this.fetchConcentratorFirwareFunction.FetchConcentratorFirmware(httpRequest, CancellationToken.None);

            Assert.NotNull(result);
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task RunFetchConcentratorFirmware_Returns_BadRequest_ForMissingQueryParams()
        {
            var httpRequest = new DefaultHttpContext().Request;
            var queryDictionary = new Dictionary<string, StringValues>()
            {
                { ApiVersion.QueryStringParamName, ApiVersion.LatestVersion.Name }
            };
            var queryCollection = new QueryCollection(queryDictionary);
            httpRequest.Query = queryCollection;

            var result = await this.fetchConcentratorFirwareFunction.FetchConcentratorFirmware(httpRequest, CancellationToken.None);

            Assert.NotNull(result);
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task RunFetchConcentratorFirmware_Returns_InternalServerError_ForTwinMissingCups()
        {
            var httpRequest = new DefaultHttpContext().Request;
            var queryCollection = new QueryCollection(new Dictionary<string, StringValues>()
            {
                { ApiVersion.QueryStringParamName, ApiVersion.LatestVersion.Name },
                { "StationEui", new StringValues(this.testStationEui.ToString()) }
            });
            httpRequest.Query = queryCollection;

            var twin = new Twin();
            twin.Properties.Desired = new TwinCollection(JsonUtil.Strictify(/*lang=json*/ @"{'a': 'b'}"));
            this.registryManager.Setup(m => m.GetTwinAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                                .Returns(Task.FromResult<IDeviceTwin>(new IoTHubDeviceTwin(twin)));

            var actual = await this.fetchConcentratorFirwareFunction.FetchConcentratorFirmware(httpRequest, CancellationToken.None);

            var result = Assert.IsType<ObjectResult>(actual);
            Assert.Equal(500, result.StatusCode);
            Assert.Equal("Failed to parse firmware upgrade url from the 'cups' desired property.", result.Value);
        }

        [Fact]
        public async Task RunFetchConcentratorFirmware_Returns_InternalServerError_ForTwinMissingFwUrl()
        {
            var httpRequest = new DefaultHttpContext().Request;
            var queryCollection = new QueryCollection(new Dictionary<string, StringValues>()
            {
                { ApiVersion.QueryStringParamName, ApiVersion.LatestVersion.Name },
                { "StationEui", new StringValues(this.testStationEui.ToString()) }
            });
            httpRequest.Query = queryCollection;

            var twin = new Twin();
            twin.Properties.Desired = new TwinCollection(JsonUtil.Strictify(/*lang=json*/ @"{'cups': {
                'package': '1.0.1',
                'fwKeyChecksum': 123456,
                'fwSignature': '123'
            }}"));
            this.registryManager.Setup(m => m.GetTwinAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                                .Returns(Task.FromResult<IDeviceTwin>(new IoTHubDeviceTwin(twin)));

            var actual = await this.fetchConcentratorFirwareFunction.FetchConcentratorFirmware(httpRequest, CancellationToken.None);

            var result = Assert.IsType<ObjectResult>(actual);
            Assert.Equal(500, result.StatusCode);
            Assert.Equal("Failed to parse firmware upgrade url from the 'cups' desired property.", result.Value);
        }

        [Fact]
        public async Task RunFetchConcentratorFirmware_Returns_InternalServerError_WhenDownloadFails()
        {
            var httpRequest = new DefaultHttpContext().Request;
            var queryCollection = new QueryCollection(new Dictionary<string, StringValues>()
            {
                { ApiVersion.QueryStringParamName, ApiVersion.LatestVersion.Name },
                { "StationEui", new StringValues(this.testStationEui.ToString()) }
            });
            httpRequest.Query = queryCollection;

            var twin = new Twin();
            twin.Properties.Desired = new TwinCollection(JsonUtil.Strictify(/*lang=json*/ @"{'cups': {
                'package': '1.0.1',
                'fwUrl': 'https://storage.blob.core.windows.net/container/blob',
                'fwKeyChecksum': 123456,
                'fwSignature': '123'
            }}"));
            this.registryManager.Setup(m => m.GetTwinAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                                .Returns(Task.FromResult<IDeviceTwin>(new IoTHubDeviceTwin(twin)));

            this.blobClient.Setup(m => m.DownloadStreamingAsync(It.IsAny<BlobDownloadOptions>(),
                                                                It.IsAny<CancellationToken>()))
                           .ThrowsAsync(new RequestFailedException("download failed"));

            var actual = await this.fetchConcentratorFirwareFunction.FetchConcentratorFirmware(httpRequest, CancellationToken.None);

            Assert.NotNull(actual);
            var result = Assert.IsType<ObjectResult>(actual);
            Assert.Equal(500, result.StatusCode);
            Assert.Equal("Failed to download firmware", result.Value);
        }
    }
}
