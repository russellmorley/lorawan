using LoraDeviceManager.Cache;
using LoRaTools.CacheStore;
using LoRaWan;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LoraDeviceManager.Utils
{
    public class FrameCounter
    {
        private readonly ICacheStore cacheStore;

        public FrameCounter(ICacheStore cacheStore)
        {
            this.cacheStore = cacheStore;
        }

        public async Task<uint> GetNextFCntDownAsync(DevEui devEui, string gatewayId, uint fCntUp, uint fCntDown)
        {
            uint newFCntDown = 0;
            using (var loraDeviceCache = new LoRaDeviceCache(cacheStore, devEui, gatewayId))
            {
                if (await loraDeviceCache.TryToLockAsync())
                {
                    if (loraDeviceCache.TryGetInfo(out var serverStateForDeviceInfo))
                    {
                        newFCntDown = ProcessExistingDeviceInfo(loraDeviceCache, serverStateForDeviceInfo, gatewayId, fCntUp, fCntDown);
                    }
                    else
                    {
                        newFCntDown = fCntDown + 1;
                        var state = loraDeviceCache.Initialize(fCntUp, newFCntDown);
                    }
                }
            }

            try
            {
                _ = (ushort)newFCntDown;
                return newFCntDown;
            }
            catch (InvalidCastException)
            {
                return 0;
            }
        }
        internal static uint ProcessExistingDeviceInfo(LoRaDeviceCache deviceCache, DeviceCacheInfo cachedDeviceState, string gatewayId, uint clientFCntUp, uint clientFCntDown)
        {
            uint newFCntDown = 0;

            if (cachedDeviceState != null)
            {
                // we have a state in the cache matching this device and now we own the lock
                if (clientFCntUp > cachedDeviceState.FCntUp)
                {
                    // it is a new message coming up by the first gateway
                    if (clientFCntDown >= cachedDeviceState.FCntDown)
                        newFCntDown = clientFCntDown + 1;
                    else
                        newFCntDown = cachedDeviceState.FCntDown + 1;

                    cachedDeviceState.FCntUp = clientFCntUp;
                    cachedDeviceState.FCntDown = newFCntDown;
                    cachedDeviceState.GatewayId = gatewayId;

                    _ = deviceCache.StoreInfo(cachedDeviceState);
                }
                else if (clientFCntUp == cachedDeviceState.FCntUp && gatewayId == cachedDeviceState.GatewayId)
                {
                    // it is a retry message coming up by the same first gateway
                    newFCntDown = cachedDeviceState.FCntDown + 1;
                    cachedDeviceState.FCntDown = newFCntDown;

                    _ = deviceCache.StoreInfo(cachedDeviceState);
                }
            }

            return newFCntDown;
        }
    }
}
