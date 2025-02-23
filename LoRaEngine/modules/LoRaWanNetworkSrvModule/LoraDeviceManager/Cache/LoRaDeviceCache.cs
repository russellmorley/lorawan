// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraDeviceManager.Cache
{
    using System;
    using System.Threading.Tasks;
    using LoRaTools.CacheStore;
    using LoRaWan;
    using Newtonsoft.Json;

    public sealed class LoRaDeviceCache : IDisposable
    {
        private const string CacheKeyLockSuffix = "msglock";
        private static readonly TimeSpan LockExpiry = TimeSpan.FromSeconds(10);

        private readonly ICacheStore cacheStore;
        private readonly string gatewayId;
        private readonly DevEui devEUI;
        private readonly string cacheKey;

        public bool IsLockOwner { get; private set; }

        private string lockKey;

        public LoRaDeviceCache(ICacheStore cacheStore, DevEui devEUI, string gatewayId)
        {
            if (string.IsNullOrEmpty(gatewayId))
            {
                throw new ArgumentNullException(nameof(gatewayId));
            }

            this.cacheStore = cacheStore;
            this.devEUI = devEUI;
            this.gatewayId = gatewayId;
            cacheKey = devEUI.ToString();
        }

        public async Task<bool> TryToLockAsync(string lockKey = null, bool block = true)
        {
            if (IsLockOwner)
            {
                return true;
            }

            var lk = lockKey ?? devEUI + CacheKeyLockSuffix;

            if (IsLockOwner = await cacheStore.LockTakeAsync(lk, gatewayId, LockExpiry, block))
            {
                // store the used key
                this.lockKey = lk;
            }

            return IsLockOwner;
        }

        public bool Initialize(uint fCntUp = 0, uint fCntDown = 0)
        {
            // it is the first message from this device
            var serverStateForDeviceInfo = new DeviceCacheInfo
            {
                FCntDown = fCntDown,
                FCntUp = fCntUp,
                GatewayId = gatewayId
            };

            return StoreInfo(serverStateForDeviceInfo, true);
        }

        public bool TryGetValue(out string value)
        {
            EnsureLockOwner();
            value = cacheStore.StringGet(cacheKey);
            return value != null;
        }

        public bool Exists()
        {
            return cacheStore.KeyExists(cacheKey);
        }

        public bool HasValue()
        {
            return cacheStore.StringGet(cacheKey) != null;
        }

        public bool TryGetInfo(out DeviceCacheInfo info)
        {
            EnsureLockOwner();

            info = cacheStore.GetObject<DeviceCacheInfo>(cacheKey);
            return info != null;
        }

        public bool StoreInfo(DeviceCacheInfo info, bool initialize = false)
        {
            EnsureLockOwner();
            return cacheStore.StringSet(cacheKey, JsonConvert.SerializeObject(info), new TimeSpan(1, 0, 0, 0), initialize);
        }

        public void SetValue(string value, TimeSpan? expiry = null)
        {
            EnsureLockOwner();
            if (!expiry.HasValue)
            {
                expiry = TimeSpan.FromMinutes(1);
            }

            _ = cacheStore.StringSet(cacheKey, value, expiry);
        }

        public void ClearCache()
        {
            EnsureLockOwner();
            _ = cacheStore.KeyDelete(cacheKey);
        }

        private void EnsureLockOwner()
        {
            if (!IsLockOwner)
            {
                throw new InvalidOperationException($"Trying to access cache without owning the lock. Device: {devEUI} Gateway: {gatewayId}");
            }
        }

        private void ReleaseLock()
        {
            if (!IsLockOwner)
            {
                return;
            }

            var released = cacheStore.LockRelease(lockKey, gatewayId);
            if (!released)
            {
                throw new InvalidOperationException("failed to release lock");
            }

            IsLockOwner = false;
        }

        public void Dispose()
        {
            ReleaseLock();
        }
    }
}
