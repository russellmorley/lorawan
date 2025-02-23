// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoraDeviceManagerServices
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using global::LoraDeviceManagerServices.LoraDeviceManagerServices;
    using global::LoRaTools.Version;
    using LoraDeviceManager;
    using LoRaWan.Tests.Common;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Extensions.Logging.Abstractions;
    using Xunit;

    public class ApiValidationTest
    {
        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("1A88d")]
        [InlineData(".")]
        [InlineData("0000:0000:0000:0000")]
        [InlineData("FFFF:FFFF:FFFF:FFFF")]
        public async Task DevEUI_Validation(string devEUI)
        {
            var dummyExecContext = new ExecutionContext();
            var apiCalls = new Func<HttpRequest, Task<IActionResult>>[]
            {
                (req) => Task.Run(() => new NextFCntDownFunction(null, NullLogger<NextFCntDownFunction>.Instance, new TestNoValidateTenantStrategy()).NextFCntDown(req)),
                (req) => Task.Run(() => new FunctionBundlerFunction(
                    new LoraDeviceManagerImpl(null, null, null, null, null, NullLogger<LoraDeviceManagerImpl>.Instance, null),
                    NullLogger<FunctionBundlerFunction>.Instance, 
                    new TestNoValidateTenantStrategy())
                        .FunctionBundler(req, string.Empty)),
                (req) => new GetDeviceFunction(
                    new LoraDeviceManagerImpl(null, null, null, null, null, NullLogger<LoraDeviceManagerImpl>.Instance, null), 
                    NullLogger<GetDeviceFunction>.Instance,
                    new TestNoValidateTenantStrategy(),
                    null)
                        .GetDevice(req),

            };

            foreach (var apiCall in apiCalls)
            {
                var request = new DefaultHttpContext().Request;
                request.Query = new QueryCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
                    {
                        { ApiVersion.QueryStringParamName, ApiVersion.LatestVersion.Name },
                        { "DevEUI", devEUI }
                    });

                _ = Assert.IsType<BadRequestObjectResult>(await apiCall(request));
            }
        }
    }
}
