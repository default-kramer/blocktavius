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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Blocktavius.AppDQB2
{
	/// <summary>
	/// Interaction logic for ProjectParamsControl.xaml
	/// </summary>
	public partial class ProjectParamsControl : UserControl
	{
		public ProjectParamsControl()
		{
			InitializeComponent();
		}

		private void EditChunkGrid_Click(object sender, RoutedEventArgs e)
		{
			var project = DataContext as ProjectVM;
			if (project == null)
			{
				return;
			}
			EditChunkGridDialog.ShowDialog(project);
		}

		private void ConfirmAndRun_Click(object sender, RoutedEventArgs e)
		{
			MessageBox.Show("WIP, disabled");
			if (1.ToString().Length > 0) { return; }

			var project = DataContext as ProjectVM;
			var target = Global.CurrentProfile?.WritableSaveSlots?.FirstOrDefault();
			if (project == null
				|| target == null
				|| !project.TryRebuildStage(out var stage)
				|| !stage.Saver.CanSave)
			{
				return;
			}

			stage.Saver.Save(target, stage);
		}
	}
}
