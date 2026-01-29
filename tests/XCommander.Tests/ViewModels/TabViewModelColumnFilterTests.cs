using System;
using System.Linq;
using Avalonia.Controls.DataGridFiltering;
using Moq;
using ReactiveUI;
using System.Reactive.Concurrency;
using XCommander.Models;
using XCommander.Services;
using XCommander.ViewModels;

namespace XCommander.Tests.ViewModels;

public class TabViewModelColumnFilterTests
{
    public TabViewModelColumnFilterTests()
    {
        RxApp.MainThreadScheduler = ImmediateScheduler.Instance;
        RxApp.TaskpoolScheduler = ImmediateScheduler.Instance;
    }

    [Fact]
    public void NameFilter_Apply_AddsDescriptor()
    {
        var viewModel = CreateViewModel();
        viewModel.NameFilter.Text = "readme";

        viewModel.NameFilter.ApplyCommand.Execute(null);

        var descriptor = Assert.Single(viewModel.FilteringModel.Descriptors);
        Assert.Equal("name", descriptor.ColumnId);
        Assert.Equal(FilteringOperator.Contains, descriptor.Operator);
        Assert.Equal("readme", descriptor.Value);
        Assert.Equal(StringComparison.OrdinalIgnoreCase, descriptor.StringComparisonMode);
    }

    [Fact]
    public void NameFilter_Clear_RemovesDescriptorAndResetsText()
    {
        var viewModel = CreateViewModel();
        viewModel.NameFilter.Text = "notes";
        viewModel.NameFilter.ApplyCommand.Execute(null);

        viewModel.NameFilter.ClearCommand.Execute(null);

        Assert.Empty(viewModel.FilteringModel.Descriptors);
        Assert.Equal(string.Empty, viewModel.NameFilter.Text);
    }

    [Fact]
    public void SizeFilter_Apply_UsesBetweenValues()
    {
        var viewModel = CreateViewModel();
        viewModel.SizeFilter.MinValue = 10;
        viewModel.SizeFilter.MaxValue = 20;

        viewModel.SizeFilter.ApplyCommand.Execute(null);

        var descriptor = Assert.Single(viewModel.FilteringModel.Descriptors);
        Assert.Equal("size", descriptor.ColumnId);
        Assert.Equal(FilteringOperator.Between, descriptor.Operator);
        Assert.Equal(new object[] { 10d, 20d }, descriptor.Values);
    }

    [Fact]
    public void DateFilter_Apply_ConvertsToDateTime()
    {
        var viewModel = CreateViewModel();
        var from = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var to = from.AddDays(1);
        viewModel.DateFilter.From = from;
        viewModel.DateFilter.To = to;

        viewModel.DateFilter.ApplyCommand.Execute(null);

        var descriptor = Assert.Single(viewModel.FilteringModel.Descriptors);
        Assert.Equal("date", descriptor.ColumnId);
        Assert.Equal(FilteringOperator.Between, descriptor.Operator);
        var values = descriptor.Values.ToArray();
        Assert.Equal(from.LocalDateTime, Assert.IsType<DateTime>(values[0]));
        Assert.Equal(to.LocalDateTime, Assert.IsType<DateTime>(values[1]));
    }

    private static TabViewModel CreateViewModel()
    {
        var fileSystem = new Mock<IFileSystemService>();
        fileSystem.Setup(service => service.GetDirectoryContents(It.IsAny<string>(), It.IsAny<bool>()))
            .Returns(Array.Empty<FileSystemItem>());
        fileSystem.Setup(service => service.GetDrives())
            .Returns(Array.Empty<DriveItem>());
        fileSystem.Setup(service => service.GetParentDirectory(It.IsAny<string>()))
            .Returns(string.Empty);

        return new TabViewModel(fileSystem.Object, new AppSettings());
    }
}
