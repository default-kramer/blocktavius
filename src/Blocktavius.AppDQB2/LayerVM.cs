using Blocktavius.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace Blocktavius.AppDQB2;

interface ILayerVM : IAreaVM
{
	string LayerName { get; }
	bool IsVisible { get; set; }

	IEnumerable<ExternalImageVM> ExternalImage { get; }
}
