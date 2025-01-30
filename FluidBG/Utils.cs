using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FluidBG;

public static class Constants {
    public static readonly Version Version = new(1, 1, 2);
    public static readonly string GithubRepoUrl = "https://github.com/titushm/FluidBG";
    public static readonly int[] ComboBoxSecondIntervals = { 1, 60, 3600, 86400, 604800 };
    public static RegistryKey StartupRegistryKey = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
    
    public struct Paths {
        public static readonly string DataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\titushm\\FluidBG";
        public static readonly string LogFile = $"{DataFolder}\\log.tmp";
        public static readonly string ConfigFile = $"{DataFolder}\\config.json";
    }

    public static List<int> WallpaperModes = new() {
        10, 6, 2, 0, 22
    };
    public static string[] AppThemes = {
        "system",
        "light",
        "dark"
    };
    public static string[] ImageFileTypes ={
        "png",
        "jpeg",
        "jpg",
        "tiff",
        "bmp"
    };
}

public class Utils {
    
    public static void LogToFile(string? message) {
        string logPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\log.txt"; 
        File.WriteAllText(logPath, message);
    }


    public static string[] GetSpotlightImage(){
        HttpClient httpClient = new();
        try{
            httpClient.DefaultRequestHeaders.Add("User-Agent", "FluidBG");
            HttpResponseMessage response = httpClient.GetAsync("https://windows10spotlight.com/post-sitemap.xml").Result;
            string html = response.Content.ReadAsStringAsync().Result;
            string pattern = @"<loc>(.*?)</loc>";
            MatchCollection matches = Regex.Matches(html, pattern);
            Random random = new();
            int randomIndex = random.Next(matches.Count - 1);
            string pageUrl = matches[randomIndex].Groups[1].Value;
            response = httpClient.GetAsync(pageUrl).Result;
            html = response.Content.ReadAsStringAsync().Result;
            string details = html.Split("<script type=\"application/ld+json\" class=\"aioseop-schema\">")[1].Split("</script>")[0];
            JObject jsonDetails = JsonConvert.DeserializeObject<JObject>(details);
            string imageUrl = jsonDetails["@graph"][2]["image"]["url"].ToString();
            string description = jsonDetails["@graph"][2]["description"].ToString();

            using (WebClient client = new WebClient()) 
            {
                client.DownloadFile(new Uri(imageUrl), Constants.Paths.DataFolder + "\\spotlight.jpg");
            }

            return new[] { description };
        }
        catch {
            try{
                DateTime now = DateTime.UtcNow;
                string time = now.ToString("yyyy-MM-ddTHH:mm:ssZ");
                string url =
                    $"https://arc.msn.com/v3/Delivery/Placement?pid=209567&fmt=json&ua=WindowsShellClient%2F0&cdm=1&disphorzres=9999&dispvertres=9999&pl=en-US&lc=en-US&ctry=us&time={time}";
                JObject jsonObject = null;
                try{
                    Task<HttpResponseMessage> response = httpClient.GetAsync(url);
                    string responseString = response.Result.Content.ReadAsStringAsync().Result;
                    jsonObject = JsonConvert.DeserializeObject<JObject>(responseString);
                }
                catch{
                    Log("Failed to request api url.");
                }

                Random random = new Random();
                string itemString = jsonObject["batchrsp"]["items"][random.Next(2)]["item"].ToString();
                JObject itemObject = JsonConvert.DeserializeObject<JObject>(itemString);
                string spotlightUrl = itemObject["ad"]["image_fullscreen_001_landscape"]["u"].ToString();
                Task<Stream> stream = httpClient.GetStreamAsync(spotlightUrl);
                string imagePath = Constants.Paths.DataFolder + "\\spotlight.jpg";
                using (var fs = new FileStream(imagePath, FileMode.OpenOrCreate)){
                    stream.Result.CopyTo(fs);
                }

                string title = itemObject["ad"]["title_text"]["tx"].ToString();
                string author = itemObject["ad"]["copyright_text"]["tx"].ToString();
                return new[]{ title, author };
            }
            catch{
                LogToFile("Both methods failed to obtain spotlight image.");
                return new string[]{ };
            }
        }
    }


    public static void Log(string text) {
        try {
            string timeStamp = DateTime.Now.ToString("HH:mm:ss");
            StreamWriter logWriter = File.AppendText(Constants.Paths.LogFile);
            logWriter.Write($"[{timeStamp}] {text}\n");
            logWriter.Close();
        }
        catch{
            // ignored
        }
        
    }
    public static void ClearLogFile() {
        File.WriteAllText(Constants.Paths.LogFile, "");
        Log("Log file cleared");
    }
    
    public static void ValidateConfig() {
        if (!Directory.Exists(Constants.Paths.DataFolder)) {
            Directory.CreateDirectory(Constants.Paths.DataFolder);
        }
        try {
            string jsonString = File.ReadAllText(Constants.Paths.ConfigFile);
            JsonConvert.DeserializeObject<JObject>(jsonString);
        }
        catch {
            File.WriteAllText(Constants.Paths.ConfigFile, "{\"sourcePaths\":[]}");
        }
    }
    
    public static T GetConfigProperty<T>(string propertyName) {
        ValidateConfig();
        string jsonString = File.ReadAllText(Constants.Paths.ConfigFile);
        JObject jsonObject = JsonConvert.DeserializeObject<JObject>(jsonString);
        if (jsonObject.ContainsKey(propertyName)) {
            return jsonObject[propertyName].ToObject<T>();
        }

        return default;
    }

    public static void SetConfigProperty(string propertyName, JToken value) {
        ValidateConfig(); 
        string jsonString = File.ReadAllText(Constants.Paths.ConfigFile);
        JObject jsonObject = JsonConvert.DeserializeObject<JObject>(jsonString);
        if (jsonObject.ContainsKey(propertyName)) {
            jsonObject.Property(propertyName).Remove();
        }

        jsonObject.Add(propertyName, value);
        jsonString = JsonConvert.SerializeObject(jsonObject);
        File.WriteAllText(Constants.Paths.ConfigFile, jsonString);
    }
    
}

