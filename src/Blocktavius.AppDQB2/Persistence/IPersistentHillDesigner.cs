using Blocktavius.AppDQB2.ScriptNodes.HillDesigners;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Blocktavius.AppDQB2.Persistence;

/// <summary>
/// If you implement this interface, you must also add a <see cref="PersistentHillDesignerAttribute"/>.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type", IgnoreUnrecognizedTypeDiscriminators = false)]
public interface IPersistentHillDesigner
{
	bool TryDeserializeV1(ScriptDeserializationContext context, out IHillDesigner designer);
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class PersistentHillDesignerAttribute : Attribute
{
	/// <summary>
	/// A sufficiently unique constant to be used as the JSON type discriminator.
	/// </summary>
	public required string Discriminator { get; init; }
}
