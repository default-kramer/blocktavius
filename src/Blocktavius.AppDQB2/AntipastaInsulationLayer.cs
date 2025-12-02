using Antipasta;
using Antipasta.IndexedPropagation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.AppDQB2;

public interface IProperty<TOutput> : IElement<TOutput> { }

public interface IViewmodel : INodeGroup { }

public abstract class DerivedProp<TSelf, TOutput> : DerivedElement<TOutput>, INodeWithStaticPassInfo
	where TSelf : DerivedProp<TSelf, TOutput>
{
	public required IViewmodel Owner { get; init; }
	public sealed override INodeGroup NodeGroup => Owner;

	PassIndex INodeWithStaticPassInfo.PassIndex => passIndex;
	NodeIndex INodeWithStaticPassInfo.NodeIndex => nodeIndex;
	private static readonly PassIndex passIndex;
	private static readonly NodeIndex nodeIndex;
	static DerivedProp()
	{
		if (I.indexer.TryGetByImplementationType(typeof(TSelf), out var info))
		{
			passIndex = info.PassIndex;
			nodeIndex = info.NodeIndex;
		}
		else
		{
			throw new Exception("TODO");
		}
	}
}

public abstract class SettableDerivedProp<TSelf, TOutput> : SettableDerivedElement<TOutput>, INodeWithStaticPassInfo
	where TSelf : SettableDerivedProp<TSelf, TOutput>
{
	public required IViewmodel Owner { get; init; }
	public sealed override INodeGroup NodeGroup => Owner;

	PassIndex INodeWithStaticPassInfo.PassIndex => passIndex;
	NodeIndex INodeWithStaticPassInfo.NodeIndex => nodeIndex;
	private static readonly PassIndex passIndex;
	private static readonly NodeIndex nodeIndex;
	static SettableDerivedProp()
	{
		if (I.indexer.TryGetByImplementationType(typeof(TSelf), out var info))
		{
			passIndex = info.PassIndex;
			nodeIndex = info.NodeIndex;
		}
		else
		{
			throw new Exception("TODO");
		}
	}
}

public class OriginProp<TSelf, TOutput> : SettableDerivedProp<TSelf, TOutput>, INodeWithStaticPassInfo
	where TSelf : OriginProp<TSelf, TOutput>
{
	public required TOutput InitialValue { get; init; }

	protected override bool AcceptSetValueRequest(ref TOutput newValue) => true;
	protected override TOutput Recompute() => CachedValue ?? InitialValue;

	PassIndex INodeWithStaticPassInfo.PassIndex => passIndex;
	NodeIndex INodeWithStaticPassInfo.NodeIndex => nodeIndex;
	private static readonly PassIndex passIndex;
	private static readonly NodeIndex nodeIndex;
	static OriginProp()
	{
		if (I.indexer.TryGetByImplementationType(typeof(TSelf), out var info))
		{
			passIndex = info.PassIndex;
			nodeIndex = info.NodeIndex;
		}
		else
		{
			throw new Exception("TODO");
		}
	}
}
