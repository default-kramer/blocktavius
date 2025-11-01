using Blocktavius.DQB2;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.AppDQB2;

sealed class StgdatLoader
{
	public sealed record LoadResult
	{
		public required string StgdatPath { get; init; }
		public required DateTime LoadTimeUtc { get; init; }
		public required ICloneableStage Stage { get; init; }
	}

	private LoadResult? previousLoad = null;
	private static LoadResult? sharedPreviousLoad = null;

	public bool TryLoad(string stgdatPath, out LoadResult result, out string error)
	{
		if (string.IsNullOrWhiteSpace(stgdatPath))
		{
			result = null!;
			error = "File path cannot be empty";
			return false;
		}

		var stgdatFile = new FileInfo(stgdatPath);
		if (!stgdatFile.Exists)
		{
			result = null!;
			error = $"File does not exist: {stgdatFile.FullName}";
			return false;
		}

		error = "";
		result = MaybeUseCache(previousLoad, stgdatFile)
			?? MaybeUseCache(sharedPreviousLoad, stgdatFile)
			?? DoLoad(stgdatFile, out error)
			?? null!;

		return result != null;
	}

	private static LoadResult? MaybeUseCache(LoadResult? cachedResult, FileInfo wantFile)
	{
		if (cachedResult == null
			|| cachedResult.StgdatPath.ToLowerInvariant() != wantFile.FullName.ToLowerInvariant()
			|| wantFile.LastWriteTimeUtc > cachedResult.LoadTimeUtc)
		{
			return null;
		}
		return cachedResult;
	}

	private LoadResult? DoLoad(FileInfo stgdatFile, out string error)
	{
		error = "";
		var loadTimeUtc = DateTime.UtcNow;
		try
		{
			var stage = ImmutableStage.LoadStgdat(stgdatFile.FullName);

			var result = new LoadResult()
			{
				LoadTimeUtc = loadTimeUtc,
				Stage = stage,
				StgdatPath = stgdatFile.FullName,
			};

			previousLoad = result;
			sharedPreviousLoad = result;
			return result;
		}
		catch (Exception ex)
		{
			error = ex.Message;
			return null;
		}
	}
}
