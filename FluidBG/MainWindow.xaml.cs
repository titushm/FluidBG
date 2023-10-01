using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Navigation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.WindowsAPICodePack.Dialogs;
using Button = System.Windows.Controls.Button;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using MessageBox = System.Windows.MessageBox;

namespace FluidBG {
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	
	public partial class MainWindow : Window {
		int[] comboBoxSecondIntervals = { 1, 60, 3600, 86400, 604800 };
		private IntervalTimer timer;
		private static class Paths {
			public static readonly string DataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\titushm\\FluidBG";
			public static readonly string LogFile = $"{DataFolder}\\log.tmp";
			public static readonly string ConfigFile = $"{DataFolder}\\config.json";
		}

		public MainWindow() {
			InitializeComponent();
			
			NotifyIcon notifyIcon = new NotifyIcon();
			notifyIcon.Icon = new System.Drawing.Icon("FluidBG.ico");
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

    private void ChangeRandomWallpaper() {
			NextChangeTextBlock.Text = timer.QueryNextTickTimestamp();
			Log("Tick occured");
			string[] configSourcePaths = GetConfigProperty<string[]>("sourcePaths");
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
			Wallpaper.Set(randomPath);
            HistoryListBox.Items.Insert(0, new ListBoxItem() {
                Style = (Style)FindResource("Fluid:ListBoxItem"),
                Content = new DockPanel() {
					Children = {
						new TextBlock() { 
							Text = randomPath,
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
			foreach (string path in configSourcePaths)
			{
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
			} catch {}
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

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			if (!Directory.Exists(Paths.DataFolder)) {
				Directory.CreateDirectory(Paths.DataFolder);
			}

			if (!File.Exists(Paths.LogFile))
			{
				File.Create(Paths.LogFile).Close();
			}

			if (!File.Exists(Paths.ConfigFile))
			{
				File.Create(Paths.ConfigFile).Close();
				File.WriteAllText(Paths.ConfigFile, "{}");
			}
			ValidateConfig();
			bool enabled = GetConfigProperty<bool>("enabled");
			int intervalIndex = GetConfigProperty<int>("intervalIndex");
			timer = new IntervalTimer(comboBoxSecondIntervals[intervalIndex], ChangeRandomWallpaper);
			if (enabled) {
				timer.Start();
			} 
			ClearLogFile();
			PopulateSourceList();
			PopulateIntervals();
			NextChangeTextBlock.Text = timer.QueryNextTickTimestamp();
            Log("Finished creating paths");
		}

		private void LogButton_Click(object sender, RoutedEventArgs e) {
			Process.Start("notepad.exe", Paths.LogFile);
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
			Wallpaper.Set(path);
        }

        private void OpenHistoryImageButton_Click(object sender, RoutedEventArgs e) {
            if (HistoryListBox.SelectedIndex == -1) return;
			string path = ((TextBlock)((DockPanel)((ListBoxItem)HistoryListBox.Items[HistoryListBox.SelectedIndex]).Content).Children[0]).Text;
            Process.Start(new ProcessStartInfo
            {
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
			if (!isFolder) fileDialog.Filters.Add(new CommonFileDialogFilter("Image Files", "*.jpg;*.jpeg;*.png;*.bmp;*.gif"));
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
	        if (IntervalComboBox.SelectedIndex == -1 || IntervalDecimalUpDown.Value == null) return;
			if ((Convert.ToDouble(e.NewValue) * comboBoxSecondIntervals[IntervalComboBox.SelectedIndex]) * 1000 > Int32.MaxValue) {
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
			if ((Convert.ToDouble(IntervalDecimalUpDown.Value.Value) * comboBoxSecondIntervals[selectedIndex]) * 1000 > Int32.MaxValue) {
				MessageBox.Show("Interval must be less than 4 weeks");
				return;
			}
			SetConfigProperty("intervalIndex", new JValue(selectedIndex));
			timer.ChangeInterval(Convert.ToDouble(IntervalDecimalUpDown.Value.Value) * comboBoxSecondIntervals[selectedIndex]);
        }

		private void EnabledButton_OnClick(object sender, RoutedEventArgs e) {
			SetConfigProperty("enabled", new JValue(EnabledToggleButton.IsChecked));
			NextChangeTextBlock.Text = timer.QueryNextTickTimestamp();
			if (EnabledToggleButton.IsChecked == true) {
				timer.Start();
			} else {
				timer.Stop();
				NextChangeTextBlock.Text = "Never";
			}
		}

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
			e.Cancel = true;
			Hide();
		}
    }
}