using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Controls.DataGridFiltering;
using Avalonia.Controls.DataGridSearching;
using Avalonia.Controls.DataGridSorting;
using Avalonia.Data.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using XCommander.Converters;
using XCommander.Helpers;
using XCommander.Models;

namespace XCommander.ViewModels;

public partial class DriveSelectionViewModel : ViewModelBase
{
    private const string NameColumnKey = "name";
    private const string TypeColumnKey = "type";
    private const string FreeColumnKey = "free";
    private const string TotalColumnKey = "total";

    public ObservableCollection<DriveItem> Drives { get; } = [];
    public ObservableCollection<DataGridColumnDefinition> ColumnDefinitions { get; }
    public FilteringModel FilteringModel { get; }
    public SortingModel SortingModel { get; }
    public SearchModel SearchModel { get; }
    
    [ObservableProperty]
    private DriveItem? _selectedDrive;

    public DriveSelectionViewModel(IEnumerable<DriveItem> drives)
    {
        foreach (var drive in drives)
        {
            Drives.Add(drive);
        }

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
        var builder = DataGridColumnDefinitionBuilder.For<DriveItem>();

        IPropertyInfo nameProperty = DataGridColumnHelper.CreateProperty(
            nameof(DriveItem.DisplayName),
            (DriveItem item) => item.DisplayName);
        IPropertyInfo typeProperty = DataGridColumnHelper.CreateProperty(
            nameof(DriveItem.DriveType),
            (DriveItem item) => item.DriveType);
        IPropertyInfo freeProperty = DataGridColumnHelper.CreateProperty(
            nameof(DriveItem.AvailableFreeSpace),
            (DriveItem item) => item.AvailableFreeSpace);
        IPropertyInfo totalProperty = DataGridColumnHelper.CreateProperty(
            nameof(DriveItem.TotalSize),
            (DriveItem item) => item.TotalSize);

        var freeColumn = builder.Text(
            header: "Free",
            property: freeProperty,
            getter: item => item.AvailableFreeSpace,
            configure: column =>
            {
                column.ColumnKey = FreeColumnKey;
                column.Width = new DataGridLength(120);
                column.IsReadOnly = true;
                column.ShowFilterButton = true;
            });

        if (freeColumn.Binding != null)
        {
            freeColumn.Binding.Converter = FileSizeConverter.Instance;
        }

        var totalColumn = builder.Text(
            header: "Total",
            property: totalProperty,
            getter: item => item.TotalSize,
            configure: column =>
            {
                column.ColumnKey = TotalColumnKey;
                column.Width = new DataGridLength(120);
                column.IsReadOnly = true;
                column.ShowFilterButton = true;
            });

        if (totalColumn.Binding != null)
        {
            totalColumn.Binding.Converter = FileSizeConverter.Instance;
        }

        return new ObservableCollection<DataGridColumnDefinition>
        {
            builder.Text(
                header: "Name",
                property: nameProperty,
                getter: item => item.DisplayName,
                configure: column =>
                {
                    column.ColumnKey = NameColumnKey;
                    column.Width = new DataGridLength(1, DataGridLengthUnitType.Star);
                    column.IsReadOnly = true;
                    column.ShowFilterButton = true;
                }),
            builder.Text(
                header: "Type",
                property: typeProperty,
                getter: item => item.DriveType,
                configure: column =>
                {
                    column.ColumnKey = TypeColumnKey;
                    column.Width = new DataGridLength(140);
                    column.IsReadOnly = true;
                    column.ShowFilterButton = true;
                }),
            freeColumn,
            totalColumn
        };
    }
}
