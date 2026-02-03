using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibDQB.B2;

public sealed record CmndatReadOptions
{
	public FileShare FileShare { get; init; } = FileShare.None;
}
