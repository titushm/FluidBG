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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.WindowsAPICodePack.Dialogs;
using Microsoft.Win32;
using Button = System.Windows.Controls.Button;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using MessageBox = System.Windows.MessageBox;

namespace FluidBG {
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window {
		private static readonly Version version = new (1, 0, 3);
		private static readonly string githubRepo = "https://github.com/titushm/FluidBG";
		private static RegistryKey startupRegistryKey = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);

		private static readonly HttpClient httpClient = new();
		int[] comboBoxSecondIntervals = { 1, 60, 3600, 86400, 604800 };
		private IntervalTimer timer;

		private static class Paths {
			public static readonly string DataFolder =
				Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\titushm\\FluidBG";

			public static readonly string LogFile = $"{DataFolder}\\log.tmp";
			public static readonly string ConfigFile = $"{DataFolder}\\config.json";
		}

		public MainWindow() {
			Log("MainWindow Called");
			InitializeComponent();
			bool startHidden = GetConfigProperty<bool>("startHidden");
			if (startHidden) {
				Hide();
			}
			try {
				NotifyIcon notifyIcon = new NotifyIcon();
				using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("FluidBG.FluidBG.ico"))
					notifyIcon.Icon = new Icon(stream);
				notifyIcon.Visible = true;
				notifyIcon.ContextMenuStrip = new ContextMenuStrip();
				notifyIcon.ContextMenuStrip.Items.Add("Change Now").Click += (sender, e) => { ChangeRandomWallpaper(); };
				notifyIcon.ContextMenuStrip.Items.Add("-");
				notifyIcon.ContextMenuStrip.Items.Add("Exit").Click += (sender, e) => { System.Windows.Application.Current.Shutdown(); };
				notifyIcon.Click += (sender, e) => {
					if (((MouseEventArgs)e).Button == MouseButtons.Left) {
						Show();
					}
				};
			}
			catch (Exception e) {
				MessageBox.Show(e.ToString());
			}
			Log("NotifyIcon Registered");
		}

		private async void CheckUpdate() {
			await Task.Run(() => {
				try {
					Task<HttpResponseMessage> response = httpClient.GetAsync(githubRepo + "/releases/latest");
					string redirectUrl = response.Result.RequestMessage.RequestUri.ToString();
					Version latestVersion = Version.Parse(redirectUrl.Split('/').Last());
					if (latestVersion > version) {
						System.Windows.Application.Current.Dispatcher.Invoke((Action)delegate {
							MessageBoxResult updateMessageResult = MessageBox.Show(
								$"Update available: v{latestVersion}\nWould you like to go to the github repo to update",
								"FluidBG", MessageBoxButton.YesNo);
							if (updateMessageResult == MessageBoxResult.Yes) {
								Process.Start(new ProcessStartInfo {
									FileName = githubRepo + "/releases/latest",
									UseShellExecute = true
								});
							}
						});
					}
				}
				catch { }
			});
		}
		
		private void ChangeRandomWallpaper() {
			NextChangeTextBlock.Text = timer.QueryNextTickTimestamp();
			Log("Tick occured");
			string[] configSourcePaths = GetConfigProperty<string[]>("sourcePaths");
			if (configSourcePaths == default(string[])) configSourcePaths = new string[0];
			if (configSourcePaths.Length == 0) return;
			Random random = new Random();
			int randomIndex = random.Next(0, configSourcePaths.Length);
			string randomPath = configSourcePaths[randomIndex];
			bool isDir = Directory.Exists(randomPath);
			bool isFile = File.Exists(randomPath);
			if (!isDir && !isFile) return;
			if (isDir) {
				string[] files = Directory.GetFiles(randomPath);
				if (files.Length == 0) return;
				randomIndex = random.Next(0, files.Length);
				randomPath = files[randomIndex];
			}

			Wallpaper.Set(randomPath, WallpaperModeComboBox.SelectedIndex);
			if (HistoryListBox.Items.Count > 1000) {
				HistoryListBox.Items.RemoveAt(HistoryListBox.Items.Count - 1);
			}
			HistoryListBox.Items.Insert(0, new ListBoxItem() {
				Style = (Style)FindResource("Fluid:ListBoxItem"),
				Content = new DockPanel() {
					Children = {
						new TextBlock() {
							Text = randomPath,
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
			string[] configSourcePaths = GetConfigProperty<string[]>("sourcePaths");
			if (configSourcePaths == default(string[])) configSourcePaths = new string[0];
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
			decimal interval = GetConfigProperty<decimal>("interval");
			int intervalIndex = GetConfigProperty<int>("intervalIndex");
			bool enabled = GetConfigProperty<bool>("enabled");
			if (interval == default) {
				interval = 1;
				SetConfigProperty("interval", new JValue(interval));
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

		private void ClearLogFile() {
			File.WriteAllText(Paths.LogFile, "");
			Log("Log file cleared");
		}

		private void ValidateConfig() {
			try {
				string jsonString = File.ReadAllText(Paths.ConfigFile);
				JObject jsonObject = JsonConvert.DeserializeObject<JObject>(jsonString);
			}
			catch {
				File.WriteAllText(Paths.ConfigFile, "{}");
			}
		}

		private void Log(string text) {
			try {
				string timeStamp = DateTime.Now.ToString("HH:mm:ss");
				StreamWriter logWriter = File.AppendText(Paths.LogFile);
				logWriter.Write($"[{timeStamp}] {text}\n");
				logWriter.Close();
			}
			catch { }
		}


		private T GetConfigProperty<T>(string propertyName) {
			ValidateConfig();
			string jsonString = File.ReadAllText(Paths.ConfigFile);
			JObject jsonObject = JsonConvert.DeserializeObject<JObject>(jsonString);
			if (jsonObject.ContainsKey(propertyName)) {
				return jsonObject[propertyName].ToObject<T>();
			}

			return default;
		}

		private void SetConfigProperty(string propertyName, JToken value) {
			ValidateConfig();
			string jsonString = File.ReadAllText(Paths.ConfigFile);
			JObject jsonObject = JsonConvert.DeserializeObject<JObject>(jsonString);
			if (jsonObject.ContainsKey(propertyName)) {
				jsonObject.Property(propertyName).Remove();
			}

			jsonObject.Add(propertyName, value);
			jsonString = JsonConvert.SerializeObject(jsonObject);
			File.WriteAllText(Paths.ConfigFile, jsonString);
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
			string[] configSourcePaths = GetConfigProperty<string[]>("sourcePaths");
			if (configSourcePaths == default(string[])) configSourcePaths = new string[0];
			JArray modifiedArray = JArray.FromObject(configSourcePaths);
			int index = modifiedArray.IndexOf(modifiedArray.FirstOrDefault(x => x.ToString() == selectedPath));
			modifiedArray.RemoveAt(index);
			SetConfigProperty("sourcePaths", modifiedArray);
			PopulateSourceList();
		}

		private void OpenImagePathButton_Click(object sender, RoutedEventArgs e) {
			if (SourceListBox.SelectedIndex == -1) return;
			string selectedPath = GetSelectedSourcePath();
			Process.Start("explorer.exe", $"/select,\"{selectedPath}\"");
		}

		private void Window_Loaded(object sender, RoutedEventArgs e) {
			Log("Window_Loaded");
			VersionTextBlock.Text = $"v{version}";
			if (!Directory.Exists(Paths.DataFolder)) {
				Directory.CreateDirectory(Paths.DataFolder);
			}

			if (!File.Exists(Paths.LogFile)) {
				File.Create(Paths.LogFile).Close();
			}

			if (!File.Exists(Paths.ConfigFile)) {
				File.Create(Paths.ConfigFile).Close();
				File.WriteAllText(Paths.ConfigFile, "{}");
			}

			ValidateConfig();
			bool enabled = GetConfigProperty<bool>("enabled");
			int intervalIndex = GetConfigProperty<int>("intervalIndex");
			bool startHidden = GetConfigProperty<bool>("startHidden");
			StartHiddenButton.IsChecked = startHidden;
			VersionTextBlock.Text = version.ToString();
			timer = new IntervalTimer(comboBoxSecondIntervals[intervalIndex], ChangeRandomWallpaper);
			if (enabled) {
				timer.Start();
			}
			if (startupRegistryKey.GetValue("FluidBG") != null) {
				StartupToggleButton.IsChecked = true;
			}
			ClearLogFile();
			PopulateSourceList();
			PopulateIntervals();
			NextChangeTextBlock.Text = timer.QueryNextTickTimestamp();
			CheckUpdate();
			Log("Finished initial code");
		}

		private void LogButton_Click(object sender, RoutedEventArgs e) {
			Process.Start("notepad.exe", Paths.LogFile);
			throw new Exception("Test exception");
		}

		private void ClearLogButton_Click(object sender, RoutedEventArgs e) {
			ClearLogFile();
		}

		private void ChangeNowButton_Click(object sender, RoutedEventArgs e) {
			ChangeRandomWallpaper();
		}

		private void SetHistoryWallpaperButton_Click(object sender, RoutedEventArgs e) {
			if (HistoryListBox.SelectedIndex == -1) return;
			string path =
				((TextBlock)((DockPanel)((ListBoxItem)HistoryListBox.Items[HistoryListBox.SelectedIndex]).Content)
					.Children[0]).Text;
			Wallpaper.Set(path);
		}

		private void OpenHistoryImageButton_Click(object sender, RoutedEventArgs e) {
			if (HistoryListBox.SelectedIndex == -1) return;
			string path =
				((TextBlock)((DockPanel)((ListBoxItem)HistoryListBox.Items[HistoryListBox.SelectedIndex]).Content)
					.Children[0]).Text;
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
				fileDialog.Filters.Add(new CommonFileDialogFilter("Image Files", "*.jpg;*.jpeg;*.png;*.bmp;*.gif"));
			if (fileDialog.ShowDialog() != CommonFileDialogResult.Ok) return;
			string selectedPath = fileDialog.FileName;
			string[] configSourcePaths = GetConfigProperty<string[]>("sourcePaths");
			if (configSourcePaths == default(string[])) configSourcePaths = new string[0];
			JArray modifiedArray = JArray.FromObject(configSourcePaths);
			modifiedArray.Add(new JValue(selectedPath));
			SetConfigProperty("sourcePaths", modifiedArray);
			PopulateSourceList();
		}

		private void IntervalUpDown_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e) {
			if (IntervalComboBox == null || IntervalComboBox.SelectedIndex == -1 ||
			    IntervalDecimalUpDown.Value == null) return;
			if ((Convert.ToDouble(e.NewValue) * comboBoxSecondIntervals[IntervalComboBox.SelectedIndex]) * 1000 >
			    Int32.MaxValue) {
				MessageBox.Show("Interval must be less than 4 weeks");
				return;
			}

			SetConfigProperty("interval", new JValue(e.NewValue));
			timer.ChangeInterval(Convert.ToDouble(e.NewValue) *
			                     comboBoxSecondIntervals[IntervalComboBox.SelectedIndex]);
		}

		private void IntervalUnit_Changed(object sender, SelectionChangedEventArgs e) {
			ComboBoxItem selectedItem = (ComboBoxItem)e.AddedItems[0];
			int selectedIndex = IntervalComboBox.Items.IndexOf(selectedItem);
			if (IntervalComboBox.SelectedIndex == -1 || IntervalDecimalUpDown.Value == null) return;
			if ((Convert.ToDouble(IntervalDecimalUpDown.Value.Value) * comboBoxSecondIntervals[selectedIndex]) * 1000 >
			    Int32.MaxValue) {
				MessageBox.Show("Interval must be less than 4 weeks");
				IntervalDecimalUpDown.Value = 1;
				return;
			}

			SetConfigProperty("intervalIndex", new JValue(selectedIndex));
			timer.ChangeInterval(Convert.ToDouble(IntervalDecimalUpDown.Value.Value) * comboBoxSecondIntervals[selectedIndex]);
		}

		private void EnabledButton_OnClick(object sender, RoutedEventArgs e) {
			SetConfigProperty("enabled", new JValue(EnabledToggleButton.IsChecked));
			if (EnabledToggleButton.IsChecked == true) {
				timer.Start();
				NextChangeTextBlock.Text = timer.QueryNextTickTimestamp();
			}
			else {
				timer.Stop();
				NextChangeTextBlock.Text = "Never";
			}
		}

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
			e.Cancel = true;
			Hide();
		}

		private void StartupButton_Click(object sender, RoutedEventArgs e) {
			if (StartupToggleButton.IsChecked == true) {
				startupRegistryKey.SetValue("FluidBG", System.Windows.Forms.Application.ExecutablePath);
			} else {
				startupRegistryKey.DeleteValue("FluidBG", false);
			}
		}
		
		private void StartHiddenButton_Click(object sender, RoutedEventArgs e) {
			SetConfigProperty("startHidden", new JValue(StartHiddenButton.IsChecked));
		}
	}
}