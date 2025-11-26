using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace XCommander.ViewModels;

public partial class AboutViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _applicationName = "XCommander";
    
    [ObservableProperty]
    private string _version = "1.0.0";
    
    [ObservableProperty]
    private string _copyright = $"© {DateTime.Now.Year} XCommander Project";
    
    [ObservableProperty]
    private string _description = "A cross-platform file manager inspired by Total Commander, " +
                                  "built with Avalonia UI and C#.";
    
    [ObservableProperty]
    private string _license = "MIT License";
    
    [ObservableProperty]
    private string _projectUrl = "https://github.com/wieslawsoltes/XCommander";
    
    public string DotNetVersion => Environment.Version.ToString();
    
    public string AvaloniaVersion => typeof(Avalonia.Application).Assembly.GetName().Version?.ToString() ?? "Unknown";
    
    public string OSVersion => $"{Environment.OSVersion.Platform} {Environment.OSVersion.Version}";
    
    public string SystemInfo => $".NET {DotNetVersion} | Avalonia {AvaloniaVersion} | {OSVersion}";
    
    public event EventHandler? RequestClose;
    
    [RelayCommand]
    public void Close()
    {
        RequestClose?.Invoke(this, EventArgs.Empty);
    }
    
    [RelayCommand]
    public void OpenProjectUrl()
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = ProjectUrl,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch
        {
            // Ignore errors opening URL
        }
    }
}
