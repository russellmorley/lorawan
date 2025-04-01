// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


// upgraded based on this MS beast: https://learn.microsoft.com/en-us/azure/azure-functions/migrate-dotnet-to-isolated-model?tabs=net8

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Linq;
using LoRaTools.Services;
using LoRaTools;
using Microsoft.Azure.Functions.Worker;
using LoraDeviceManager.Startup;

// for configuration, see: https://learn.microsoft.com/en-us/azure/azure-functions/dotnet-isolated-process-guide?tabs=hostbuilder%2Cwindows#register-azure-clients
// for running locally, see: 

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((hostContext, services) =>
    {
        const string serviceValidatesTenant = "ServiceValidatesTenant";

        _ = services
            .AddHttpClient()
            .AddSingleton<ITenantValidationStrategy>(sp =>
                new Sha256TenantValidationStrategy(sp.GetRequiredService<IDeviceRegistryManager>()) { 
                    DoValidation =
                        hostContext.Configuration.GetSection("appSettings")[serviceValidatesTenant] == "true"
                })
            .AddApplicationInsightsTelemetryWorkerService()
            .ConfigureFunctionsApplicationInsights();
        ServiceCollectionDependencies.AddServices(
            services,
            hostContext.Configuration);
    })
    .ConfigureLogging(logging =>
    { // to remove the default app insights logging filter that instructs logger to capture only warnings and more severe logs. See 
        // https://learn.microsoft.com/en-us/azure/azure-functions/dotnet-isolated-process-guide?tabs=hostbuilder%2Cwindows#managing-log-levels
        logging.Services.Configure<LoggerFilterOptions>(options =>
        {
            LoggerFilterRule defaultRule = options.Rules.FirstOrDefault(rule => rule.ProviderName
                == "Microsoft.Extensions.Logging.ApplicationInsights.ApplicationInsightsLoggerProvider");
            if (defaultRule is not null)
            {
                options.Rules.Remove(defaultRule);
            }
        });
        logging.AddConsole();
        logging.SetMinimumLevel(LogLevel.Debug);
    })
    .Build();

    host.Run();

namespace LoraDeviceManagerServices
{
    public static class Globals
    {
        public static readonly string WebJobsStorageClientName = "WebJobsStorage";
    }
}
