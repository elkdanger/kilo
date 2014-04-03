﻿using System;
using System.Runtime.Caching;

namespace Kilo.Data.Caching
{
	public class DefaultCacheProvider : ICacheProvider
	{
		/// <summary>
		/// Gets an object from the cache with the specified key
		/// </summary>
		/// <param name="key">The key.</param>
		/// <returns></returns>
		public object Get(string key)
		{
			return MemoryCache.Default[key];
		}

		/// <summary>
		/// Sets the value into the cache using the specified key.
		/// </summary>
		/// <param name="key">The key.</param>
		/// <param name="value">The value.</param>
		/// <param name="expiry">The expiry.</param>
		public void Set(string key, object value, DateTime? expiry = null)
		{
			CacheItem cacheItem = new CacheItem(key, value);
			CacheItemPolicy policy = null;

			if (expiry.HasValue)
			{
				policy = new CacheItemPolicy() { AbsoluteExpiration = expiry.Value };
			}
		
			MemoryCache.Default.Add(cacheItem, policy);
		}

		/// <summary>
		/// Determines whether the specified cache item is set based on its key
		/// </summary>
		/// <param name="key">The key.</param>
		/// <returns>True if the object has been set, false if not.</returns>
		public bool IsSet(string key)
		{
			return Get(key) != null;
		}
	}
}
