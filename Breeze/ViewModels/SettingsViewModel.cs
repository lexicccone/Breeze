using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Input;
using Avalonia;
using Breeze.Models;
using Breeze.Services;
using Breeze.Utilities;
using Microsoft.Web.WebView2.Core;

namespace Breeze.ViewModels;

/// <summary>State of the settings page. Every change is written to disk immediately.</summary>
public sealed class SettingsViewModel : ViewModelBase
{
    private readonly AppSettings _settings = SettingsStore.Current;

    /// <summary>Height of one navigation entry, used to place the selection indicator.</summary>
    private const double SectionPitch = 38;

    private int _selectedSection;
    private Func<Task>? _pending;
    private string _pendingResult = string.Empty;
    private string _confirmMessage = string.Empty;
    private bool _isConfirmOpen;
    private string _privacyStatus = string.Empty;

    public SettingsViewModel()
    {
        ClearHistoryCommand = new RelayCommand(() => Ask(
            "Clear browsing history? This removes the local history for every site.",
            BrowsingData.ClearHistoryAsync,
            "Browsing history cleared."));

        ClearCookiesCommand = new RelayCommand(() => Ask(
            "Clear cookies? You will be signed out of sites you are logged into.",
            BrowsingData.ClearCookiesAsync,
            "Cookies cleared."));

        ClearCacheCommand = new RelayCommand(() => Ask(
            "Clear cache? Cached files will be downloaded again on your next visit.",
            BrowsingData.ClearCacheAsync,
            "Cache cleared."));

        ConfirmCommand = new RelayCommand(Confirm);
        CancelCommand = new RelayCommand(() => IsConfirmOpen = false);
        OpenDownloadFolderCommand = new RelayCommand(OpenDownloadFolder);
    }

    public ICommand ClearHistoryCommand { get; }

    public ICommand ClearCookiesCommand { get; }

    public ICommand ClearCacheCommand { get; }

    public ICommand ConfirmCommand { get; }

    public ICommand CancelCommand { get; }

    public ICommand OpenDownloadFolderCommand { get; }

    public IReadOnlyList<SearchEngine> SearchEngines => Services.SearchEngines.All;

    public int SelectedSection
    {
        get => _selectedSection;
        set
        {
            if (SetProperty(ref _selectedSection, value))
            {
                OnPropertyChanged(nameof(IndicatorOffset));
            }
        }
    }

    /// <summary>Top offset of the navigation indicator; animated by the view.</summary>
    public Thickness IndicatorOffset => new(3, 9 + (_selectedSection * SectionPitch), 0, 0);

    public bool StartupIsHome
    {
        get => _settings.StartupPage == StartupPage.Home;
        set => SetStartup(StartupPage.Home, value);
    }

    public bool StartupIsBlank
    {
        get => _settings.StartupPage == StartupPage.Blank;
        set => SetStartup(StartupPage.Blank, value);
    }

    public bool StartupIsCustom
    {
        get => _settings.StartupPage == StartupPage.Custom;
        set => SetStartup(StartupPage.Custom, value);
    }

    public string StartupUrl
    {
        get => _settings.StartupUrl;
        set => Store(() => _settings.StartupUrl = value, _settings.StartupUrl != value);
    }

    public string DownloadFolder
    {
        get => _settings.DownloadFolder;
        set => Store(() => _settings.DownloadFolder = value, _settings.DownloadFolder != value);
    }

    public bool AskWhereToSave
    {
        get => _settings.AskWhereToSave;
        set => Store(() => _settings.AskWhereToSave = value, _settings.AskWhereToSave != value);
    }

    public bool ThemeIsSystem
    {
        get => _settings.Theme == AppTheme.System;
        set => SetTheme(AppTheme.System, value);
    }

    public bool ThemeIsLight
    {
        get => _settings.Theme == AppTheme.Light;
        set => SetTheme(AppTheme.Light, value);
    }

    public bool ThemeIsDark
    {
        get => _settings.Theme == AppTheme.Dark;
        set => SetTheme(AppTheme.Dark, value);
    }

    public double UiScale => _settings.UiScale;

    public bool CompactMode => _settings.CompactMode;

    public SearchEngine SelectedSearchEngine
    {
        get => Services.SearchEngines.Current;
        set => Store(() => _settings.SearchEngine = value.Name, _settings.SearchEngine != value.Name);
    }

    public string Version => $"v{AppIdentity.Version}";

    public string RuntimeVersion => RuntimeInformation.FrameworkDescription;

    public string WebViewVersion => ReadWebViewVersion();

    public bool IsConfirmOpen
    {
        get => _isConfirmOpen;
        private set => SetProperty(ref _isConfirmOpen, value);
    }

    public string ConfirmMessage
    {
        get => _confirmMessage;
        private set => SetProperty(ref _confirmMessage, value);
    }

    public string PrivacyStatus
    {
        get => _privacyStatus;
        private set => SetProperty(ref _privacyStatus, value);
    }

    private void SetStartup(StartupPage page, bool selected)
    {
        if (!selected || _settings.StartupPage == page)
        {
            return;
        }

        _settings.StartupPage = page;
        SettingsStore.Save();
        OnPropertyChanged(nameof(StartupIsHome));
        OnPropertyChanged(nameof(StartupIsBlank));
        OnPropertyChanged(nameof(StartupIsCustom));
    }

    private void SetTheme(AppTheme theme, bool selected)
    {
        if (!selected || _settings.Theme == theme)
        {
            return;
        }

        _settings.Theme = theme;
        SettingsStore.Save();
        Theming.Apply(theme);
        OnPropertyChanged(nameof(ThemeIsSystem));
        OnPropertyChanged(nameof(ThemeIsLight));
        OnPropertyChanged(nameof(ThemeIsDark));
    }

    private void Store(Action apply, bool changed, [CallerMemberName] string? propertyName = null)
    {
        if (!changed)
        {
            return;
        }

        apply();
        SettingsStore.Save();
        OnPropertyChanged(propertyName);
    }

    private void Ask(string message, Func<Task> action, string result)
    {
        ConfirmMessage = message;
        _pending = action;
        _pendingResult = result;
        PrivacyStatus = string.Empty;
        IsConfirmOpen = true;
    }

    private void Confirm()
    {
        var action = _pending;
        _pending = null;
        IsConfirmOpen = false;

        if (action is not null)
        {
            _ = RunAsync(action, _pendingResult);
        }
    }

    private async Task RunAsync(Func<Task> action, string result)
    {
        PrivacyStatus = "Working...";
        await action();
        PrivacyStatus = result;
    }

    /// <summary>Opens the download folder in Explorer. The stored value is treated as untrusted:
    /// it must resolve to an existing directory, and it is passed as an argument rather than
    /// shell executed, so it can never be resolved as a program to run.</summary>
    private void OpenDownloadFolder()
    {
        try
        {
            var folder = Path.GetFullPath(DownloadFolder);

            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            var start = new ProcessStartInfo("explorer.exe") { UseShellExecute = false };
            start.ArgumentList.Add(folder);
            Process.Start(start)?.Dispose();
        }
        catch (Exception error)
        {
            ErrorLog.Write("downloads.open", error);
        }
    }

    private static string ReadWebViewVersion()
    {
        try
        {
            return CoreWebView2Environment.GetAvailableBrowserVersionString() ?? "Not installed";
        }
        catch (Exception)
        {
            return "Not installed";
        }
    }
}
