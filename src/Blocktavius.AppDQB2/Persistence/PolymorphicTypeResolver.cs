using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;

namespace Blocktavius.AppDQB2.Persistence;


sealed class PolymorphicTypeResolver : NullablePropertiesNotRequiredResolver
{
	public override JsonTypeInfo GetTypeInfo(Type type, JsonSerializerOptions options)
	{
		JsonTypeInfo jsonTypeInfo = base.GetTypeInfo(type, options);

		if (jsonTypeInfo.Type == typeof(IPersistentScriptNode))
		{
			jsonTypeInfo.PolymorphismOptions = jsonTypeInfo.PolymorphismOptions ?? new();
			foreach (var scriptType in KnownScriptNodeTypes)
			{
				jsonTypeInfo.PolymorphismOptions.DerivedTypes.Add(new JsonDerivedType(scriptType.ObjectType, scriptType.Attr.Discriminator));
			}
		}

		return jsonTypeInfo;
	}

	sealed record ScriptNodeType
	{
		/// <summary>
		/// Must be non-abstract and inherit <see cref="IPersistentScriptNode"/>
		/// </summary>
		public required Type ObjectType { get; init; }

		public required PersistentScriptNodeAttribute Attr { get; init; }
	}

	private static IReadOnlyList<ScriptNodeType> KnownScriptNodeTypes = DiscoverScriptNodeTypes().ToList();

	private static IEnumerable<ScriptNodeType> DiscoverScriptNodeTypes()
	{
		var types = AppDomain.CurrentDomain.GetAssemblies()
			.SelectMany(asm => asm.GetTypes())
			.Where(t => t.IsClass && !t.IsAbstract)
			.Where(t => typeof(IPersistentScriptNode).IsAssignableFrom(t))
			.ToList();

		foreach (var type in types)
		{
			var attr = type.GetCustomAttribute<PersistentScriptNodeAttribute>(inherit: false);
			if (attr == null)
			{
				// TODO log or throw... or just let the serialization mechanism throw?
			}
			else
			{
				yield return new ScriptNodeType { ObjectType = type, Attr = attr };
			}
		}
	}
}