using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Blocktavius.AppDQB2.PropGridEditors;

/// <summary>
/// Dialog window that allows you to choose a block.
/// </summary>
public partial class BlockSelectorWindow : Window
{
	public BlockSelectorWindow()
	{
		InitializeComponent();

		lvBlocks.SetBinding(ListView.ItemsSourceProperty, nameof(Viewmodel.Blocks));
		lvBlocks.SetBinding(ListView.SelectedItemProperty, nameof(Viewmodel.SelectedBlock));
		lvBlocks.DisplayMemberPath = nameof(BlockVM.DisplayName);

		tabControl.SetBinding(TabControl.SelectedIndexProperty, nameof(Viewmodel.SelectedTabIndex));
	}

	private void btnChoose_Click(object sender, RoutedEventArgs e)
	{
		this.DialogResult = true;
		this.Close();
	}

	internal sealed class Viewmodel : ViewModelBase
	{
		public required IReadOnlyList<BlockVM> Blocks { get; init; }

		private BlockVM? selectedBlock;
		public BlockVM? SelectedBlock
		{
			get => selectedBlock;
			set => ChangeProperty(ref selectedBlock, value);
		}

		const int TabIndex_Blocks = 0;
		const int TabIndex_Mottlers = 1;
		private int selectedTabIndex = TabIndex_Blocks;
		public int SelectedTabIndex
		{
			get => selectedTabIndex;
			set => ChangeProperty(ref selectedTabIndex, value);
		}

		public void Initialize(IBlockProviderVM? currentValue)
		{
			if (currentValue is BlockVM blockVM)
			{
				SelectedBlock = blockVM;
				SelectedTabIndex = TabIndex_Blocks;
			}
		}

		public IBlockProviderVM? SelectedProvider()
		{
			return SelectedTabIndex switch
			{
				TabIndex_Blocks => SelectedBlock,
				TabIndex_Mottlers => null, // TODO
				_ => null,
			};
		}
	}
}
