using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
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

        private string _iconPath;
        public string IconPath
        {
            get => _iconPath;
            set
            {
                _iconPath = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IconPath)));
            }
        }

        private bool _isPlayable = true;
        public bool IsPlayable
        {
            get => _isPlayable;
            set
            {
                _isPlayable = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsPlayable)));
            }
        }

        private bool _isRunning = false;
        public bool IsRunning
        {
            get => _isRunning;
            set
            {
                _isRunning = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsRunning)));
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    }

    public sealed partial class MainWindow : Window
    {
        public ObservableCollection<ModItem> Mods { get; set; } = new ObservableCollection<ModItem>();
        private ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        private Dictionary<ModItem, Process> runningProcesses = new Dictionary<ModItem, Process>();

     
        public static Visibility BoolToVisibility(bool value)
        {
            return value ? Visibility.Visible : Visibility.Collapsed;
        }

        public static string GetButtonLabel(bool isRunning)
        {
            return isRunning ? "Running" : "Play";
        }

        public static Symbol GetIconSymbol(bool isRunning)
        {
            return isRunning ? Symbol.Stop : Symbol.Play;
        }

        public MainWindow()
        {
            try
            {
                this.InitializeComponent();

                SetupTitleBar();

                if (ModsGridView != null)
                {
                    ModsGridView.ItemsSource = Mods;
                }

               
                if (NavView != null && NavView.MenuItems.Count > 0)
                {
                    NavView.SelectedItem = NavView.MenuItems[0];
                }

                LoadSavedSettings();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in MainWindow constructor: {ex}");
                if (StatusText != null)
                {
                    StatusText.Text = $"Error initializing: {ex.Message}";
                }
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
                        titleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(51, 255, 255, 255);
                        titleBar.ButtonPressedForegroundColor = Microsoft.UI.Colors.White;
                        titleBar.ButtonInactiveForegroundColor = Windows.UI.Color.FromArgb(100, 255, 255, 255);
                    }
                    else
                    {
               
                        titleBar.ButtonForegroundColor = Microsoft.UI.Colors.Black;
                        titleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(25, 0, 0, 0);
                        titleBar.ButtonHoverForegroundColor = Microsoft.UI.Colors.Black;
                        titleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(51, 0, 0, 0);
                        titleBar.ButtonPressedForegroundColor = Microsoft.UI.Colors.Black;
                        titleBar.ButtonInactiveForegroundColor = Windows.UI.Color.FromArgb(100, 0, 0, 0);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting up title bar: {ex.Message}");
            }
        }

        // --- NAVIGATION SYSTEM AND SETTINGS ---

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            // Safety Alert: Prevents crash if WinUI tries to run this event before the screen loads completely
            if (HomeGrid == null || SettingsGrid == null) return;

            try
            {
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
            catch (Exception ex)
            {
                Debug.WriteLine($"Error changing navigation: {ex.Message}");
                StatusText.Text = $"Error: {ex.Message}";
            }
        }

        private void LoadSavedSettings()
        {
            try
            {
                if (ExePathTextBox != null && localSettings.Values["ExePath"] is string savedExe)
                    ExePathTextBox.Text = savedExe;

                if (ModsFolderPathTextBox != null && localSettings.Values["ModsFolderPath"] is string savedMods)
                    ModsFolderPathTextBox.Text = savedMods;

                if (ExePathTextBox != null && ModsFolderPathTextBox != null &&
                    !string.IsNullOrEmpty(ExePathTextBox.Text) && !string.IsNullOrEmpty(ModsFolderPathTextBox.Text))
                {
                    LoadMods();
                }
                else
                {
                    if (StatusText != null)
                        StatusText.Text = "Please go to Settings and set the paths for the game and mods.";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading saved settings: {ex.Message}");
                if (StatusText != null)
                    StatusText.Text = $"Error loading settings: {ex.Message}";
            }
        }

        private async void SelectExe_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new FileOpenPicker();
                InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
                picker.ViewMode = PickerViewMode.List;
                picker.FileTypeFilter.Add(".exe");

                var file = await picker.PickSingleFileAsync();
                if (file != null && ExePathTextBox != null)
                {
                    ExePathTextBox.Text = file.Path;
                    localSettings.Values["ExePath"] = file.Path;
                    if (SettingsSaveStatusText != null)
                        SettingsSaveStatusText.Text = "Game path saved!";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error selecting exe: {ex.Message}");
                if (SettingsSaveStatusText != null)
                    SettingsSaveStatusText.Text = $"Error: {ex.Message}";
            }
        }

        private async void SelectModsFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new FolderPicker();
                InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
                picker.FileTypeFilter.Add("*");

                var folder = await picker.PickSingleFolderAsync();
                if (folder != null && ModsFolderPathTextBox != null)
                {
                    ModsFolderPathTextBox.Text = folder.Path;
                    localSettings.Values["ModsFolderPath"] = folder.Path;
                    if (SettingsSaveStatusText != null)
                        SettingsSaveStatusText.Text = "Mods folder saved!";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error selecting mods folder: {ex.Message}");
                if (SettingsSaveStatusText != null)
                    SettingsSaveStatusText.Text = $"Error: {ex.Message}";
            }
        }

        // --- LOADING MODS ---

        private void RefreshMods_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LoadMods();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error refreshing mods: {ex.Message}");
                if (StatusText != null)
                    StatusText.Text = $"Error refreshing: {ex.Message}";
            }
        }

        private async void LoadMods()
        {
            try
            {
                Mods.Clear();
                string modsDir = ModsFolderPathTextBox.Text;

                if (string.IsNullOrWhiteSpace(modsDir) || !Directory.Exists(modsDir))
                {
                    StatusText.Text = "Mods folder not configured or invalid.";
                    return;
                }

                StatusText.Text = "Searching for mods...";

                string[] directories = Directory.GetDirectories(modsDir);

                foreach (string dir in directories)
                {
                    string metaFilePath = Path.Combine(dir, "_polymod_meta.json");
                    string iconFilePath = Path.Combine(dir, "_polymod_icon.png");

                    if (File.Exists(metaFilePath))
                    {
                        try
                        {
                            string jsonContent = await File.ReadAllTextAsync(metaFilePath);
                            var meta = JsonSerializer.Deserialize<PolymodMeta>(jsonContent);

                            string iconPath = null;
                            if (File.Exists(iconFilePath))
                            {
                                iconPath = $"file:///{iconFilePath}";
                            }

                            Mods.Add(new ModItem
                            {
                                FolderPath = dir,
                                FolderName = new DirectoryInfo(dir).Name,
                                Title = meta?.title ?? "Untitled Mod",
                                Description = meta?.description ?? "No description.",
                                IconPath = iconPath
                            });
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error reading {dir}: {ex.Message}");
                        }
                    }
                }

                StatusText.Text = $"{Mods.Count} mods ready to play.";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading mods: {ex.Message}");
                StatusText.Text = $"Error loading mods: {ex.Message}";
                Mods.Clear();
            }
        }

        // --- EXECUTION LOGIC ---

        private async void LaunchMod_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if ((sender as Button)?.Tag is ModItem mod)
                {
                    // If the game is running, kill the process
                    if (mod.IsRunning)
                    {
                        if (runningProcesses.TryGetValue(mod, out var process))
                        {
                            try
                            {
                                if (!process.HasExited)
                                {
                                    StatusText.Text = $"Stopping {mod.Title}...";
                                    process.Kill();

                                    // Wait for process to exit
                                    bool exited = process.WaitForExit(5000);
                                    if (!exited)
                                    {
                                        StatusText.Text = $"Force stopping {mod.Title}...";
                                        process.Kill(true);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Error killing process: {ex.Message}");
                                StatusText.Text = $"Error stopping game: {ex.Message}";
                            }
                        }
                        return;
                    }

                    // Otherwise, launch the mod
                    string gameExePath = ExePathTextBox.Text;

                    if (string.IsNullOrWhiteSpace(gameExePath) || !File.Exists(gameExePath))
                    {
                        StatusText.Text = "Error: Configure the game executable in settings.";
                        return;
                    }

                    await RunModAsync(mod, gameExePath);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error launching mod: {ex.Message}");
                if (StatusText != null)
                    StatusText.Text = $"Error: {ex.Message}";
            }
        }

        private void PlayButton_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ModItem mod && mod.IsRunning)
            {
                if (button.Content is StackPanel stackPanel)
                {
                    foreach (var child in stackPanel.Children)
                    {
                        if (child is TextBlock textBlock)
                        {
                            textBlock.Text = "Stop";
                        }
                        if (child is SymbolIcon icon)
                        {
                            icon.Symbol = Symbol.Stop;
                        }
                    }
                }
            }
        }

        private void PlayButton_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ModItem mod)
            {
                if (button.Content is StackPanel stackPanel)
                {
                    foreach (var child in stackPanel.Children)
                    {
                        if (child is TextBlock textBlock)
                        {
                            textBlock.Text = mod.IsRunning ? "Running" : "Play";
                        }
                        if (child is SymbolIcon icon)
                        {
                            icon.Symbol = mod.IsRunning ? Symbol.Stop : Symbol.Play;
                        }
                    }
                }
            }
        }

        private async Task RunModAsync(ModItem mod, string gameExePath)
        {
            string gameDirectory = Path.GetDirectoryName(gameExePath);
            string gameModsFolder = Path.Combine(gameDirectory, "mods");
            string targetModPath = Path.Combine(gameModsFolder, mod.FolderName);
            string originalModPath = mod.FolderPath;

            mod.IsPlayable = false;
            mod.IsRunning = true;
            StatusText.Text = $"Moving and starting {mod.Title}...";

            try
            {
                if (!Directory.Exists(gameModsFolder))
                {
                    Directory.CreateDirectory(gameModsFolder);
                }

                if (Directory.Exists(targetModPath))
                {
                    StatusText.Text = "Error: A mod with this name is already in the game folder.";
                    mod.IsPlayable = true;
                    mod.IsRunning = false;
                    return;
                }

                Directory.Move(originalModPath, targetModPath);

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = gameExePath,
                    WorkingDirectory = gameDirectory,
                    UseShellExecute = false
                };

                using (Process process = Process.Start(startInfo))
                {
                    if (process != null)
                    {
                        runningProcesses[mod] = process;
                        StatusText.Text = $"Playing {mod.Title}...";
                        await process.WaitForExitAsync();
                        runningProcesses.Remove(mod);
                    }
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error: {ex.Message}";
                runningProcesses.Remove(mod);
            }
            finally
            {
                mod.IsRunning = false;
                StatusText.Text = "Closing the game and restoring the mod folder...";
                if (Directory.Exists(targetModPath))
                {
                    await Task.Delay(1000);
                    try
                    {
                        Directory.Move(targetModPath, originalModPath);
                        StatusText.Text = $"{mod.Title} closed and returned successfully.";
                    }
                    catch
                    {
                        StatusText.Text = $"WARNING: Could not automatically move {mod.Title} back.";
                    }
                }

                mod.IsPlayable = true;
            }
        }
    }
}