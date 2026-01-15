using Blocktavius.AppDQB2.Persistence.V1;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace Blocktavius.AppDQB2.ScriptNodes;

sealed class AreaDefinerVM : ViewModelBase
{
	public void Load(RectV1 rect)
	{
		RectAreaBeginX = rect.X0;
		RectAreaBeginZ = rect.Z0;
		RectAreaEndX = rect.X1;
		RectAreaEndZ = rect.Z1;
	}

	public RectV1? RebuildCustomRect()
	{
		if (RectAreaBeginX.HasValue && RectAreaBeginZ.HasValue && RectAreaEndX.HasValue && RectAreaEndZ.HasValue)
		{
			return new RectV1()
			{
				X0 = RectAreaBeginX.Value,
				Z0 = RectAreaBeginZ.Value,
				X1 = RectAreaEndX.Value,
				Z1 = RectAreaEndZ.Value,
			};
		}
		return null;
	}

	private IAreaVM? area;
	[Category("Area")]
	[ItemsSource(typeof(Global.AreasItemsSource))]
	public IAreaVM? Area
	{
		get => area;
		set
		{
			if (ChangeProperty(ref area, value) && value != null)
			{
				RectAreaBeginX = null;
				RectAreaBeginZ = null;
				RectAreaEndX = null;
				RectAreaEndZ = null;
			}
		}
	}

	private int? _beginX;
	[Category("Area")]
	public int? RectAreaBeginX
	{
		get => _beginX;
		set
		{
			if (ChangeProperty(ref _beginX, value) && value.HasValue)
			{
				Area = null;
			}
		}
	}

	private int? _beginZ;
	[Category("Area")]
	public int? RectAreaBeginZ
	{
		get => _beginZ;
		set
		{
			if (ChangeProperty(ref _beginZ, value) && value.HasValue)
			{
				Area = null;
			}
		}
	}

	private int? _endX;
	[Category("Area")]
	public int? RectAreaEndX
	{
		get => _endX;
		set
		{
			if (ChangeProperty(ref _endX, value) && value.HasValue)
			{
				Area = null;
			}
		}
	}

	private int? _endZ;
	[Category("Area")]
	public int? RectAreaEndZ
	{
		get => _endZ;
		set
		{
			if (ChangeProperty(ref _endZ, value) && value.HasValue)
			{
				Area = null;
			}
		}
	}
}
