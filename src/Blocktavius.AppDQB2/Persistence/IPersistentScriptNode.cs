using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Blocktavius.AppDQB2.Persistence;

/// <summary>
/// If you implement this interface, you must also add a <see cref="PersistentScriptNodeAttribute"/>.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type", IgnoreUnrecognizedTypeDiscriminators = false)]
interface IPersistentScriptNode
{
	bool TryDeserializeV1(out ScriptNodeVM node);
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
sealed class PersistentScriptNodeAttribute : Attribute
{
	/// <summary>
	/// A sufficiently unique constant to be used as the JSON type discriminator.
	/// </summary>
	public required string Discriminator { get; init; }
}
