namespace ViewmodelDeputy;

public sealed class AnalyzedProperty
{
	public string PropertyName { get; init; } = default!;
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class DeputizedVMAttribute : Attribute
{
	public string MyPropertiesClassName { get; init; } = "MyProperties";
}

public interface IReadonlyPropBuilder
{

}

public sealed class PropRegistration
{
	public PropRegistration DependsOn(string propName) => this;

	public AnalyzedProperty Finish() => new();
}

public sealed class PropBuilder<TSelf>
{
	public PropRegistration Register(string propName) => new();

	public IReadonlyPropBuilder Freeze() => null!;
}

/// <summary>
/// Specially-recognized return types:
/// * IAsyncComputation{TInput,TResult} or any type that implements it
/// * IComputation{TInput,TResult} or any type that implements it
/// * Task{TResult} would generate an IAsyncComputation{TInput,TResult}
/// Else
/// * TResult would generate an IComputation{TInput,TResult}
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ComputedPropertyAttribute : Attribute
{
	public string PropertyName { get; init; } = ""; // use convention when blank? eg ComputeFoo() method name implies Foo property name

	public string AccessModifier { get; init; } = "public";

	public string? Input { get; init; } = null;
}

/// <summary>
/// This should raise an exception and/or a test failure (user's choice) if
/// * a property is asserted which is not actually a dependency
/// * a property which is a dependency is not asserted
/// The *must* assert all of the immediate dependencies, and *may* assert chained/indirect dependencies.
///
/// This attribute applies to properties, but can also be used on methods which cause properties
/// to be generated (such as the <see cref="ComputedPropertyAttribute"/>).
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class AssertDependsOnAttribute : Attribute
{
	public AssertDependsOnAttribute(params string[] dependsOnPropertyNames) { }

	public AssertDependsOnAttribute(NothingType nothing) : this() { }

	public const NothingType Nothing = default;

	public enum NothingType { }
}

public interface IComputation<TInput, TResult> where TInput : IEquatable<TInput>
{
	TInput Input();

	TResult Compute();
}

public sealed class Computer<TInput, TResult> where TInput : IEquatable<TInput>
{
	private (TInput input, TResult result)? current = null;

	public TResult RecomputeIfStale(IComputation<TInput, TResult> computation)
	{
		var input = computation.Input();
		if (current.HasValue && EqualityComparer<TInput>.Default.Equals(input, current.Value.input))
		{
			return current.Value.result;
		}

		var result = computation.Compute();
		current = (input, result);
		return result;
	}
}
