using LoraDeviceManager.Cache;
using LoRaTools.BasicsStation.Processors;
using LoRaTools.FunctionBundler;
using LoRaWan;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace LoraDeviceManager
{
    public interface ILoraDeviceManager
    {
        public Task<List<IoTHubDeviceInfo>> GetDeviceList(DevAddr devAddr);
        public Task<List<IoTHubDeviceInfo>> GetDeviceList(DevEui devEui, string gatewayId, DevNonce devNonce);
        public Task<string?> GetDevicePrimaryKey(string eui);
        public Task<uint> GetNextFCntDownAsync(DevEui devEui, string gatewayId, uint fCntUp, uint fCntDown);
        public Task AbpFcntCacheReset(DevEui devEui, string gatewayId);
        public Task<string> GetStationCredentials(StationEui eui, ConcentratorCredentialType credentialtype, CancellationToken cancellationToken);
        public Task<FunctionBundlerResult> ExecuteFunctionBundlerAsync(DevEui devEUI, FunctionBundlerRequest request);
        public Task<(long length, Stream contentStream)> GetStationFirmware(StationEui eui, CancellationToken token);
        public Task AddDevice(DevAddr devAddr, DevEui? devEui, string gatewayId, string NwkSKeyString);
        public Task SyncLoraDevAddrCacheWithRegistry();
    }
}
