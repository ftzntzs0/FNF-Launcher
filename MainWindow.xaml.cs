using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;
using Microsoft.UI;
using Microsoft.UI.Windowing;


namespace FNFVsliceLauncher
{
    public class PolymodMeta
    {
        public string title { get; set; }
        public string description { get; set; }
    }

    public class ModItem : System.ComponentModel.INotifyPropertyChanged
    {
        public string FolderPath { get; set; }
        public string FolderName { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public DateTime LastPlayed { get; set; }

        private string _iconPath;
        public string IconPath
        {
            get => _iconPath;
            set { _iconPath = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IconPath))); }
        }

        private bool _isPlayable = true;
        public bool IsPlayable
        {
            get => _isPlayable;
            set { _isPlayable = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsPlayable))); }
        }

        private bool _isRunning = false;
        public bool IsRunning
        {
            get => _isRunning;
            set { _isRunning = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsRunning))); }
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    }

    public sealed partial class MainWindow : Window
    {
        public static Symbol GetIconSymbol(bool isRunning) { return isRunning ? Symbol.Play : Symbol.Play; }
        public static string GetButtonLabel(bool isRunning) { return isRunning ? "Running" : "Play Mod"; }

        public ObservableCollection<ModItem> Mods { get; set; } = new ObservableCollection<ModItem>();
        private ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        private Dictionary<ModItem, Process> runningProcesses = new Dictionary<ModItem, Process>();
        private List<ModItem> allLoadedModsList = new List<ModItem>();

        public MainWindow()
        {
            this.InitializeComponent();
            SetupTitleBar();
            if (ModsGridView != null) ModsGridView.ItemsSource = Mods;
            if (NavView != null && NavView.MenuItems.Count > 0) NavView.SelectedItem = NavView.MenuItems[0];
            LoadSavedSettings();
            var hWnd = WindowNative.GetWindowHandle(this);

   
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(hWnd);

            AppWindow appWindow = AppWindow.GetFromWindowId(windowId);

            if (appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.Maximize();
            }
        
        
        }

        private void SetupTitleBar()
        {
            try
            {
                this.ExtendsContentIntoTitleBar = true;
                this.SetTitleBar(AppTitleBar);
                var hWnd = WindowNative.GetWindowHandle(this);
                var windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
                var appWindow = AppWindow.GetFromWindowId(windowId);
                if (AppWindowTitleBar.IsCustomizationSupported())
                {
                    var titleBar = appWindow.TitleBar;
                    titleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
                    titleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
                    var isDarkTheme = Application.Current.RequestedTheme == ApplicationTheme.Dark;
                    if (isDarkTheme)
                    {
                        titleBar.ButtonForegroundColor = Microsoft.UI.Colors.White;
                        titleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(25, 255, 255, 255);
                        titleBar.ButtonHoverForegroundColor = Microsoft.UI.Colors.White;
                    }
                    else
                    {
                        titleBar.ButtonForegroundColor = Microsoft.UI.Colors.Black;
                        titleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(25, 0, 0, 0);
                        titleBar.ButtonHoverForegroundColor = Microsoft.UI.Colors.Black;
                    }
                }
            }
            catch (Exception ex) { Debug.WriteLine($"Error TitleBar: {ex.Message}"); }
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (HomeGrid == null || SettingsGrid == null) return;
            if (args.IsSettingsSelected || (args.SelectedItem is NavigationViewItem item && item.Tag.ToString() == "Settings"))
            {
                HomeGrid.Visibility = Visibility.Collapsed;
                SettingsGrid.Visibility = Visibility.Visible;
                SettingsSaveStatusText.Text = "";
            }
            else
            {
                HomeGrid.Visibility = Visibility.Visible;
                SettingsGrid.Visibility = Visibility.Collapsed;
                LoadMods();
            }
        }

        private void LoadSavedSettings()
        {
            if (ExePathTextBox != null && localSettings.Values["ExePath"] is string savedExe) ExePathTextBox.Text = savedExe;
            if (ModsFolderPathTextBox != null && localSettings.Values["ModsFolderPath"] is string savedMods) ModsFolderPathTextBox.Text = savedMods;
            if (!string.IsNullOrEmpty(ExePathTextBox?.Text) && !string.IsNullOrEmpty(ModsFolderPathTextBox?.Text)) LoadMods();
            else if (StatusText != null) StatusText.Text = "Please set paths in Settings.";
        }

        private async void SelectExe_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker(); InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this)); picker.ViewMode = PickerViewMode.List; picker.FileTypeFilter.Add(".exe");
            var file = await picker.PickSingleFileAsync();
            if (file != null && ExePathTextBox != null) { ExePathTextBox.Text = file.Path; localSettings.Values["ExePath"] = file.Path; if (SettingsSaveStatusText != null) SettingsSaveStatusText.Text = "Game path saved!"; }
        }

        private async void SelectModsFolder_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FolderPicker(); InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this)); picker.FileTypeFilter.Add("*");
            var folder = await picker.PickSingleFolderAsync();
            if (folder != null && ModsFolderPathTextBox != null) { ModsFolderPathTextBox.Text = folder.Path; localSettings.Values["ModsFolderPath"] = folder.Path; if (SettingsSaveStatusText != null) SettingsSaveStatusText.Text = "Mods folder saved!"; }
        }

        private void RefreshMods_Click(object sender, RoutedEventArgs e) { LoadMods(); }
        private void SortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) { ApplySorting(); }

    
        private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                ApplySorting();
            }
        }

        private async void ApplySorting()
        {
            if (SortComboBox == null || Mods == null || allLoadedModsList.Count == 0) return;

      
            string searchText = SearchBox?.Text?.Trim().ToLowerInvariant() ?? "";

            var selectedItem = SortComboBox.SelectedItem as ComboBoxItem;
            string sortType = selectedItem?.Tag?.ToString() ?? "LastPlayed";

   
            var filteredList = allLoadedModsList.Where(m =>
                string.IsNullOrWhiteSpace(searchText) ||
                m.Title.ToLowerInvariant().Contains(searchText) ||
                m.Description.ToLowerInvariant().Contains(searchText)
            );

      
            IEnumerable<ModItem> sortedQuery;
            switch (sortType)
            {
                case "AZ": sortedQuery = filteredList.OrderBy(m => m.Title); break;
                case "ZA": sortedQuery = filteredList.OrderByDescending(m => m.Title); break;
                case "LastPlayed":
                default: sortedQuery = filteredList.OrderByDescending(m => m.LastPlayed); break;
            }
            var finalDisplayedList = sortedQuery.ToList();

          
            for (int i = Mods.Count - 1; i >= 0; i--)
            {
                if (!finalDisplayedList.Contains(Mods[i]))
                {
                    Mods.RemoveAt(i);
                }
            }

           
            foreach (var item in finalDisplayedList)
            {
                if (!Mods.Contains(item))
                {
                    Mods.Add(item);
                }
            }

            for (int i = 0; i < finalDisplayedList.Count; i++)
            {
                var itemToMove = finalDisplayedList[i];
                int currentIndex = Mods.IndexOf(itemToMove);

                if (currentIndex != i && currentIndex != -1)
                {
                    Mods.RemoveAt(currentIndex);
                    Mods.Insert(i, itemToMove);

                    await Task.Delay(15);
                }
            }
        }

        private async void LoadMods()
        {
            try
            {
                allLoadedModsList.Clear();
                string modsDir = ModsFolderPathTextBox.Text;
                if (string.IsNullOrWhiteSpace(modsDir) || !Directory.Exists(modsDir)) { StatusText.Text = "Mods folder not configured or invalid."; return; }
                StatusText.Text = "Searching for mods...";
                string[] directories = Directory.GetDirectories(modsDir);
                foreach (string dir in directories)
                {
                    string metaFilePath = Path.Combine(dir, "_polymod_meta.json"); string iconFilePath = Path.Combine(dir, "_polymod_icon.png");
                    if (File.Exists(metaFilePath))
                    {
                        try
                        {
                            string jsonContent = await File.ReadAllTextAsync(metaFilePath); var meta = JsonSerializer.Deserialize<PolymodMeta>(jsonContent);
                            string iconPath = null; if (File.Exists(iconFilePath)) iconPath = $"file:///{iconFilePath}";
                            string folderName = new DirectoryInfo(dir).Name; long lastPlayedTicks = 0;
                            if (localSettings.Values[$"LastPlayed_{folderName}"] is long savedTicks) lastPlayedTicks = savedTicks;
                            allLoadedModsList.Add(new ModItem { FolderPath = dir, FolderName = folderName, Title = meta?.title ?? "Untitled Mod", Description = meta?.description ?? "No description.", IconPath = iconPath, LastPlayed = new DateTime(lastPlayedTicks) });
                        }
                        catch (Exception ex) { Debug.WriteLine($"Error reading {dir}: {ex.Message}"); }
                    }
                }
                StatusText.Text = $"{allLoadedModsList.Count} mods ready to play.";
                ApplySorting();
            }
            catch (Exception ex) { Debug.WriteLine($"Error loading mods: {ex.Message}"); StatusText.Text = $"Error: {ex.Message}"; }
        }

        private async void LaunchMod_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is ModItem mod)
            {
                if (mod.IsRunning)
                {
                    if (runningProcesses.TryGetValue(mod, out var process))
                    {
                        try
                        {
                            if (!process.HasExited)
                            {
                                StatusText.Text = $"Stopping {mod.Title}..."; process.Kill();
                                bool exited = process.WaitForExit(5000); if (!exited) process.Kill(true);
                            }
                        }
                        catch (Exception ex) { Debug.WriteLine($"Error killing process: {ex.Message}"); }
                    }
                    return;
                }
                string gameExePath = ExePathTextBox.Text;
                if (string.IsNullOrWhiteSpace(gameExePath) || !File.Exists(gameExePath)) { StatusText.Text = "Error: Configure game executable in settings."; return; }
                await RunModAsync(mod, gameExePath);
            }
        }

        private async Task RunModAsync(ModItem mod, string gameExePath)
        {
            string gameDirectory = Path.GetDirectoryName(gameExePath); string gameModsFolder = Path.Combine(gameDirectory, "mods");
            string targetModPath = Path.Combine(gameModsFolder, mod.FolderName); string originalModPath = mod.FolderPath;

            mod.LastPlayed = DateTime.Now;
            localSettings.Values[$"LastPlayed_{mod.FolderName}"] = mod.LastPlayed.Ticks;

            
            ApplySorting();

            mod.IsPlayable = false; mod.IsRunning = true;
            StatusText.Text = $"Moving and starting {mod.Title}...";
            try
            {
                if (!Directory.Exists(gameModsFolder)) Directory.CreateDirectory(gameModsFolder);
                if (Directory.Exists(targetModPath)) { StatusText.Text = "Error: Mod name already exists in game folder."; mod.IsPlayable = true; mod.IsRunning = false; return; }
                Directory.Move(originalModPath, targetModPath);
                ProcessStartInfo startInfo = new ProcessStartInfo { FileName = gameExePath, WorkingDirectory = gameDirectory, UseShellExecute = false };
                using (Process process = Process.Start(startInfo))
                {
                    if (process != null) { runningProcesses[mod] = process; StatusText.Text = $"Playing {mod.Title}..."; await process.WaitForExitAsync(); runningProcesses.Remove(mod); }
                }
            }
            catch (Exception ex) { StatusText.Text = $"Error: {ex.Message}"; runningProcesses.Remove(mod); }
            finally
            {
                mod.IsRunning = false; StatusText.Text = "Game closed. Restoring mod folder...";
                if (Directory.Exists(targetModPath))
                {
                    await Task.Delay(1000);
                    try { Directory.Move(targetModPath, originalModPath); StatusText.Text = $"{mod.Title} closed and restored successfully."; }
                    catch { StatusText.Text = $"WARNING: Could not automatically move {mod.Title} back."; }
                }
                mod.IsPlayable = true;
            }
        }
    }
}