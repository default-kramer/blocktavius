using Blocktavius.DQB2;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Blocktavius.AppDQB2;

/// <summary>
/// Interaction logic for PlanScriptDialog.xaml
/// </summary>
public partial class PlanScriptDialog : Window
{
	public PlanScriptDialog()
	{
		InitializeComponent();
	}

	internal void ShowDialog(ProjectVM project)
	{
		using var vm = new PlanScriptVM(project);
		this.DataContext = vm;

		this.ShowDialog();
	}

	private async void OkButton_Click(object sender, RoutedEventArgs e)
	{
		var vm = (PlanScriptVM)DataContext;
		await vm.Execute();
	}

	/// <summary>
	/// Shows the tool tip when the link is clicked.
	/// </summary>
	private void Hyperlink_Click(object sender, RoutedEventArgs e)
	{
		var block = (sender as Hyperlink)?.LogicalAncestors()?.OfType<TextBlock>()?.FirstOrDefault();
		if (block != null && block.DataContext is IPlanItemVM planItem)
		{
			bool copyToClipboard = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);

			string message = planItem.StatusToolTip;
			if (copyToClipboard)
			{
				Clipboard.SetText(message);
			}
			else
			{
				const string hint = "(Hold ctrl while clicking to copy this message to the clipboard.)";
				message = $"{hint}{Environment.NewLine}{Environment.NewLine}{message}";
			}

			// For some reason doing this in XAML with a binding causes weird quirks like the tool tip
			// showing as a tiny empty box, so do it in code instead.
			// (Also because of the ctrl+click -> clipboard thing.)
			var toolTip = new ToolTip();
			block.ToolTip = toolTip;

			const int maxWidth = 350;
			toolTip.MaxWidth = maxWidth;
			toolTip.Content = new TextBlock { Text = message, MaxWidth = maxWidth, TextWrapping = TextWrapping.Wrap };
			toolTip.IsOpen = true;
			toolTip.StaysOpen = false;
		}
	}

	/// <summary>
	/// Subset of properties from the <see cref="ProjectVM"/> which
	/// require us to rebuild the plan when they are changed.
	/// </summary>
	sealed record ProjectDeps
	{
		public required SlotVM? SelectedSourceSlot { get; init; }
		public required WritableSlotVM? SelectedDestSlot { get; init; }
		public required InclusionModeVM SelectedInclusionMode { get; init; }
		public required bool DestIsSource { get; init; }

		public static ProjectDeps Rebuild(ProjectVM project) => new ProjectDeps
		{
			SelectedSourceSlot = project.GetSelectedSourceSlot,
			SelectedDestSlot = project.GetSelectedDestSlot,
			SelectedInclusionMode = project.SelectedInclusionMode,
			DestIsSource = project.GetSelectedSourceSlot != null
				&& project.GetSelectedSourceSlot.FullPath.Equals(project.GetSelectedDestSlot?.FullPath, StringComparison.OrdinalIgnoreCase),
		};
	}

	sealed class PlanScriptVM : ViewModelBase, IDisposable
	{
		private readonly object subscribeKey = new();
		private readonly DirectoryInfo? backupLocation;
		private readonly Task<IStage?> rebuildTask;

		public ObservableCollection<IPlanItemVM> PlanItems { get; } = new();
		public ProjectVM Project { get; }
		public string BackupMessage { get; }
		public string BackupWarning { get; }

		private bool _canExecute = false;
		public bool CanExecute
		{
			get => _canExecute;
			set => ChangeProperty(ref _canExecute, value);
		}

		// After the script runs (to success or failure), we change the dialog to readonly
		// mode so that it cannot be run again.
		private bool _isDone = false;
		public bool IsDone
		{
			get => _isDone;
			set
			{
				if (_isDone && !value)
				{
					// This is especially important so we don't re-use the same backup directory
					throw new Exception("Assert fail - Cannot go from Done to Not-Done");
				}
				ChangeProperty(ref _isDone, value, nameof(IsDone), nameof(IsNotDone));
			}
		}

		public bool IsNotDone => !IsDone;

		private string _runScriptError = "";
		public string RunScriptError
		{
			get => _runScriptError;
			set => ChangeProperty(ref _runScriptError, value);
		}

		private bool _destIsSource;
		public bool DestIsSource
		{
			get => _destIsSource;
			private set => ChangeProperty(ref _destIsSource, value);
		}

		private string? _srcSlotName = null;
		public string SourceSlotName
		{
			get => _srcSlotName ?? "";
			private set => ChangeProperty(ref _srcSlotName, value);
		}

		public string ScriptName { get; }
		public string Title { get; }

		public PlanScriptVM(ProjectVM project)
		{
			this.Project = project;
			project.Subscribe(subscribeKey, this);

			if (project.SelectedScript == null)
			{
				throw new ArgumentException("SelectedScript must not be null here");
			}
			ScriptName = project.SelectedScript.GetScriptName() ?? "<Untitled Script>";
			Title = $"Plan and Run -- {ScriptName}";

			rebuildTask = project.TryRebuildStage();

			if (project.BackupsEnabled(out var backupDir))
			{
				backupLocation = new DirectoryInfo(Path.Combine(backupDir.FullName, DateTime.UtcNow.ToString("yyyyMMdd.HHmmss.fff")));
				BackupMessage = $"Backups will be created at {backupLocation.FullName}";
				BackupWarning = "";
			}
			else
			{
				backupLocation = null;
				BackupMessage = "";
				BackupWarning = "Backups will not be created! Be careful!";
			}

			_deps = ProjectDeps.Rebuild(project);
			UpdatePlan();
		}

		private ProjectDeps _deps;
		private ProjectDeps Deps
		{
			get => _deps;
			set
			{
				if (ChangeProperty(ref _deps, value))
				{
					UpdatePlan();
				}
			}
		}

		protected override void OnSubscribedPropertyChanged(ViewModelBase sender, PropertyChangedEventArgs e)
		{
			if (IsDone) { return; }

			Deps = ProjectDeps.Rebuild(Project);
		}

		void IDisposable.Dispose()
		{
			Project.Unsubscribe(subscribeKey);
		}

		private void UpdatePlan()
		{
			if (IsDone) { return; }

			DestIsSource = Deps.DestIsSource;
			SourceSlotName = Deps.SelectedSourceSlot?.Name ?? "";
			CanExecute = Deps.SelectedDestSlot != null
				&& Deps.SelectedSourceSlot != null;

			if (Project.GetSelectedSourceStage == null)
			{
				CanExecute = false;
				IsDone = true; // not really "done", but setting this makes it readonly which is what we want
				RunScriptError = "Source stage must be selected (on the main window).";
			}

			PlanItems.Clear();

			var mode = Project.SelectedInclusionMode.InclusionMode;
			var sourceSlot = Project.GetSelectedSourceSlot;
			var destSlot = Project.GetSelectedDestSlot;

			if (sourceSlot == null)
			{
				return;
			}

			TryPlanSimpleCopy(sourceSlot, "AUTOCMNDAT.BIN", mode == InclusionMode.Automatic);
			TryPlanSimpleCopy(sourceSlot, "AUTOSTGDAT.BIN", mode == InclusionMode.Automatic);
			//TryPlanSimpleCopy(sourceSlot, "CMNDAT.BIN", mode == InclusionMode.Automatic || mode == InclusionMode.JustCmndat, forceBackup: true);
			TryPlanHackyCmndatEdit(sourceSlot);

			var sortedStages = sourceSlot.Stages
				.OrderBy(stage => stage == Project.GetSelectedSourceStage ? 0 : 1)
				.ThenBy(stage => stage.KnownStageSortOrder ?? int.MaxValue)
				.ThenBy(stage => stage.Name)
				.ToList();

			foreach (var stage in sortedStages)
			{
				IPlanItemVM planItem;

				if (stage == Project.GetSelectedSourceStage)
				{
					planItem = new CopyWithModificationsPlanItemVM()
					{
						DestIsSource = Deps.DestIsSource,
						RebuildStageTask = rebuildTask,
						SourceStgdatFile = stage.StgdatFile,
						BackupDir = this.backupLocation,
						ShortName = stage.Name,
					};
				}
				else
				{
					bool shouldCopy = mode == InclusionMode.Automatic && stage.IsKnownStage;
					planItem = new SimpleCopyPlanItemVM
					{
						ForceBackup = false,
						DestIsSource = Deps.DestIsSource,
						BackupDir = this.backupLocation,
						SourceFile = stage.StgdatFile,
						ShouldCopy = shouldCopy,
						ShortName = stage.Name,
					};
				}

				PlanItems.Add(planItem);
			}

			if (destSlot == null)
			{
				var items = PlanItems.Select(DoNothingPlanItemVM.CopyFrom).ToList();
				PlanItems.Clear();
				foreach (var item in items)
				{
					PlanItems.Add(item);
				}
			}
		}

		private bool TryPlanSimpleCopy(SlotVM sourceSlot, string name, bool shouldCopy, bool forceBackup = false)
		{
			var sourceFile = new FileInfo(sourceSlot.GetFullPath(name));
			if (sourceFile.Exists)
			{
				PlanItems.Add(new SimpleCopyPlanItemVM
				{
					ForceBackup = forceBackup,
					DestIsSource = Deps.DestIsSource,
					BackupDir = this.backupLocation,
					SourceFile = sourceFile,
					ShouldCopy = shouldCopy,
					ShortName = name,
				});
				return true;
			}
			return false;
		}

		private bool TryPlanHackyCmndatEdit(SlotVM sourceSlot)
		{
			var sourceFile = new FileInfo(sourceSlot.GetFullPath("CMNDAT.BIN"));
			if (sourceFile.Exists)
			{
				PlanItems.Add(new CmndatHackPlan
				{
					BackupDir = this.backupLocation,
				});
				return true;
			}
			return false;
		}

		public async Task Execute()
		{
			if (!CanExecute) return;
			if (IsDone)
			{
				throw new Exception("Assert fail - Can only run once");
			}
			CanExecute = false;
			IsDone = true;

			if (Project.GetSelectedDestSlot == null)
			{
				RunScriptError = "Destination slot not set"; // should never happen
				return;
			}
			if (Project.GetSelectedSourceSlot == null)
			{
				RunScriptError = "Source slot not set"; // should never happen
				return;
			}

			var context = new ExecutionContext()
			{
				FromSlot = Project.GetSelectedSourceSlot,
				ToSlot = Project.GetSelectedDestSlot,
			};

			var planItems = PlanItems.ToList();
			var backupDir = planItems.Any(p => p.WillBeBackedUp) ? this.backupLocation : null;

			try
			{
				var stage = await rebuildTask;
				if (stage == null || !stage.Saver.CanSave)
				{
					RunScriptError = "Failed to construct the modified stage.";
					return;
				}

				if (backupDir != null)
				{
					backupDir.Create();
				}

				var tasks = planItems.Select(context.Execute).ToList();
				await Task.WhenAll(tasks);
			}
			catch (AggregateException aggEx)
			{
				context.TopLevelException = aggEx;
				var sb = new StringBuilder();
				string spacer = "";
				foreach (var ex in aggEx.InnerExceptions)
				{
					sb.Append(spacer).Append(ex.Message);
					spacer = Environment.NewLine + Environment.NewLine;
				}
				RunScriptError = sb.ToString();
			}
			catch (Exception ex)
			{
				context.TopLevelException = ex;
				RunScriptError = ex.Message;
			}
			finally
			{
				if (context != null && backupDir != null)
				{
					try
					{
						var path = Path.Combine(backupDir.FullName, "_executionLog.txt");
						File.WriteAllText(path, context.CombineLogs(planItems));
					}
					catch (Exception) { }
				}
			}
		}
	}

	const int minMilliseconds = 600;

	sealed class ExecutionContext
	{
		private readonly ConcurrentDictionary<IPlanItemVM, StringBuilder> logs = new();
		public required SlotVM FromSlot { get; init; }
		public required WritableSlotVM ToSlot { get; init; }
		public Exception? TopLevelException { get; set; } = null;

		public string CombineLogs(IReadOnlyList<IPlanItemVM> planItems)
		{
			var sb = new StringBuilder();
			sb.AppendLine($"From: {FromSlot.Name}  {FromSlot.FullPath}");
			sb.AppendLine($"  To: {ToSlot.Name}  {ToSlot.FullPath}");

			if (TopLevelException != null)
			{
				sb.AppendLine().AppendLine("=== ERROR ===");
				sb.AppendLine(TopLevelException.ToString());
			}

			foreach (var item in planItems)
			{
				sb.AppendLine().AppendLine($"=== {item.ShortName} ===");
				sb.AppendLine($"    Backup? {item.WillBeBackedUp}");
				sb.AppendLine($"      Copy? {item.WillBeCopied}");
				sb.AppendLine($"    Modify? {item.WillBeModified}");
				sb.Append(logs.GetValueOrDefault(item)?.ToString() ?? $"No action taken.{Environment.NewLine}");
			}

			return sb.ToString();
		}

		public StringBuilder GetLogger(IPlanItemVM planItem) => logs.GetOrAdd(planItem, new StringBuilder());

		public async Task Execute(IPlanItemVM planItem)
		{
			try
			{
				await planItem.Execute(this);
			}
			catch (Exception ex)
			{
				var logger = GetLogger(planItem);
				logger.AppendLine("Unhandled exception!").AppendLine(ex.ToString());
				throw;
			}
		}
	}

	interface IPlanItemVM
	{
		bool WillBeBackedUp { get; }
		bool WillBeModified { get; }
		bool WillBeCopied { get; }
		string ShortName { get; }
		string Status { get; }
		string StatusToolTip { get; }

		Task Execute(ExecutionContext context);
	}

	abstract class PlanItemVM : ViewModelBase
	{
		public required DirectoryInfo? BackupDir { get; init; }

		private string _status = "";
		public string Status
		{
			get => _status;
			protected set => ChangeProperty(ref _status, value);
		}

		private string _statusToolTip = "";
		public string StatusToolTip
		{
			get => _statusToolTip;
			protected set => ChangeProperty(ref _statusToolTip, value);
		}

		protected async Task<(bool success, Exception? exception)> DoBackup(FileInfo file)
		{
			Status = "Backing up...";
			if (BackupDir == null)
			{
				throw new Exception("Assert fail! Promised to create backup, but BackupDir is null");
			}

			bool success = false;
			Exception? exception = null;

			await Task.Run(() =>
			{
				try
				{
					file.CopyTo(Path.Combine(BackupDir.FullName, file.Name), overwrite: true);
					success = true;
				}
				catch (Exception ex)
				{
					exception = ex;
				}
			});

			return (success, exception);
		}
	}

	/// <summary>
	/// For when there is no dest slot and we just want to list the files.
	/// </summary>
	class DoNothingPlanItemVM : IPlanItemVM
	{
		public bool WillBeBackedUp => false;
		public bool WillBeModified => false;
		public bool WillBeCopied => false;
		public required string ShortName { get; init; }
		public string Status => "";
		public string StatusToolTip => "";

		public static DoNothingPlanItemVM CopyFrom(IPlanItemVM orig)
		{
			return new DoNothingPlanItemVM { ShortName = orig.ShortName };
		}

		public Task Execute(ExecutionContext context) => Task.CompletedTask;
	}

	class SimpleCopyPlanItemVM : PlanItemVM, IPlanItemVM
	{
		/// <summary>
		/// Used for CMNDAT, which we want to back up even when DestIsSource.
		/// </summary>
		public required bool ForceBackup { get; init; }
		public required bool DestIsSource { get; init; }
		public required FileInfo SourceFile { get; init; }
		public required bool ShouldCopy { get; init; }
		public required string ShortName { get; init; }

		public bool WillBeCopied => !DestIsSource && ShouldCopy;
		public bool WillBeBackedUp => BackupDir != null && (ForceBackup || (WillBeCopied && !DestIsSource));
		public bool WillBeModified => false;

		public async Task Execute(ExecutionContext context)
		{
			WritableSlotVM targetSlot = context.ToSlot;
			Status = "";
			StatusToolTip = "";
			var fullLog = context.GetLogger(this);

			var targetFile = new FileInfo(targetSlot.GetFullPath(SourceFile.Name));
			bool doBackup = WillBeBackedUp && targetFile.Exists;
			bool doCopy = WillBeCopied;

			if (!doBackup && !doCopy)
			{
				if (WillBeBackedUp)
				{
					Status = "Skipped";
					StatusToolTip = "File did not exist, nothing to back up.";
					fullLog.AppendLine("Skipped - File did not exist, nothing to back up.");
				}
				return;
			}

			var minDelay = Task.Delay(minMilliseconds);
			var errorDetails = new StringBuilder();

			if (doBackup)
			{
				var (success, exception) = await DoBackup(targetFile);
				if (success)
				{
					fullLog.AppendLine("Backup completed.");
				}
				else
				{
					if (doCopy)
					{
						errorDetails.Append("Backup failed; copy skipped.");
						fullLog.AppendLine("Backup failed; copy skipped.");
					}
					else
					{
						errorDetails.Append("Backup failed. Nothing else to do.");
						fullLog.AppendLine("Backup failed. Nothing else to do.");
					}

					if (exception != null)
					{
						errorDetails.AppendLine().AppendLine().Append(exception.Message);
						fullLog.AppendLine(exception.ToString());
					}

					await minDelay;
					StatusToolTip = errorDetails.ToString();
					Status = "Error";
					return;
				}
			}

			if (doCopy)
			{
				Status = "Copying...";
				await Task.Run(() =>
				{
					try
					{
						// Steam Autocloud will report a conflict if you copy a file with an
						// older Modified Timestamp, like this:
						//    SourceFile.CopyTo(targetFile.FullName, overwrite: true);
						// And maybe there's more to it than just the Modified Timestamp...
						// Doing the copy this way seems to work well enough:
						using var readStream = File.Open(SourceFile.FullName, FileMode.Open, FileAccess.Read);
						using var writeStream = File.Open(targetFile.FullName, FileMode.Create, FileAccess.Write);
						readStream.CopyTo(writeStream);
						writeStream.Flush();
						writeStream.Close();

						fullLog.AppendLine("Copy completed.");
					}
					catch (Exception ex)
					{
						errorDetails.AppendLine("Backup succeeded; copy failed.").AppendLine().Append(ex.Message);
						fullLog.AppendLine("Backup succeeded; copy failed.").AppendLine(ex.ToString());
					}
				});
			}

			await minDelay;

			StatusToolTip = errorDetails.ToString();
			Status = (StatusToolTip == "") ? "Done" : "Error";
		}
	}

	class CmndatHackPlan : PlanItemVM, IPlanItemVM
	{
		public bool WillBeBackedUp => BackupDir != null;

		public bool WillBeModified => true;

		public bool WillBeCopied => true;

		public string ShortName => "CMNDAT.BIN";

		const int headerLength = 0x2A444;

		public async Task Execute(ExecutionContext context)
		{
			Status = "workinonit...";

			var srcPath = context.FromSlot.GetFullPath("CMNDAT.BIN");

			using var cmndatStream = new FileStream(srcPath, FileMode.Open, FileAccess.Read);

			var header = new byte[headerLength];
			await cmndatStream.ReadExactlyAsync(header, offset: 0, count: headerLength);

			using var zlib = new System.IO.Compression.ZLibStream(cmndatStream, System.IO.Compression.CompressionMode.Decompress);

			const int decompressedLength = 5627194; // hypothesis: The decompressed buffer will always have this length
			using var decompressedStream = new MemoryStream(decompressedLength);
			await zlib.CopyToAsync(decompressedStream);
			await zlib.FlushAsync();
			await decompressedStream.FlushAsync();

			if (decompressedStream.Length != decompressedLength && System.Diagnostics.Debugger.IsAttached)
			{
				System.Diagnostics.Debugger.Break(); // decompressedLength hypothesis invalidated?
			}

			Memory<byte> data;
			if (decompressedStream.TryGetBuffer(out var buffer))
			{
				data = buffer.AsMemory();
			}
			else
			{
				data = new Memory<byte>(decompressedStream.ToArray());
			}

			var mapData = data.Slice(2401803); // skip to start of first island's minimap data

			const int INTRO_SIZE = 0;
			const int TILE_DATA_SIZE = 256 * 256 * 2;
			const int OUTRO_SIZE = 4;
			const int ISLAND_DATA_SIZE = INTRO_SIZE + TILE_DATA_SIZE + OUTRO_SIZE;

			const int islandId = 13;
			const int start = INTRO_SIZE + ISLAND_DATA_SIZE * islandId;
			var span = mapData.Slice(start, TILE_DATA_SIZE + OUTRO_SIZE).Span;

			var tile = GetTileToCopy(span);
			// The chunk with NW corner at 800,800 is in-bounds on the IoA.
			// Tile offsets for this chunk are 100,100 -> 104,104.
			for (int z = 100; z < 104; z++)
			{
				for (int x = 100; x < 104; x++)
				{
					int offset = GetOffset(x, z);
					span[offset] = tile.Item1;
					span[offset + 1] = tile.Item2;
				}
			}

			/*
			// What happens if we use "outro" bytes from the IoA?
			var existingOutro = Convert.ToHexString(span.Slice(TILE_DATA_SIZE, OUTRO_SIZE));
			span[TILE_DATA_SIZE + 0] = 0x7E;
			span[TILE_DATA_SIZE + 1] = 0x80;
			span[TILE_DATA_SIZE + 2] = 0x01;
			span[TILE_DATA_SIZE + 3] = 0x1E;
			var replacedOutro = Convert.ToHexString(span.Slice(TILE_DATA_SIZE, OUTRO_SIZE));
			*/

			Status = "Logic done, saving...";

			await WriteCmndatFile(header, data, context.ToSlot.GetFullPath("CMNDAT.BIN"));

			Status = "Minimap modified!";
		}

		private (byte, byte) GetTileToCopy(ReadOnlySpan<byte> span)
		{
			// grab whatever tile is in the center of the minimap
			const int x = 1024 / 8;
			const int z = 1024 / 8;
			int offset = GetOffset(x, z);
			return (span[offset], span[offset + 1]);
		}

		private static int GetOffset(int x, int z) => z * 256 * 2 + x * 2;

		private static async Task WriteCmndatFile(byte[] origHeader, Memory<byte> uncompressedContent, string dstPath)
		{
			using var cmndatStream = new FileStream(dstPath, FileMode.Create, FileAccess.Write);
			await cmndatStream.WriteAsync(origHeader);

			using var compressedBody = new MemoryStream();
			using (var zlib = new ZLibStream(compressedBody, CompressionMode.Compress, leaveOpen: true))
			{
				await zlib.WriteAsync(uncompressedContent);
				await zlib.FlushAsync();
				compressedBody.Flush();
			}

			compressedBody.Seek(0, SeekOrigin.Begin);
			await compressedBody.CopyToAsync(cmndatStream);

			await cmndatStream.FlushAsync();
			cmndatStream.Close();
		}
	}

	class CopyWithModificationsPlanItemVM : PlanItemVM, IPlanItemVM
	{
		public required bool DestIsSource { get; init; }
		public required FileInfo SourceStgdatFile { get; init; }
		public required Task<IStage?> RebuildStageTask { get; init; }
		public required string ShortName { get; init; }

		public bool WillBeModified => true;
		public bool WillBeCopied => !DestIsSource;
		public bool WillBeBackedUp => BackupDir != null;

		public async Task Execute(ExecutionContext context)
		{
			WritableSlotVM targetSlot = context.ToSlot;
			Status = "";
			StatusToolTip = "";
			var minDelay = Task.Delay(minMilliseconds);
			var fullLog = context.GetLogger(this);

			Status = "Rebuilding...";
			var stage = await RebuildStageTask;
			if (stage == null || !stage.Saver.CanSave)
			{
				throw new Exception("Assert fail - cannot save, should not have even started the plan!");
			}

			var targetFile = new FileInfo(targetSlot.GetFullPath(SourceStgdatFile.Name));
			bool doBackup = WillBeBackedUp && targetFile.Exists;

			var errorDetails = new StringBuilder();

			if (doBackup)
			{
				var (success, exception) = await DoBackup(targetFile);
				if (success)
				{
					fullLog.AppendLine("Backup completed.");
				}
				else
				{
					errorDetails.Append("Backup failed; stopping.");
					fullLog.AppendLine("Backup failed; stopping.");
					if (exception != null)
					{
						errorDetails.AppendLine().AppendLine().Append(exception.Message);
						fullLog.AppendLine(exception.ToString());
					}

					await minDelay;
					Status = "Error";
					StatusToolTip = errorDetails.ToString();
					return;
				}
			}

			Status = "Saving...";
			await Task.Run(() =>
			{
				try
				{
					stage.Saver.Save(targetSlot.WritableSlot, stage, targetFile, includeEmptyChunks: false);
					fullLog.AppendLine("Scripted stage constructed successfully!");
				}
				catch (Exception ex)
				{
					errorDetails.AppendLine("Failed to save stage.").AppendLine().Append(ex.Message);
					fullLog.AppendLine("Failed to save stage.").AppendLine(ex.ToString());
				}
			});

			await minDelay;
			StatusToolTip = errorDetails.ToString();
			Status = (StatusToolTip == "") ? "Done" : "Error";
		}
	}
}
