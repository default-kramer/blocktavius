using Antipasta;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.AppDQB2;

public interface IProperty<TOutput> : IElement<TOutput> { }

public interface IViewmodel : INodeGroup { }

public abstract class DerivedProp<TSelf, TOutput> : DerivedElement<TOutput>
	where TSelf : DerivedProp<TSelf, TOutput>
{
	public required IViewmodel Owner { get; init; }
	public sealed override INodeGroup NodeGroup => Owner;
}

public abstract class SettableDerivedProp<TSelf, TOutput> : SettableDerivedElement<TOutput>
	where TSelf : SettableDerivedProp<TSelf, TOutput>
{
	public required IViewmodel Owner { get; init; }
	public sealed override INodeGroup NodeGroup => Owner;
}

public class OriginProp<TSelf, TOutput> : SettableDerivedProp<TSelf, TOutput>
	where TSelf : OriginProp<TSelf, TOutput>
{
	public required TOutput InitialValue { get; init; }

	protected override bool AcceptSetValueRequest(ref TOutput newValue) => true;
	protected override TOutput Recompute() => CachedValue ?? InitialValue;
}
