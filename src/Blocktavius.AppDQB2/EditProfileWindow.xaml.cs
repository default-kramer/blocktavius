using Blocktavius.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
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

namespace Blocktavius.AppDQB2;

/// <summary>
/// Interaction logic for EditProfileWindow.xaml
/// </summary>
public partial class EditProfileWindow : Window
{
	public EditProfileWindow()
	{
		InitializeComponent();
	}

	internal bool? ShowDialog(ProfileSettings profile, out ProfileSettings updatedSettings)
	{
		var vm = new EditProfileVM(profile);
		this.DataContext = vm;

		var result = this.ShowDialog();
		if (result == true)
		{
			updatedSettings = vm.ToProfile();
		}
		else
		{
			updatedSettings = profile;
		}
		return result;
	}

	private void Confirm_Click(object sender, RoutedEventArgs e)
	{
		DialogResult = true;
		this.Close();
	}

	private void Cancel_Click(object sender, RoutedEventArgs e)
	{
		DialogResult = false;
		this.Close();
	}

	sealed class EditProfileVM : ViewModelBase
	{
		private const string AllProps = ""; // INPC notification for all properties

		private ProfileSettings loadedSettings;
		public EditProfileVM(ProfileSettings settings)
		{
			loadedSettings = settings;
			this.Reload(settings);
		}

		public ObservableCollection<SaveSlotVM> SaveSlots { get; } = new();

		private string _sdFullPath = "";
		public string SDFullPath
		{
			get => _sdFullPath;
			private set => ChangeProperty(ref _sdFullPath, value);
		}

		private bool _createBackups = true;
		public bool CreateBackups
		{
			get => _createBackups;
			set => ChangeProperty(ref _createBackups, value, AllProps);
		}

		public Visibility BackupLocationVisibility => CreateBackups ? Visibility.Visible : Visibility.Collapsed;
		public Visibility BackupWarningVisibility => (!CreateBackups && SaveSlots.Any(slot => slot.IsWritable)) ? Visibility.Visible : Visibility.Collapsed;
		public Visibility NoSaveSlotsWarningVisibility => SaveSlots.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

		public string BackupLocationText { get; private set; } = "";

		public void OnChooseNewSD(DirectoryInfo sdDir)
		{
			var profile = ProfileSettings.LoadOrCreate(sdDir);
			Reload(profile);
			SDFullPath = sdDir.FullName;
		}

		internal void OnVisibilityChanged(SaveSlotVM vm) => OnPropertyChanged(AllProps);

		private void Reload(ProfileSettings settings)
		{
			loadedSettings = settings;
			var profile = settings.ToJsonModel();

			CreateBackups = profile.SaveBackups.GetValueOrDefault(true);
			if (settings.TryFindSD(out var sd))
			{
				SDFullPath = sd.FullName;
			}

			SaveSlots.Clear();
			foreach (var slot in settings.SaveSlots)
			{
				SaveSlots.Add(new SaveSlotVM()
				{
					OriginalSlot = slot,
					Parent = this,
					IsWritable = slot.IsWritable,
					Name = slot.Name,
				});
			}

			BackupLocationText = $"Backups will be saved to {BackupDir(settings).FullName}";
			OnPropertyChanged(AllProps);
		}

		private static DirectoryInfo BackupDir(ProfileSettings settings) => settings.BackupDir ?? settings.GetDefaultBackupDir();

		public ProfileSettings ToProfile()
		{
			var newSettings = new ProfileSettings
			{
				ConfigDir = loadedSettings.ConfigDir,
				ConfigFile = loadedSettings.ConfigFile,
				ProfileId = loadedSettings.ProfileId,
				VerificationHash = loadedSettings.VerificationHash,
				BackupDir = this.CreateBackups ? BackupDir(loadedSettings) : null,
				SaveSlots = this.SaveSlots.Select(s => s.MakeModified()).ToList(),
			};

			return newSettings;
		}
	}

	sealed class SaveSlotVM : ViewModelBase
	{
		public required EditProfileVM Parent { get; init; }
		public required ProfileSettings.SaveSlot OriginalSlot { get; init; }
		public ProfileSettings.SaveSlot MakeModified() => OriginalSlot.MakeModified(Name, IsWritable);

		private string _name = "";
		public string Name
		{
			get => _name;
			set => ChangeProperty(ref _name, value);
		}

		private bool _isWritable = false;
		public bool IsWritable
		{
			get => _isWritable;
			set
			{
				if (ChangeProperty(ref _isWritable, value))
				{
					Parent.OnVisibilityChanged(this);
				}
			}
		}
	}
}
