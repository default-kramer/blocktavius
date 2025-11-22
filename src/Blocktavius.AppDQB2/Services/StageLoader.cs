using Blocktavius.DQB2;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;

namespace Blocktavius.AppDQB2.Services;

static class IDEA
{
	public static Task MarkSlow(this Task task)
	{
		if (Application.Current?.Dispatcher?.Thread == Thread.CurrentThread)
		{
			// notify scheduler
		}
		return task;
	}

	public static Task<T> MarkSlow<T>(this Task<T> task)
	{
		if (Application.Current?.Dispatcher?.Thread == Thread.CurrentThread)
		{
			// notify scheduler
		}
		return task;
	}
}

interface IStageLoader
{
	Task<LoadStageResult> LoadStage(FileInfo stgdatFile);
}

public sealed record LoadStageResult
{
	public required string StgdatPath { get; init; }
	public required DateTime LoadTimeUtc { get; init; }
	public required ICloneableStage Stage { get; init; }
}

sealed class StageLoader : IStageLoader
{
	private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

	const int pruneCacheAt = 25;
	const int pruneCacheTo = 20;
	private readonly ConcurrentDictionary<CacheKey, CacheValue> cache = new();

	sealed record CacheKey
	{
		public string Path { get; }

		public CacheKey(FileInfo file)
		{
			Path = file.FullName.ToLowerInvariant();
		}
	}

	public async Task<LoadStageResult> LoadStage(FileInfo stgdatFile)
	{
		var key = new CacheKey(stgdatFile);
		var val = cache.GetOrAdd(key, _ => new CacheValue { StgdatFile = stgdatFile.FullName });
		var result = await val.ReloadIfNeeded();

		if (cache.Count > pruneCacheAt)
		{
			try { CleanupCache(cache); }
			catch (Exception ex)
			{
				logger.Warn(ex, "Exception during cache cleanup");
			}
		}

		return result;
	}

	private static void CleanupCache(ConcurrentDictionary<CacheKey, CacheValue> cache)
	{
		var items = cache.OrderBy(kvp => kvp.Value.LastUsedUtc).ToList();
		int removeCount = items.Count - pruneCacheTo;
		for (int i = 0; i < removeCount; i++)
		{
			cache.TryRemove(items[i]);
		}
	}

	sealed class CacheValue
	{
		// immutable:
		private readonly SemaphoreSlim loaderLock = new(1, 1);
		public required string StgdatFile { get; init; }

		// mutable:
		private LoadStageResult? cachedResult = null;
		public DateTime LastUsedUtc { get; private set; } = DateTime.MinValue;

		public async Task<LoadStageResult> ReloadIfNeeded()
		{
			// Set this immediately to avoid removing this item from the cache.
			// We will set it again when we finish loading.
			LastUsedUtc = DateTime.UtcNow;

			// This code assumes that once cachedResult is set to a non-null value it will never go back to null

			if (cachedResult == null || cachedResult.LoadTimeUtc < File.GetLastWriteTimeUtc(StgdatFile))
			{
				bool quickWait = loaderLock.Wait(0);
				if (!quickWait)
				{
					await loaderLock.WaitAsync().MarkSlow();
				}

				try
				{
					var utcNow = DateTime.UtcNow;
					var lastWriteUtc = File.GetLastWriteTimeUtc(StgdatFile);

					if (cachedResult == null || cachedResult.LoadTimeUtc < lastWriteUtc)
					{
						var stage = await Task.Run(() => ImmutableStage.LoadStgdat(StgdatFile)).MarkSlow();
						cachedResult = new()
						{
							LoadTimeUtc = utcNow,
							Stage = stage,
							StgdatPath = StgdatFile,
						};
						logger.Info("Reloaded stage {0}, last written at {1}", StgdatFile, lastWriteUtc);
					}
				}
				finally
				{
					loaderLock.Release();
				}
			}

			LastUsedUtc = DateTime.UtcNow;
			return cachedResult;
		}
	}
}
