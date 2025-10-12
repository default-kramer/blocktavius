using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace Blocktavius.AppDQB2.ScriptNodes.HillDesigners;

public abstract class HillType
{
	public abstract IHillDesigner CreateNewDesigner();
	public abstract string DisplayName { get; }


	private static readonly List<HillType> hillTypes = new();


	sealed class SimpleHillType<T> : HillType where T : IHillDesigner, new()
	{
		public override IHillDesigner CreateNewDesigner() => new T();
		public override string DisplayName => typeof(T).Name;
	}

	private static void Register<T>() where T : IHillDesigner, new()
	{
		hillTypes.Add(new SimpleHillType<T>());
	}

	static HillType()
	{
		Register<WinsomeHillDesigner>();
		Register<AdamantHillDesigner>();
		Register<PlainHillDesigner>();
		Register<CornerPusherHillDesigner>();
		Register<BubblerHillDesigner>();
	}

	public sealed class PropGridItemsSource : IItemsSource
	{
		public ItemCollection GetValues()
		{
			var items = new ItemCollection();
			foreach (var hillType in hillTypes)
			{
				items.Add(hillType, hillType.DisplayName);
			}
			return items;
		}
	}
}
