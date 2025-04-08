using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LoRaTools.CacheStore
{
    public class NoCacheStore : ICacheStore
    {
        public HashEntry[] GetHashObject(string key)
        {
            throw new NotImplementedException();
        }

        public T GetObject<T>(string key) where T : class
        {
            throw new NotImplementedException();
        }

        public Task<TimeSpan?> GetObjectTTL(string key)
        {
            throw new NotImplementedException();
        }

        public bool KeyDelete(string key)
        {
            throw new NotImplementedException();
        }

        public bool KeyExists(string key)
        {
            throw new NotImplementedException();
        }

        public long ListAdd(string key, string value, TimeSpan? expiration = null)
        {
            throw new NotImplementedException();
        }

        public IReadOnlyList<string> ListGet(string key)
        {
            throw new NotImplementedException();
        }

        public bool LockRelease(string key, string owner)
        {
            throw new NotImplementedException();
        }

        public Task<bool> LockTakeAsync(string key, string owner, TimeSpan expiration, bool block = true)
        {
            throw new NotImplementedException();
        }

        public bool ObjectSet<T>(string key, T value, TimeSpan? expiration, bool onlyIfNotExists = false) where T : class
        {
            return false;
        }

        public void ReplaceHashObjects<T>(string cacheKey, IDictionary<string, T> input, TimeSpan? timeToExpire = null, bool removeOldOccurence = false) where T : class
        {
            throw new NotImplementedException();
        }

        public void SetHashObject(string key, string subkey, string value, TimeSpan? timeToExpire = null)
        {
            throw new NotImplementedException();
        }

        public string StringGet(string key)
        {
            return null; //same as redis.StringGet, which returns 'RedisValue.Null' if the key isn't found, which ToString() then string casts to null.
        }

        public bool StringSet(string key, string value, TimeSpan? expiration, bool onlyIfNotExists = false)
        {
            return false;
        }

        public bool TryChangeLockTTL(string key, TimeSpan timeToExpire)
        {
            return false;
        }
    }
}
