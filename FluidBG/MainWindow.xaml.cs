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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.WindowsAPICodePack.Dialogs;
using Microsoft.Win32;
using Button = System.Windows.Controls.Button;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using MessageBox = System.Windows.MessageBox;
using Microsoft.WindowsAPICodePack.Shell.Interop;

namespace FluidBG {
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window {
		private static readonly Version VERSION = new Version(1, 0, 8);
		private static readonly string GITHUB_REPO_URL = "https://github.com/titushm/FluidBG";
		private static RegistryKey STARTUP_REGISTRY_KEY = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
		private static readonly HttpClient httpClient = new();
		int[] comboBoxSecondIntervals = { 1, 60, 3600, 86400, 604800 };
		private IntervalTimer timer;  
		private NotifyIcon notifyIcon = new();

		private static class Paths {
			public static readonly string DataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\titushm\\FluidBG";
			public static readonly string LogFile = $"{DataFolder}\\log.tmp";
			public static readonly string ConfigFile = $"{DataFolder}\\config.json";
		}

		private Dictionary<int, int> wallpaperModes = new Dictionary<int, int> {
            {0, 10},
            {1, 6},
            {2, 2},
            {3, 0},
            {4, 0},
			{5, 22}
        };

		public MainWindow() {
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(AppDomain_UnhandledException);
			ValidateConfig();
            Log("MainWindow Called");
			InitializeComponent();
			bool startHidden = GetConfigProperty<bool>("startHidden");
			if (startHidden) {
				Hide();
			}
			try {
				using (Stream iconStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("FluidBG.FluidBG_Disabled.ico"))
				notifyIcon.Icon = new Icon(iconStream);
				notifyIcon.Visible = true;
				notifyIcon.Text = "FluidBG";
				notifyIcon.ContextMenuStrip = new ContextMenuStrip();
				notifyIcon.ContextMenuStrip.Items.Add("Change Now").Click += (sender, e) => { ChangeRandomWallpaper(); };
				notifyIcon.ContextMenuStrip.Items.Add("-");
				notifyIcon.ContextMenuStrip.Items.Add("Exit").Click += (sender, e) => { System.Windows.Application.Current.Shutdown(); };
				notifyIcon.Click += (sender, e) => {
					if (((MouseEventArgs)e).Button == MouseButtons.Left) {
						Show();
						if (WindowState == WindowState.Minimized) {
							WindowState = WindowState.Normal;
						}
						Activate();
                    }
				};
			}
			catch (Exception e) {
				MessageBox.Show(e.ToString());
			}
            bool enabled = GetConfigProperty<bool>("enabled");
            int intervalIndex = GetConfigProperty<int>("intervalIndex");
            timer = new IntervalTimer(comboBoxSecondIntervals[intervalIndex], ChangeRandomWallpaper);
            if (enabled) {
                timer.Start();
                using (Stream iconStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("FluidBG.FluidBG.ico"))
                notifyIcon.Icon = new Icon(iconStream);
				notifyIcon.Text = "FluidBG | Change at " + timer.QueryNextTickTimestamp();
            }
        }

        private void LogToFile(string message) {
            string logPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\log.txt";
            File.WriteAllText(logPath, message);
        }

        private void AppDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e) {
            LogToFile(e.ExceptionObject.ToString());
            MessageBox.Show(e.ExceptionObject.ToString());
        }

        private async void CheckUpdate() {
			await Task.Run(() => {
				try {
                    Task<HttpResponseMessage> response = httpClient.GetAsync(GITHUB_REPO_URL + "/releases/latest");
                    string redirectUrl = response.Result.RequestMessage.RequestUri.ToString();
					Version latestVersion = Version.Parse(redirectUrl.Split('/').Last());
					if (latestVersion > VERSION) {
						System.Windows.Application.Current.Dispatcher.Invoke((Action)delegate {
							MessageBoxResult updateMessageResult = MessageBox.Show(
								$"Update available: v{latestVersion}\nWould you like to go to the github repo to update",
								"FluidBG", MessageBoxButton.YesNo);
							if (updateMessageResult == MessageBoxResult.Yes) {
								Process.Start(new ProcessStartInfo {
									FileName = GITHUB_REPO_URL + "/releases/latest",
									UseShellExecute = true
								});
							}
						});
					}
				}
				catch { }
			});
		}
		
		private void GenerateSpotlightImage() {
            DateTime utcNow = DateTime.UtcNow;
            string time = utcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
            string url = "https://arc.msn.com/v3/Delivery/Placement?pid=209567&fmt=json&rafb=0&ua=WindowsShellClient%2F0&cdm=1&disphorzres=9999&dispvertres=9999&lo=80217&pl=en-US&lc=en-US&ctry=us&time=" + time;
            Task<HttpResponseMessage> response = httpClient.GetAsync(url);
			string responseString = response.Result.Content.ReadAsStringAsync().Result;
			JObject jsonObject = JsonConvert.DeserializeObject<JObject>(responseString);
			Random random = new Random();
			jsonObject["batchrsp"]["items"][0].Remove(); // Remove the first item as it is always the same
            string itemString = jsonObject["batchrsp"]["items"][random.Next(2)]["item"].ToString();
			JObject itemObject = JsonConvert.DeserializeObject<JObject>(itemString);
			string spotlightUrl = itemObject["ad"]["image_fullscreen_001_landscape"]["u"].ToString();
            Task<Stream> stream = httpClient.GetStreamAsync(spotlightUrl);
            string imagePath = Paths.DataFolder + "\\spotlight.jpg";
            using (var fs = new FileStream(imagePath, FileMode.OpenOrCreate)) {
                stream.Result.CopyTo(fs);
            }
			string title = itemObject["ad"]["title_text"]["tx"].ToString();
			string author = itemObject["ad"]["copyright_text"]["tx"].ToString();
            SetConfigProperty("spotlightTitle", new JValue(title));
			SetConfigProperty("spotlightAuthor", new JValue(author));
        }


        private void ChangeRandomWallpaper() {
			updateNextChange();
			Log("Tick occured");
			
			string[] sourcePaths = GetConfigProperty<string[]>("sourcePaths");
			if (sourcePaths == default(string[])) {
				sourcePaths = Array.Empty<string>();
			}
			List<string> configSourcePaths = sourcePaths.ToList();
			bool spotlight = GetConfigProperty<bool>("spotlight");
            string spotlightImagePath = Paths.DataFolder + "\\spotlight.jpg";
			if (spotlight) {
				if (!File.Exists(spotlightImagePath)) {
					GenerateSpotlightImage();
				}
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
				string[] files = Directory.GetFiles(randomPath);
				if (files.Length == 0) return;
				randomIndex = random.Next(0, files.Length);
				randomPath = files[randomIndex];
			}
			int mode = wallpaperModes[WallpaperModeComboBox.SelectedIndex];
			bool tile = WallpaperModeComboBox.Text == "Tile";
            Wallpaper.Set(randomPath, mode, tile);
			if (HistoryListBox.Items.Count > 1000) {
				HistoryListBox.Items.RemoveAt(HistoryListBox.Items.Count - 1);
			}
			string historyText = randomPath;
			if (randomPath == spotlightImagePath) {
                GenerateSpotlightImage();
                string author = GetConfigProperty<string>("spotlightAuthor");
				string title = GetConfigProperty<string>("spotlightTitle");
				historyText = author + " - " + title;
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
			if (!Directory.Exists(Paths.DataFolder)) {
                Directory.CreateDirectory(Paths.DataFolder);
            }
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

		private void updateNextChange() {
			if (timer.Timer.IsEnabled) {
				NextChangeTextBlock.Text = timer.QueryNextTickTimestamp();
				notifyIcon.Text = "FluidBG | Change at " + timer.QueryNextTickTimestamp();
			} else {
				NextChangeTextBlock.Text = "Never";
				notifyIcon.Text = "FluidBG";
            }

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
			if (File.Exists(selectedPath) || Directory.Exists(selectedPath)) {
                Process.Start("explorer.exe", $"/select,\"{selectedPath}\"");
				return;
            }
			MessageBox.Show("Path does not exist");
			
		}

		private void Window_Loaded(object sender, RoutedEventArgs e) {
			if (!File.Exists(Paths.LogFile)) {
				File.Create(Paths.LogFile).Close();
			}
			Log("Window_Loaded");
			VersionTextBlock.Text = $"v{VERSION}";


			if (!File.Exists(Paths.ConfigFile)) {
				File.Create(Paths.ConfigFile).Close();
				File.WriteAllText(Paths.ConfigFile, "{}");
			}
			if (STARTUP_REGISTRY_KEY.GetValue("FluidBG") != null) {
				StartupToggleButton.IsChecked = true;
			}
			if (GetConfigProperty<bool>("spotlight")) {
				SpotlightToggleButton.IsChecked = true;
			}
			if (GetConfigProperty<bool>("startHidden")) {
                StartHiddenButton.IsChecked = true;
            }
			ClearLogFile();
			PopulateSourceList();
			PopulateIntervals();
			updateNextChange();
			CheckUpdate();
			Log("Finished initial code");
		}

		private void LogButton_Click(object sender, RoutedEventArgs e) {
			Process.Start("notepad.exe", Paths.LogFile);
		}

        private void SpotlightButton_Click(object sender, RoutedEventArgs e) {
			SetConfigProperty("spotlight", new JValue(SpotlightToggleButton.IsChecked));
        }

        private void ClearLogButton_Click(object sender, RoutedEventArgs e) {
			ClearLogFile();
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
			timer.ChangeInterval(Convert.ToDouble(e.NewValue) * comboBoxSecondIntervals[IntervalComboBox.SelectedIndex]);
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
			Stream iconStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("FluidBG.FluidBG_Disabled.ico");

            if (EnabledToggleButton.IsChecked == true) {
				timer.Start();
                iconStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("FluidBG.FluidBG.ico");
            }
            else {
				timer.Stop();				
			}
            notifyIcon.Icon = new Icon(iconStream);
            updateNextChange();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
			e.Cancel = true;
			Hide();
		}

		private void StartupButton_Click(object sender, RoutedEventArgs e) {
			if (StartupToggleButton.IsChecked == true) {
				STARTUP_REGISTRY_KEY.SetValue("FluidBG", System.Windows.Forms.Application.ExecutablePath);
			} else {
				STARTUP_REGISTRY_KEY.DeleteValue("FluidBG", false);
			}
		}
		
		private void StartHiddenButton_Click(object sender, RoutedEventArgs e) {
			SetConfigProperty("startHidden", new JValue(StartHiddenButton.IsChecked));
		}
	}
}