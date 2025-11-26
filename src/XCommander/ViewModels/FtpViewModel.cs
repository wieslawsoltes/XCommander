using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XCommander.Models;
using XCommander.Services;

namespace XCommander.ViewModels;

public partial class FtpConnectionViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _id = string.Empty;
    
    [ObservableProperty]
    private string _name = string.Empty;
    
    [ObservableProperty]
    private string _host = string.Empty;
    
    [ObservableProperty]
    private int _port = 21;
    
    [ObservableProperty]
    private string _username = string.Empty;
    
    [ObservableProperty]
    private ConnectionProtocol _protocol = ConnectionProtocol.Ftp;
    
    [ObservableProperty]
    private FtpEncryptionMode _encryptionMode = FtpEncryptionMode.None;
    
    public static FtpConnectionViewModel FromModel(FtpConnection model) => new()
    {
        Id = model.Id,
        Name = model.Name,
        Host = model.Host,
        Port = model.Port,
        Username = model.Username,
        Protocol = model.Protocol,
        EncryptionMode = model.EncryptionMode
    };
    
    public FtpConnection ToModel() => new()
    {
        Id = Id,
        Name = Name,
        Host = Host,
        Port = Port,
        Username = Username,
        Protocol = Protocol,
        EncryptionMode = EncryptionMode
    };
}

public partial class FtpViewModel : ViewModelBase
{
    private readonly IFtpService _ftpService;
    
    [ObservableProperty]
    private string _host = string.Empty;
    
    [ObservableProperty]
    private int _port = 21;
    
    [ObservableProperty]
    private string _username = string.Empty;
    
    [ObservableProperty]
    private string _password = string.Empty;
    
    [ObservableProperty]
    private string _connectionName = string.Empty;
    
    [ObservableProperty]
    private string _currentPath = "/";
    
    [ObservableProperty]
    private bool _isConnected;
    
    [ObservableProperty]
    private bool _isConnecting;
    
    [ObservableProperty]
    private string _statusText = "Not connected";
    
    [ObservableProperty]
    private FtpItem? _selectedItem;
    
    [ObservableProperty]
    private FtpConnectionViewModel? _selectedSavedConnection;
    
    [ObservableProperty]
    private FtpEncryptionMode _encryptionMode = FtpEncryptionMode.None;
    
    /// <summary>
    /// Available encryption modes for FTP connections
    /// </summary>
    public FtpEncryptionMode[] AvailableEncryptionModes { get; } = 
    [
        FtpEncryptionMode.None,
        FtpEncryptionMode.Explicit,
        FtpEncryptionMode.Implicit
    ];
    
    public ObservableCollection<FtpItem> Items { get; } = [];
    public ObservableCollection<FtpConnectionViewModel> SavedConnections { get; } = [];
    
    public FtpViewModel(IFtpService ftpService)
    {
        _ftpService = ftpService;
        LoadSavedConnections();
    }
    
    private void LoadSavedConnections()
    {
        var data = FtpConnectionsData.Load();
        SavedConnections.Clear();
        foreach (var conn in data.Connections.OrderBy(c => c.Order))
        {
            SavedConnections.Add(FtpConnectionViewModel.FromModel(conn));
        }
    }
    
    [RelayCommand]
    public void SaveCurrentConnection()
    {
        if (string.IsNullOrEmpty(Host))
        {
            StatusText = "Please enter a host first";
            return;
        }
        
        var name = string.IsNullOrEmpty(ConnectionName) 
            ? $"{Username}@{Host}" 
            : ConnectionName;
        
        var connection = new FtpConnection
        {
            Name = name,
            Host = Host,
            Port = Port,
            Username = Username,
            Protocol = ConnectionProtocol.Ftp,
            EncryptionMode = EncryptionMode
        };
        connection.SetPassword(Password);
        
        var data = FtpConnectionsData.Load();
        
        // Check if already exists
        var existing = data.Connections.FirstOrDefault(c => 
            c.Host == Host && c.Port == Port && c.Username == Username);
        
        if (existing != null)
        {
            existing.Name = name;
            existing.SetPassword(Password);
            existing.EncryptionMode = EncryptionMode;
            existing.LastUsed = DateTime.Now;
        }
        else
        {
            connection.Order = data.Connections.Count;
            data.Connections.Add(connection);
        }
        
        data.Save();
        LoadSavedConnections();
        StatusText = $"Connection '{name}' saved";
    }
    
    [RelayCommand]
    public void LoadSelectedConnection()
    {
        if (SelectedSavedConnection == null)
            return;
        
        var data = FtpConnectionsData.Load();
        var conn = data.Connections.FirstOrDefault(c => c.Id == SelectedSavedConnection.Id);
        
        if (conn != null)
        {
            Host = conn.Host;
            Port = conn.Port;
            Username = conn.Username;
            Password = conn.GetPassword();
            ConnectionName = conn.Name;
            EncryptionMode = conn.EncryptionMode;
            StatusText = $"Loaded connection '{conn.Name}'";
        }
    }
    
    [RelayCommand]
    public void DeleteSelectedConnection()
    {
        if (SelectedSavedConnection == null)
            return;
        
        var data = FtpConnectionsData.Load();
        var conn = data.Connections.FirstOrDefault(c => c.Id == SelectedSavedConnection.Id);
        
        if (conn != null)
        {
            data.Connections.Remove(conn);
            data.Save();
            LoadSavedConnections();
            StatusText = $"Connection '{conn.Name}' deleted";
        }
    }
    
    [RelayCommand]
    public async Task ConnectAsync()
    {
        if (string.IsNullOrEmpty(Host))
        {
            StatusText = "Please enter a host";
            return;
        }
        
        IsConnecting = true;
        var encryptionInfo = EncryptionMode != FtpEncryptionMode.None 
            ? $" (FTPS {EncryptionMode})" 
            : "";
        StatusText = $"Connecting{encryptionInfo}...";
        
        try
        {
            var success = await _ftpService.ConnectAsync(Host, Port, Username, Password, EncryptionMode);
            
            if (success)
            {
                IsConnected = true;
                StatusText = $"Connected to {Host}{encryptionInfo}";
                
                // Update last used
                var data = FtpConnectionsData.Load();
                var conn = data.Connections.FirstOrDefault(c => 
                    c.Host == Host && c.Port == Port && c.Username == Username);
                if (conn != null)
                {
                    conn.LastUsed = DateTime.Now;
                    data.Save();
                }
                
                await NavigateToAsync("/");
            }
            else
            {
                StatusText = "Connection failed";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsConnecting = false;
        }
    }
    
    [RelayCommand]
    public async Task DisconnectAsync()
    {
        await _ftpService.DisconnectAsync();
        IsConnected = false;
        Items.Clear();
        CurrentPath = "/";
        StatusText = "Disconnected";
    }
    
    [RelayCommand]
    public async Task NavigateToAsync(string path)
    {
        if (!IsConnected)
            return;
            
        StatusText = $"Loading {path}...";
        
        try
        {
            var items = await _ftpService.ListDirectoryAsync(path);
            
            Items.Clear();
            
            // Add parent directory entry if not at root
            if (path != "/")
            {
                Items.Add(new FtpItem
                {
                    Name = "..",
                    FullPath = GetParentPath(path),
                    IsDirectory = true
                });
            }
            
            foreach (var item in items)
            {
                Items.Add(item);
            }
            
            CurrentPath = path;
            StatusText = $"{Items.Count} items";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
    }
    
    [RelayCommand]
    public async Task GoToParentAsync()
    {
        if (CurrentPath == "/")
            return;
            
        var parent = GetParentPath(CurrentPath);
        await NavigateToAsync(parent);
    }
    
    [RelayCommand]
    public async Task RefreshAsync()
    {
        await NavigateToAsync(CurrentPath);
    }
    
    [RelayCommand]
    public async Task OpenItemAsync(FtpItem? item)
    {
        if (item == null)
            return;
            
        if (item.IsDirectory)
        {
            await NavigateToAsync(item.FullPath);
        }
        // For files, we'll handle download separately
    }
    
    [RelayCommand]
    public async Task DownloadSelectedAsync(string localFolder)
    {
        if (SelectedItem == null || SelectedItem.IsDirectory)
            return;
            
        var localPath = Path.Combine(localFolder, SelectedItem.Name);
        
        StatusText = $"Downloading {SelectedItem.Name}...";
        
        try
        {
            var success = await _ftpService.DownloadFileAsync(SelectedItem.FullPath, localPath);
            StatusText = success ? $"Downloaded {SelectedItem.Name}" : "Download failed";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
    }
    
    [RelayCommand]
    public async Task UploadFileAsync(string localPath)
    {
        if (!IsConnected || !File.Exists(localPath))
            return;
            
        var fileName = Path.GetFileName(localPath);
        var remotePath = CurrentPath.TrimEnd('/') + "/" + fileName;
        
        StatusText = $"Uploading {fileName}...";
        
        try
        {
            var success = await _ftpService.UploadFileAsync(localPath, remotePath);
            
            if (success)
            {
                StatusText = $"Uploaded {fileName}";
                await RefreshAsync();
            }
            else
            {
                StatusText = "Upload failed";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
    }
    
    [RelayCommand]
    public async Task CreateDirectoryAsync(string name)
    {
        if (!IsConnected || string.IsNullOrEmpty(name))
            return;
            
        var path = CurrentPath.TrimEnd('/') + "/" + name;
        
        try
        {
            var success = await _ftpService.CreateDirectoryAsync(path);
            
            if (success)
            {
                await RefreshAsync();
            }
            else
            {
                StatusText = "Failed to create directory";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
    }
    
    [RelayCommand]
    public async Task DeleteSelectedAsync()
    {
        if (SelectedItem == null || SelectedItem.Name == "..")
            return;
            
        StatusText = $"Deleting {SelectedItem.Name}...";
        
        try
        {
            bool success;
            
            if (SelectedItem.IsDirectory)
            {
                success = await _ftpService.DeleteDirectoryAsync(SelectedItem.FullPath);
            }
            else
            {
                success = await _ftpService.DeleteFileAsync(SelectedItem.FullPath);
            }
            
            if (success)
            {
                await RefreshAsync();
            }
            else
            {
                StatusText = "Delete failed";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
    }
    
    private static string GetParentPath(string path)
    {
        if (path == "/")
            return "/";
            
        var trimmed = path.TrimEnd('/');
        var lastSlash = trimmed.LastIndexOf('/');
        
        return lastSlash <= 0 ? "/" : trimmed.Substring(0, lastSlash);
    }
}
