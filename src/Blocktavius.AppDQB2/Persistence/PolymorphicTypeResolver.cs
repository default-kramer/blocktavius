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
		else if (jsonTypeInfo.Type == typeof(IPersistentHillDesigner))
		{
			jsonTypeInfo.PolymorphismOptions = jsonTypeInfo.PolymorphismOptions ?? new();
			foreach (var hillType in KnownHillDesignerTypes)
			{
				jsonTypeInfo.PolymorphismOptions.DerivedTypes.Add(new JsonDerivedType(hillType.ObjectType, hillType.Attr.Discriminator));
			}
		}

		return jsonTypeInfo;
	}



	private static IReadOnlyList<ScriptNodeType> KnownScriptNodeTypes = DiscoverScriptNodeTypes().ToList();

	private static IEnumerable<ScriptNodeType> DiscoverScriptNodeTypes() =>
		DiscoverTypes<PersistentScriptNodeAttribute>(typeof(IPersistentScriptNode))
		.Select(x => new ScriptNodeType { ObjectType = x.type, Attr = x.attr });

	private static IReadOnlyList<HillDesignerType> KnownHillDesignerTypes = DiscoverHillDesignerTypes().ToList();

	private static IEnumerable<HillDesignerType> DiscoverHillDesignerTypes() =>
		DiscoverTypes<PersistentHillDesignerAttribute>(typeof(IPersistentHillDesigner))
		.Select(x => new HillDesignerType { ObjectType = x.type, Attr = x.attr });

	private static IEnumerable<(Type type, TAttr attr)> DiscoverTypes<TAttr>(Type interfaceType) where TAttr : System.Attribute
	{
		var types = AppDomain.CurrentDomain.GetAssemblies()
			.SelectMany(asm => asm.GetTypes())
			.Where(t => t.IsClass && !t.IsAbstract && interfaceType.IsAssignableFrom(t))
			.ToList();

		foreach (var type in types)
		{
			var attr = type.GetCustomAttribute<TAttr>(inherit: false);
			if (attr == null)
			{
				// TODO log or throw... or just let the serialization mechanism throw?
				System.Diagnostics.Debugger.Break();
			}
			else
			{
				yield return (type, attr);
			}
		}
	}

	sealed record ScriptNodeType
	{
		/// <summary>
		/// Must be non-abstract and implement <see cref="IPersistentScriptNode"/>
		/// </summary>
		public required Type ObjectType { get; init; }

		public required PersistentScriptNodeAttribute Attr { get; init; }
	}

	sealed record HillDesignerType
	{
		/// <summary>
		/// Must be non-abstract and implement <see cref="IPersistentHillDesigner"/>
		/// </summary>
		public required Type ObjectType { get; init; }

		public required PersistentHillDesignerAttribute Attr { get; init; }
	}
}
