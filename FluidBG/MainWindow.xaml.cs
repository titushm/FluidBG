using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Navigation;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Microsoft.WindowsAPICodePack.Dialogs;
using Button = System.Windows.Controls.Button;
using Directory = System.IO.Directory;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using MessageBox = System.Windows.MessageBox;

namespace FluidBG {
	public partial class MainWindow {
		private static readonly HttpClient HttpClient = new();
		private IntervalTimer Timer;  
		private NotifyIcon NotifyIcon = new();
		public MainWindow() {
            AppDomain.CurrentDomain.UnhandledException += AppDomain_UnhandledException;
			Utils.ValidateConfig();
            Utils.Log("MainWindow Called");
			InitializeComponent();
			if (Utils.GetConfigProperty<bool>("startHidden")) {
				Hide();
			}
			using (Stream iconStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("FluidBG.FluidBG_Disabled.ico"))
					NotifyIcon.Icon = new Icon(iconStream);
			NotifyIcon.Visible = true;
			NotifyIcon.Text = "FluidBG";
			NotifyIcon.ContextMenuStrip = new ContextMenuStrip();
			NotifyIcon.ContextMenuStrip.Items.Add("Change Now").Click += (sender, e) => { ChangeRandomWallpaper(); };
			NotifyIcon.ContextMenuStrip.Items.Add("-");
			NotifyIcon.ContextMenuStrip.Items.Add("Exit").Click += (sender, e) => { System.Windows.Application.Current.Shutdown(); };
			NotifyIcon.Click += (sender, e) => {
				if (((MouseEventArgs)e).Button != MouseButtons.Left) return;
				Show();
				if (WindowState == WindowState.Minimized) {
					WindowState = WindowState.Normal;
				}
				Activate();
			};
            bool enabled = Utils.GetConfigProperty<bool>("enabled");
            int intervalIndex = Utils.GetConfigProperty<int>("intervalIndex");
            Timer = new IntervalTimer(Constants.ComboBoxSecondIntervals[intervalIndex], ChangeRandomWallpaper);
            if (enabled) {
                Timer.Start();
                using (Stream? iconStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("FluidBG.FluidBG.ico"))
	                if (iconStream != null)
		                NotifyIcon.Icon = new Icon(iconStream);
                NotifyIcon.Text = "FluidBG | Change at " + Timer.QueryNextTickTimestamp();
            }
        }

        private void AppDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e) {
            Utils.LogToFile(e.ExceptionObject.ToString());
            MessageBox.Show(e.ExceptionObject.ToString());
        }

        private void LoadSettings(){
	        if (Constants.StartupRegistryKey.GetValue("FluidBG") != null) StartupToggleButton.IsChecked = true;
	        if (Utils.GetConfigProperty<bool>("spotlight")) SpotlightToggleButton.IsChecked = true;
	        if (Utils.GetConfigProperty<bool>("startHidden")) StartHiddenButton.IsChecked = true;
	        if (Utils.GetConfigProperty<bool>("tileImage")) TileToggleButton.IsChecked = true;
	        int modeIndex = Utils.GetConfigProperty<int>("wallpaperModeIndex");
	        WallpaperModeComboBox.SelectedIndex = modeIndex;
	        // int themeIndex = Utils.GetConfigProperty<int>("appThemeIndex"); //TODO: add dark mode
	        // ThemeComboBox.SelectedIndex = themeIndex;
        }

        private void ApplyTheme(){
	   //      int theme = Utils.GetConfigProperty<int>("appThemeIndex"); //TODO: add dark mode
	   //      if (theme == 0) theme = Utils.GetWindowsTheme();
	   //      switch (theme) {
				// case 1:
				// 	FluidBGWindow.Background = Brushes.Black;
				// 	
				// 	break;
				// case 2:
				// 	break;
	        // }
        }

        private async void CheckUpdate() {
			await Task.Run(() => {
				try {
                    Task<HttpResponseMessage> response = HttpClient.GetAsync(Constants.GithubRepoUrl + "/releases/latest");
                    string redirectUrl = response.Result.RequestMessage.RequestUri.ToString();
					Version latestVersion = Version.Parse(redirectUrl.Split('/').Last());
					if (latestVersion > Constants.Version) {
						System.Windows.Application.Current.Dispatcher.Invoke((Action)delegate {
							MessageBoxResult updateMessageResult = MessageBox.Show(
								$"Update available: v{latestVersion}\nWould you like to go to the github repo to update",
								"FluidBG", MessageBoxButton.YesNo);
							if (updateMessageResult == MessageBoxResult.Yes) {
								Process.Start(new ProcessStartInfo {
									FileName = Constants.GithubRepoUrl + "/releases/latest",
									UseShellExecute = true
								});
							}
						});
					}
				}
				catch{
					// ignored
				}
			});
		}

		private void UpdateNextChange() {
			if (Timer.Timer != null && Timer.Timer.IsEnabled) {
				NextChangeTextBlock.Text = Timer.QueryNextTickTimestamp();
				NotifyIcon.Text = "FluidBG | Change at " + Timer.QueryNextTickTimestamp();
			} else {
				NextChangeTextBlock.Text = "Never";
				NotifyIcon.Text = "FluidBG";
			}

		}
        private void ChangeRandomWallpaper() {
			UpdateNextChange();
			Utils.Log("Tick occured");
			
			string[] sourcePaths = Utils.GetConfigProperty<string[]>("sourcePaths");
			List<string> configSourcePaths = sourcePaths.ToList();
			bool spotlight = Utils.GetConfigProperty<bool>("spotlight");
			string spotlightImagePath = Constants.Paths.DataFolder + "\\spotlight.jpg";
			string[] imageDetails = {"Spotlight image"};
			if (spotlight) {
				imageDetails = Utils.GetSpotlightImage();
				configSourcePaths.Add(spotlightImagePath);
	        }
            if (configSourcePaths.Count == 0) return;
			Random random = new Random();
			int randomIndex = random.Next(0, configSourcePaths.Count);
			string randomPath = configSourcePaths[randomIndex];
			bool isDir = Directory.Exists(randomPath);
			bool isFile = File.Exists(randomPath);
			if (!isDir && !isFile) return;
			if (isDir) {
				
				string[] files = Directory.GetFiles(randomPath, "*.*")
					.Where(file => Constants.ImageFileTypes.Any(ext => file.EndsWith("." + ext, StringComparison.OrdinalIgnoreCase)))
					.ToArray();
				if (files.Length == 0) return;
				randomIndex = random.Next(0, files.Length);
				randomPath = files[randomIndex];
			}

			int modeIndex = Utils.GetConfigProperty<int>("wallpaperModeIndex");
			int mode = Constants.WallpaperModes[modeIndex];
			bool tile = Utils.GetConfigProperty<bool>("tileImage");
            Wallpaper.Set(randomPath, mode, tile);
			if (HistoryListBox.Items.Count > 1000) {
				HistoryListBox.Items.RemoveAt(HistoryListBox.Items.Count - 1);
			}
			string historyText = randomPath;
			if (randomPath == spotlightImagePath) {
				foreach (var property in imageDetails){
					historyText += " " + property;
				}
            }
			HistoryListBox.Items.Insert(0, new ListBoxItem() {
				Style = (Style)FindResource("Fluid:ListBoxItem"),
				Content = new DockPanel() {
					Children = {
						new TextBlock() {
							Text = historyText,
							Padding = new Thickness(0,0,10,0)
						},
						new TextBlock() {
							HorizontalAlignment = HorizontalAlignment.Right,
							Text = DateTime.Now.ToString("HH:mm:ss")
						}
					}
				}
			});
		}

		private void PopulateSourceList() {
			SourceListBox.Items.Clear();
			string[] configSourcePaths = Utils.GetConfigProperty<string[]>("sourcePaths");
			foreach (string path in configSourcePaths) {
				bool isDir = Directory.Exists(path);
				bool isFile = File.Exists(path);
				if (!isDir && !isFile) continue;
				SourceListBox.Items.Insert(0, new ListBoxItem() {
					Style = (Style)FindResource("Fluid:ListBoxItem"),
					Content = new TextBlock() {
						Text = path
					}
				});
			}
		}

		private void PopulateIntervals() {
			decimal interval = Utils.GetConfigProperty<decimal>("interval");
			int intervalIndex = Utils.GetConfigProperty<int>("intervalIndex");
			bool enabled = Utils.GetConfigProperty<bool>("enabled");
			if (interval == default) {
				interval = 1;
				Utils.SetConfigProperty("interval", new JValue(interval));
			}
			IntervalDecimalUpDown.Value = interval;
			IntervalComboBox.SelectedIndex = intervalIndex;
			EnabledToggleButton.IsChecked = enabled;
		}

		private string GetSelectedSourcePath() {
			ListBoxItem sourceListBox = (ListBoxItem)SourceListBox.SelectedItem;
			TextBlock listItemText = (TextBlock)sourceListBox.Content;
			string selectedPath = listItemText.Text;
			return selectedPath;
		}
		

		private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e) {
			string url = e.Uri.AbsoluteUri;
			Process.Start(new ProcessStartInfo {
				FileName = url,
				UseShellExecute = true
			});
			e.Handled = true;
		}

		private void RemoveImagePathButton_Click(object sender, RoutedEventArgs e) {
			if (SourceListBox.SelectedIndex == -1) return;
			string selectedPath = GetSelectedSourcePath();
			string[] configSourcePaths = Utils.GetConfigProperty<string[]>("sourcePaths");
			JArray modifiedArray = JArray.FromObject(configSourcePaths);
			int index = modifiedArray.IndexOf(modifiedArray.FirstOrDefault(x => x.ToString() == selectedPath));
			modifiedArray.RemoveAt(index);
			Utils.SetConfigProperty("sourcePaths", modifiedArray);
			PopulateSourceList();
		}

		private void OpenImagePathButton_Click(object sender, RoutedEventArgs e) {
			if (SourceListBox.SelectedIndex == -1) return;
			string selectedPath = GetSelectedSourcePath();
			if (File.Exists(selectedPath) || Directory.Exists(selectedPath)) {
                Process.Start("explorer.exe", $"/select,\"{selectedPath}\"");
				return;
            }
			MessageBox.Show("Path does not exist");
			
		}

		private void Window_Loaded(object sender, RoutedEventArgs e) {
			if (!File.Exists(Constants.Paths.LogFile)) {
				File.Create(Constants.Paths.LogFile).Close();
			}
			Utils.Log("Window_Loaded");
			VersionTextBlock.Text = $"v{Constants.Version}";


			if (!File.Exists(Constants.Paths.ConfigFile)) {
				File.Create(Constants.Paths.ConfigFile).Close();
				File.WriteAllText(Constants.Paths.ConfigFile, "{}");
			}
			
			Utils.ClearLogFile();
			LoadSettings();
			PopulateSourceList();
			PopulateIntervals();
			UpdateNextChange();
			CheckUpdate();
			ApplyTheme();
			Utils.Log("Finished initial code");
		}

		private void LogButton_Click(object sender, RoutedEventArgs e) {
			Process.Start("notepad.exe", Constants.Paths.LogFile);
		}

        private void SpotlightButton_Click(object sender, RoutedEventArgs e) {
			Utils.SetConfigProperty("spotlight", new JValue(SpotlightToggleButton.IsChecked));
        }

        private void ClearLogButton_Click(object sender, RoutedEventArgs e) {
			Utils.ClearLogFile();
		}

		private void ChangeNowButton_Click(object sender, RoutedEventArgs e) {
			ChangeRandomWallpaper();
		}

		private void SetHistoryWallpaperButton_Click(object sender, RoutedEventArgs e) {
			if (HistoryListBox.SelectedIndex == -1) return;
			string path = ((TextBlock)((DockPanel)((ListBoxItem)HistoryListBox.Items[HistoryListBox.SelectedIndex]).Content).Children[0]).Text;
			if (!File.Exists(path)) {
				MessageBox.Show("File does not exist");
				return;
			}
			Wallpaper.Set(path);
		}

		private void OpenHistoryImageButton_Click(object sender, RoutedEventArgs e) {
			if (HistoryListBox.SelectedIndex == -1) return;
			string path = ((TextBlock)((DockPanel)((ListBoxItem)HistoryListBox.Items[HistoryListBox.SelectedIndex]).Content).Children[0]).Text;
			if (!File.Exists(path)) {
				MessageBox.Show("File does not exist");
				return;
			}
			Process.Start(new ProcessStartInfo {
				FileName = path,
				UseShellExecute = true
			});
			e.Handled = true;
		}

		private void SelectPathButton_Click(object sender, RoutedEventArgs e) {
			Button button = (Button)sender;
			bool isFolder = bool.Parse(button.Tag.ToString());
			CommonOpenFileDialog fileDialog = new CommonOpenFileDialog() {
				IsFolderPicker = isFolder,
				InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
			};
			if (!isFolder)
				fileDialog.Filters.Add(new CommonFileDialogFilter("Image Files", "*.jpg;*.jpeg;*.png;*.bmp;*.tiff"));
			if (fileDialog.ShowDialog() != CommonFileDialogResult.Ok) return;
			string selectedPath = fileDialog.FileName;
			string[] configSourcePaths = Utils.GetConfigProperty<string[]>("sourcePaths");
			if (configSourcePaths == default(string[])) configSourcePaths = new string[0];
			JArray modifiedArray = JArray.FromObject(configSourcePaths);
			modifiedArray.Add(new JValue(selectedPath));
			Utils.SetConfigProperty("sourcePaths", modifiedArray);
			PopulateSourceList();
		}

        private void IntervalUpDown_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e) {
			if (IntervalComboBox == null || IntervalComboBox.SelectedIndex == -1 ||
			    IntervalDecimalUpDown.Value == null) return;
			if (Convert.ToDouble(e.NewValue) * Constants.ComboBoxSecondIntervals[IntervalComboBox.SelectedIndex] * 1000 >
			    Int32.MaxValue) {
				MessageBox.Show("Interval must be less than 4 weeks");
				return;
			}

			Utils.SetConfigProperty("interval", new JValue(e.NewValue));
			Timer.ChangeInterval(Convert.ToDouble(e.NewValue) * Constants.ComboBoxSecondIntervals[IntervalComboBox.SelectedIndex]);
		}

		private void IntervalUnit_Changed(object sender, SelectionChangedEventArgs e) {
			ComboBoxItem selectedItem = (ComboBoxItem)e.AddedItems[0];
			int selectedIndex = IntervalComboBox.Items.IndexOf(selectedItem);
			if (IntervalComboBox.SelectedIndex == -1 || IntervalDecimalUpDown.Value == null) return;
			if ((Convert.ToDouble(IntervalDecimalUpDown.Value.Value) * Constants.ComboBoxSecondIntervals[selectedIndex]) * 1000 >
			    Int32.MaxValue) {
				MessageBox.Show("Interval must be less than 4 weeks");
				IntervalDecimalUpDown.Value = 1;
				return;
			}

			Utils.SetConfigProperty("intervalIndex", new JValue(selectedIndex));
			Timer.ChangeInterval(Convert.ToDouble(IntervalDecimalUpDown.Value.Value) * Constants.ComboBoxSecondIntervals[selectedIndex]);
		}

		private void EnabledButton_OnClick(object sender, RoutedEventArgs e) {
			Utils.SetConfigProperty("enabled", new JValue(EnabledToggleButton.IsChecked));
			Stream iconStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("FluidBG.FluidBG_Disabled.ico");

            if (EnabledToggleButton.IsChecked == true) {
				Timer.Start();
                iconStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("FluidBG.FluidBG.ico");
            }
            else {
				Timer.Stop();				
			}
            NotifyIcon.Icon = new Icon(iconStream);
            UpdateNextChange();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
			e.Cancel = true;
			Hide();
		}

		private void StartupButton_Click(object sender, RoutedEventArgs e) {
			if (StartupToggleButton.IsChecked == true) {
				Constants.StartupRegistryKey.SetValue("FluidBG", System.Windows.Forms.Application.ExecutablePath);
			} else {
				Constants.StartupRegistryKey.DeleteValue("FluidBG", false);
			}
		}
		
		private void StartHiddenButton_Click(object sender, RoutedEventArgs e) {
			Utils.SetConfigProperty("startHidden", new JValue(StartHiddenButton.IsChecked));
		}

		private void WallpaperModeComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e){
			Utils.SetConfigProperty("wallpaperModeIndex", new JValue(WallpaperModeComboBox.SelectedIndex));
		}

		private void TileToggleButton_OnClick(object sender, RoutedEventArgs e){
			Utils.SetConfigProperty("tileImage", new JValue(TileToggleButton.IsChecked));
		}

		// private void ThemeComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e){ //TODO: add dark mode
		// 	Utils.SetConfigProperty("appThemeIndex", new JValue(ThemeComboBox.SelectedIndex));
		// 	ApplyTheme();
		// }
	}
}