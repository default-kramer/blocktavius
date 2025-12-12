using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Antipasta;

/// <summary>
/// TODO - Is this wise? Should the listener list be WeakRef{INode} anyway?
/// </summary>
public enum GraphConnectionStatus
{
	Connected,
	TemporarilyDisconnected,
	PermanentlyDisconnected,
}
