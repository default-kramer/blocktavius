using Blocktavius.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.DQB2;

/// <summary>
/// Low-level wrapper around a byte array.
/// </summary>
interface IBlockdata
{
	ushort GetBlock(Point point);

	LittleEndianStuff.ByteArrayBlockdata Clone();

	ValueTask WriteAsync(Stream stream);

	TSelf HackySelfCast<TSelf>() where TSelf : struct, IBlockdata;
}

interface IMutableBlockdata : IBlockdata
{
	void SetBlock(Point point, ushort block);
}