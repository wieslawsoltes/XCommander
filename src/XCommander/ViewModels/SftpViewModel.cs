using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Controls.DataGridFiltering;
using Avalonia.Controls.DataGridSearching;
using Avalonia.Controls.DataGridSorting;
using Avalonia.Data.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XCommander.Helpers;
using XCommander.Models;
using XCommander.Services;

namespace XCommander.ViewModels;

public partial class SftpViewModel : ViewModelBase
{
    private const string NameColumnKey = "name";
    private const string SizeColumnKey = "size";
    private const string DateColumnKey = "date";
    private const string PermissionsColumnKey = "permissions";

    private readonly ISftpService _sftpService;

    [ObservableProperty]
    private string _host = string.Empty;

    [ObservableProperty]
    private int _port = 22;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _privateKeyPath = string.Empty;

    [ObservableProperty]
    private bool _usePrivateKey;

    [ObservableProperty]
    private bool _isConnecting;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string _currentPath = "/";

    [ObservableProperty]
    private string _status = "Not connected";

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private bool _isOperationInProgress;

    [ObservableProperty]
    private SftpItem? _selectedItem;

    public ObservableCollection<SftpItem> Items { get; } = new();
    public ObservableCollection<SftpItem> SelectedItems { get; } = new();
    public ObservableCollection<DataGridColumnDefinition> ColumnDefinitions { get; }
    public FilteringModel FilteringModel { get; }
    public SortingModel SortingModel { get; }
    public SearchModel SearchModel { get; }

    public SftpViewModel(ISftpService sftpService)
    {
        _sftpService = sftpService;
        FilteringModel = new FilteringModel { OwnsViewFilter = true };
        SortingModel = new SortingModel
        {
            MultiSort = true,
            CycleMode = SortCycleMode.AscendingDescendingNone,
            OwnsViewSorts = true
        };
        SearchModel = new SearchModel();
        ColumnDefinitions = BuildColumnDefinitions();
    }

    private static ObservableCollection<DataGridColumnDefinition> BuildColumnDefinitions()
    {
        var builder = DataGridColumnDefinitionBuilder.For<SftpItem>();

        IPropertyInfo displaySizeProperty = DataGridColumnHelper.CreateProperty(
            nameof(SftpItem.DisplaySize),
            (SftpItem item) => item.DisplaySize);
        IPropertyInfo dateProperty = DataGridColumnHelper.CreateProperty(
            nameof(SftpItem.DateModified),
            (SftpItem item) => item.DateModified);
        IPropertyInfo permissionsProperty = DataGridColumnHelper.CreateProperty(
            nameof(SftpItem.Permissions),
            (SftpItem item) => item.Permissions);

        var dateColumn = builder.Text(
            header: "Modified",
            property: dateProperty,
            getter: item => item.DateModified,
            configure: column =>
            {
                column.ColumnKey = DateColumnKey;
                column.Width = new DataGridLength(140);
                column.IsReadOnly = true;
                column.ShowFilterButton = true;
            });

        if (dateColumn.Binding != null)
        {
            dateColumn.Binding.StringFormat = "yyyy-MM-dd HH:mm";
        }

        return new ObservableCollection<DataGridColumnDefinition>
        {
            builder.Template(
                header: "Name",
                cellTemplateKey: "SftpItemNameTemplate",
                configure: column =>
                {
                    column.ColumnKey = NameColumnKey;
                    column.Width = new DataGridLength(1, DataGridLengthUnitType.Star);
                    column.MinWidth = 200;
                    column.IsReadOnly = true;
                    column.ShowFilterButton = true;
                    column.ValueAccessor = new DataGridColumnValueAccessor<SftpItem, string>(
                        item => item.Name);
                    column.ValueType = typeof(string);
                }),
            builder.Text(
                header: "Size",
                property: displaySizeProperty,
                getter: item => item.DisplaySize,
                configure: column =>
                {
                    column.ColumnKey = SizeColumnKey;
                    column.Width = new DataGridLength(100);
                    column.IsReadOnly = true;
                    column.ShowFilterButton = true;
                    column.Options = new DataGridColumnDefinitionOptions
                    {
                        SortValueAccessor = new DataGridColumnValueAccessor<SftpItem, long>(
                            item => item.Size)
                    };
                }),
            dateColumn,
            builder.Text(
                header: "Permissions",
                property: permissionsProperty,
                getter: item => item.Permissions,
                configure: column =>
                {
                    column.ColumnKey = PermissionsColumnKey;
                    column.Width = new DataGridLength(100);
                    column.IsReadOnly = true;
                    column.ShowFilterButton = true;
                })
        };
    }

    [RelayCommand]
    public async Task ConnectAsync()
    {
        if (string.IsNullOrWhiteSpace(Host) || string.IsNullOrWhiteSpace(Username))
        {
            Status = "Host and username are required";
            return;
        }

        try
        {
            IsConnecting = true;
            Status = "Connecting...";

            var keyPath = UsePrivateKey && !string.IsNullOrWhiteSpace(PrivateKeyPath) 
                ? PrivateKeyPath 
                : null;

            var success = await _sftpService.ConnectAsync(Host, Port, Username, Password, keyPath);

            if (success)
            {
                IsConnected = true;
                Status = $"Connected to {Host}";
                CurrentPath = _sftpService.CurrentDirectory;
                await RefreshAsync();
            }
            else
            {
                Status = "Connection failed";
            }
        }
        catch (Exception ex)
        {
            Status = $"Error: {ex.Message}";
        }
        finally
        {
            IsConnecting = false;
        }
    }

    [RelayCommand]
    public async Task DisconnectAsync()
    {
        await _sftpService.DisconnectAsync();
        IsConnected = false;
        Status = "Disconnected";
        Items.Clear();
        CurrentPath = "/";
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        if (!IsConnected)
            return;

        try
        {
            Status = "Loading...";
            Items.Clear();

            var items = await _sftpService.ListDirectoryAsync(CurrentPath);
            
            // Add parent directory item if not at root
            if (CurrentPath != "/")
            {
                var parentPath = Path.GetDirectoryName(CurrentPath.TrimEnd('/'))?.Replace('\\', '/') ?? "/";
                Items.Add(new SftpItem
                {
                    Name = "..",
                    FullPath = parentPath,
                    IsDirectory = true,
                    DateModified = DateTime.Now
                });
            }

            foreach (var item in items)
            {
                Items.Add(item);
            }

            Status = $"{Items.Count} items";
        }
        catch (Exception ex)
        {
            Status = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task NavigateToAsync(string path)
    {
        if (!IsConnected)
            return;

        CurrentPath = path;
        await RefreshAsync();
    }

    [RelayCommand]
    public async Task OpenItemAsync()
    {
        if (SelectedItem == null)
            return;

        if (SelectedItem.IsDirectory)
        {
            await NavigateToAsync(SelectedItem.FullPath);
        }
    }

    [RelayCommand]
    public async Task DownloadSelectedAsync(string localFolder)
    {
        if (SelectedItem == null || string.IsNullOrEmpty(localFolder))
            return;

        try
        {
            IsOperationInProgress = true;
            
            var localPath = Path.Combine(localFolder, SelectedItem.Name);
            Status = $"Downloading {SelectedItem.Name}...";

            var progress = new Progress<FileOperationProgress>(p =>
            {
                Progress = p.Percentage;
                Status = $"Downloading: {p.CurrentItem} ({p.Percentage:F1}%)";
            });

            var success = await _sftpService.DownloadFileAsync(SelectedItem.FullPath, localPath, progress);

            if (success)
            {
                Status = $"Downloaded: {SelectedItem.Name}";
            }
            else
            {
                Status = "Download failed";
            }
        }
        catch (Exception ex)
        {
            Status = $"Error: {ex.Message}";
        }
        finally
        {
            IsOperationInProgress = false;
            Progress = 0;
        }
    }

    [RelayCommand]
    public async Task UploadFileAsync(string localPath)
    {
        if (!IsConnected || string.IsNullOrEmpty(localPath) || !File.Exists(localPath))
            return;

        try
        {
            IsOperationInProgress = true;
            
            var remotePath = CurrentPath.TrimEnd('/') + "/" + Path.GetFileName(localPath);
            Status = $"Uploading {Path.GetFileName(localPath)}...";

            var progress = new Progress<FileOperationProgress>(p =>
            {
                Progress = p.Percentage;
                Status = $"Uploading: {p.CurrentItem} ({p.Percentage:F1}%)";
            });

            var success = await _sftpService.UploadFileAsync(localPath, remotePath, progress);

            if (success)
            {
                Status = $"Uploaded: {Path.GetFileName(localPath)}";
                await RefreshAsync();
            }
            else
            {
                Status = "Upload failed";
            }
        }
        catch (Exception ex)
        {
            Status = $"Error: {ex.Message}";
        }
        finally
        {
            IsOperationInProgress = false;
            Progress = 0;
        }
    }

    [RelayCommand]
    public async Task CreateDirectoryAsync(string name)
    {
        if (!IsConnected || string.IsNullOrWhiteSpace(name))
            return;

        try
        {
            var path = CurrentPath.TrimEnd('/') + "/" + name;
            var success = await _sftpService.CreateDirectoryAsync(path);

            if (success)
            {
                Status = $"Created directory: {name}";
                await RefreshAsync();
            }
            else
            {
                Status = "Failed to create directory";
            }
        }
        catch (Exception ex)
        {
            Status = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task DeleteSelectedAsync()
    {
        if (SelectedItem == null || SelectedItem.Name == "..")
            return;

        try
        {
            Status = $"Deleting {SelectedItem.Name}...";

            bool success;
            if (SelectedItem.IsDirectory)
            {
                success = await _sftpService.DeleteDirectoryAsync(SelectedItem.FullPath);
            }
            else
            {
                success = await _sftpService.DeleteFileAsync(SelectedItem.FullPath);
            }

            if (success)
            {
                Status = $"Deleted: {SelectedItem.Name}";
                await RefreshAsync();
            }
            else
            {
                Status = "Delete failed";
            }
        }
        catch (Exception ex)
        {
            Status = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task RenameSelectedAsync(string newName)
    {
        if (SelectedItem == null || string.IsNullOrWhiteSpace(newName) || SelectedItem.Name == "..")
            return;

        try
        {
            var newPath = Path.GetDirectoryName(SelectedItem.FullPath)?.Replace('\\', '/') + "/" + newName;
            var success = await _sftpService.RenameAsync(SelectedItem.FullPath, newPath);

            if (success)
            {
                Status = $"Renamed to: {newName}";
                await RefreshAsync();
            }
            else
            {
                Status = "Rename failed";
            }
        }
        catch (Exception ex)
        {
            Status = $"Error: {ex.Message}";
        }
    }
}
