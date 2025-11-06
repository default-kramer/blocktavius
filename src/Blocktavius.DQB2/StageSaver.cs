using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.DQB2;

/// <summary>
/// To make extra certain the user has designated this slot as writable.
/// </summary>
public interface IWritableSaveSlot
{
	public DirectoryInfo Directory { get; }
}

public interface IStageSaver
{
	bool CanSave { get; }

	void Save(IWritableSaveSlot slot, IStage stage, FileInfo? assertDestFilename);
}
