using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Antipasta.Scheduling;

public enum SpinwaitResult
{
	Unknown,
	Unblocked,
	Timeout,
	SpinwaitNotSupported,
}
