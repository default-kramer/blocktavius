using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace Blocktavius.AppDQB2.ScriptNodes.CliffDesigners;

public abstract class CliffType
{
	public abstract ICliffDesigner CreateNewDesigner();
	public abstract string DisplayName { get; }


	private static readonly List<CliffType> cliffTypes = new();


	sealed class SimpleCliffType<T> : CliffType where T : ICliffDesigner, new()
	{
		public override ICliffDesigner CreateNewDesigner() => new T();
		public override string DisplayName => typeof(T).Name;
	}

	private static void Register<T>() where T : ICliffDesigner, new()
	{
		cliffTypes.Add(new SimpleCliffType<T>());
	}

	public static CliffType? FindTypeOf(ICliffDesigner designer)
	{
		var expectType = designer.GetType();
		return cliffTypes.FirstOrDefault(t => t.CreateNewDesigner().GetType() == expectType);
	}

	static CliffType()
	{
		Register<FacileCliffDesigner>();
	}

	public sealed class PropGridItemsSource : IItemsSource
	{
		public ItemCollection GetValues()
		{
			var items = new ItemCollection();
			foreach (var cliffType in cliffTypes)
			{
				items.Add(cliffType, cliffType.DisplayName);
			}
			return items;
		}
	}
}
