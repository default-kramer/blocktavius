using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.DQB2.EyeOfRubiss;

public sealed class Driver : IDisposable
{
	private readonly DirectoryInfo driverDir;
	private readonly FileInfo driverFile;
	private readonly SemaphoreSlim writeLock = new(initialCount: 1, maxCount: 1);
	private readonly ProcManager procManager;

	private Driver(DirectoryInfo driverDir, FileInfo driverFile, ProcManager procManager)
	{
		this.driverDir = driverDir;
		this.driverFile = driverFile;
		this.procManager = procManager;
	}

	public static Driver CreateAndStart(Config config)
	{
		var driverPath = config.DriverDirectory
			?? Path.Combine(Path.GetTempPath(), "Blocktavius-EyeOfRubiss", Guid.NewGuid().ToString("D"));
		var driverDir = new DirectoryInfo(driverPath);
		driverDir.Create();

		var driverFile = new FileInfo(Path.Combine(driverPath, "driver.json"));

		// Driver File must exist before Eye Of Rubiss is launched. (For now at least.)
		BuildInitialDriverFile().WriteToFile(driverFile);

		var procManager = new ProcManager(driverFile, config);

		procManager.StartEyeOfRubiss();

		return new Driver(driverDir, driverFile, procManager);
	}

	public void Dispose()
	{
		writeLock?.Dispose();
		procManager.ShutdownEyeOfRubiss();
	}

	public async Task WriteStageAsync(IStage stage)
	{
		await writeLock.WaitAsync().ConfigureAwait(false);

		try
		{
			List<Task> chunkTasks = new();
			foreach (var chunk in stage.IterateChunks())
			{
				chunkTasks.Add(WriteChunkFile(chunk, driverDir));
			}

			var driverFileContent = BuildDriverFileContent(stage);
			await Task.WhenAll(chunkTasks).ConfigureAwait(false);
			driverFileContent.WriteToFile(driverFile);
		}
		finally
		{
			writeLock.Release();
		}
	}

	private static async Task WriteChunkFile(IChunk chunk, DirectoryInfo driverDir)
	{
		var filename = Path.Combine(driverDir.FullName, BuildChunkFilename(chunk.Offset));
		using var stream = File.Open(filename, FileMode.Create, FileAccess.Write);
		await chunk.Internals.WriteBlockdataAsync(stream).ConfigureAwait(false);
		stream.Flush();
		stream.Close();
	}

	private static string BuildChunkFilename(ChunkOffset offset)
	{
		var corner = offset.NorthwestCorner;
		return $"chunk.{corner.X}.{corner.Z}.bin";
	}

	private static DriverFileModel BuildDriverFileContent(IStage stage)
	{
		var offsets = stage.ChunksInUse;

		var chunkInfos = offsets.Select(o => new DriverFileModel.ChunkInfo
		{
			OffsetX = o.NorthwestCorner.X,
			OffsetZ = o.NorthwestCorner.Z,
			RelativePath = BuildChunkFilename(o),
		});

		var content = new DriverFileModel()
		{
			UniqueValue = Guid.NewGuid().ToString(),
			ChunkInfos = chunkInfos.ToList(),
			SetCameraX = 1024,
			SetCameraZ = 1024,
		};

		return content;
	}

	private static DriverFileModel BuildInitialDriverFile()
	{
		return new DriverFileModel()
		{
			UniqueValue = Guid.NewGuid().ToString(),
			ChunkInfos = new List<DriverFileModel.ChunkInfo>(),
			SetCameraX = null,
			SetCameraZ = null,
		};
	}

	public sealed record Config
	{
		public required string EyeOfRubissExePath { get; init; }

		/// <summary>
		/// If null, a temp directory will be created.
		/// </summary>
		public string? DriverDirectory { get; init; }

		/// <summary>
		/// Handy to view output from Eye of Rubiss
		/// </summary>
		public bool UseCmdShell { get; init; } = false;
	}

	sealed class ProcManager
	{
		private readonly FileInfo driverFile;
		private readonly Config config;
		private Process? process;
		private readonly object processLock = new();

		public ProcManager(FileInfo driverFile, Config config)
		{
			this.driverFile = driverFile;
			this.config = config;
		}

		public void StartEyeOfRubiss()
		{
			lock (processLock)
			{
				if (process == null || process.HasExited)
				{
					process = StartEyeOfRubiss(driverFile, config);
				}
			}
		}

		public void ShutdownEyeOfRubiss()
		{
			lock (processLock)
			{
				var process = this.process;
				if (process == null)
				{
					return;
				}

				try { process.CloseMainWindow(); } catch { }
				try { process.Close(); }
				catch
				{
					try { process.Kill(); } catch { }
				}
				try { process.Dispose(); } catch { }
				this.process = null;
			}
		}

		private static Process StartEyeOfRubiss(FileInfo driverFile, Config config)
		{
			var psi = new ProcessStartInfo();
			psi.UseShellExecute = false;

			if (config.UseCmdShell)
			{
				// We want something like
				//   cmd.exe /K ""C:\path with spaces maybe\EyeOfRubiss.exe" --driverFile "C:\Temp with spaces maybe\driverFile.json""
				string command = $"\"{config.EyeOfRubissExePath}\" --driverFile \"{driverFile.FullName}\"";
				psi.FileName = "cmd.exe";
				psi.Arguments = $"/K \"{command}\""; // can't use ArgumentList here
			}
			else
			{
				psi.FileName = config.EyeOfRubissExePath;
				psi.ArgumentList.Add("--driverFile");
				psi.ArgumentList.Add(driverFile.FullName);
			}

			// Do not turn these on; they will block this app from exiting until the child proc is closed:
			//psi.RedirectStandardError = true;
			//psi.RedirectStandardOutput = true;

			var proc = new Process();
			proc.StartInfo = psi;
			proc.Start();
			return proc;
		}
	}
}
