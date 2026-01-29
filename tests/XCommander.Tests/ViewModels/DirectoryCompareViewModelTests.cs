using System.Linq;
using Avalonia.Controls.DataGridSorting;
using Moq;
using XCommander.Services;
using XCommander.ViewModels;

namespace XCommander.Tests.ViewModels;

public class DirectoryCompareViewModelTests
{
    [Fact]
    public void Constructor_InitializesGridModels()
    {
        var fileSystemService = new Mock<IFileSystemService>();

        var viewModel = new DirectoryCompareViewModel(fileSystemService.Object);

        Assert.NotNull(viewModel.ColumnDefinitions);
        Assert.Equal(7, viewModel.ColumnDefinitions.Count);
        Assert.NotNull(viewModel.FilteringModel);
        Assert.True(viewModel.FilteringModel.OwnsViewFilter);
        Assert.NotNull(viewModel.SortingModel);
        Assert.True(viewModel.SortingModel.MultiSort);
        Assert.Equal(SortCycleMode.AscendingDescendingNone, viewModel.SortingModel.CycleMode);
        Assert.True(viewModel.SortingModel.OwnsViewSorts);
        Assert.NotNull(viewModel.SearchModel);
    }

    [Fact]
    public void Constructor_SetsStableColumnKeys()
    {
        var fileSystemService = new Mock<IFileSystemService>();

        var viewModel = new DirectoryCompareViewModel(fileSystemService.Object);

        var keys = viewModel.ColumnDefinitions
            .Select(column => column.ColumnKey?.ToString())
            .ToArray();

        Assert.Equal(
            new[]
            {
                "selected",
                "status",
                "path",
                "left-size",
                "left-date",
                "right-size",
                "right-date"
            },
            keys);
    }

    [Fact]
    public void ResultsView_ReflectsResultsCollection()
    {
        var fileSystemService = new Mock<IFileSystemService>();

        var viewModel = new DirectoryCompareViewModel(fileSystemService.Object);
        var item = new CompareResultItem { RelativePath = "example.txt" };

        viewModel.Results.Add(item);

        Assert.Contains(item, viewModel.ResultsView.Cast<CompareResultItem>());
    }
}
