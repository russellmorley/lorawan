using Azure.Storage.Blobs;
using LoraDeviceManager.ADR;
using LoraDeviceManager.Cache;
using LoraDeviceManager.FunctionBundler;
using LoraDeviceManager.Utils;
using LoRaTools.ADR;
using LoRaTools.AzureBlobStorage;
using LoRaTools.CacheStore;
using LoRaTools.ChannelPublisher;
using LoRaTools.EdgeDeviceGetter;
using LoRaTools.IoTHubImpl;
using LoRaTools.ServiceClient;
using Microsoft.Azure.Devices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Net.Http;

namespace LoraDeviceManager.Startup
{
    public static class ServiceCollectionDependencies
    {
        public const string storageConnectionStringKeyDefault = "StorageConnectionString";
        public const string redisConnectionStringKeyDefault = "RedisConnectionString";
        public const string iotHubConnectionStringKeyDefault = "IoTHubConnectionString";

        public static void AddServices(
            IServiceCollection services, 
            IConfiguration configuration,
            string storageConnectionStringKey = storageConnectionStringKeyDefault,
            string redisConnectionStringKey = redisConnectionStringKeyDefault,
            string iotHubConnectionStringKey = iotHubConnectionStringKeyDefault)
        {
            services.AddSingleton(sp => IoTHubRegistryManager.CreateWithProvider(() =>
                RegistryManager.CreateFromConnectionString(configuration.GetConnectionString(iotHubConnectionStringKey)),
                sp.GetRequiredService<IHttpClientFactory>(),
                sp.GetRequiredService<ILogger<IoTHubRegistryManager>>()));

            var redis = ConnectionMultiplexer.Connect(configuration.GetConnectionString(redisConnectionStringKey));
            var redisCache = redis.GetDatabase();
            var deviceCacheStore = new LoRaDeviceCacheRedisStore(redisCache);
            services.AddSingleton<ICacheStore>(deviceCacheStore);

            services.AddSingleton(sp => AzureBlobStorageManager.CreateWithProvider(() =>
                new BlobServiceClient(configuration.GetConnectionString(storageConnectionStringKey)),
                sp.GetRequiredService<ILogger<AzureBlobStorageManager>>()));

            services.AddSingleton(sp => new LoRaDevAddrCache(
                sp.GetRequiredService<ICacheStore>(),
                sp.GetRequiredService<ILogger<LoRaDevAddrCache>>()
            ));

            services.AddSingleton<FrameCounter>();
            services.AddSingleton<IServiceClient>(new ServiceClientAdapter(ServiceClient.CreateFromConnectionString(configuration.GetConnectionString(iotHubConnectionStringKey))));

            services.AddSingleton<ILoRaADRManager>(sp => new LoRaADRServerManager(
                    new LoRaADRRedisStore(redisCache, sp.GetRequiredService<ILogger<LoRaADRRedisStore>>()),
                    new LoRaADRStrategyProvider(sp.GetRequiredService<ILoggerFactory>()),
                sp.GetRequiredService<FrameCounter>(),
                sp.GetRequiredService<ILogger<LoRaADRServerManager>>()));
            services.AddSingleton<IEdgeDeviceGetter, EdgeDeviceGetter>();
            services.AddSingleton<IChannelPublisher>(sp => new RedisChannelPublisher(redis, sp.GetRequiredService<ILogger<RedisChannelPublisher>>()));

            services.AddSingleton<IFunctionBundlerExecutionItem, NextFCntDownExecutionItem>();
            services.AddSingleton<IFunctionBundlerExecutionItem, DeduplicationExecutionItem>();
            services.AddSingleton<IFunctionBundlerExecutionItem, ADRExecutionItem>();
            services.AddSingleton<IFunctionBundlerExecutionItem, PreferredGatewayExecutionItem>();

            services.AddSingleton<ILoraDeviceManager, LoraDeviceManagerImpl>();
        }
    }
}
