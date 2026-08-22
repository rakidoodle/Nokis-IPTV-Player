using System.Globalization;
using System.Windows;
using MyIPTV.App.Converters;

namespace MyIPTV.Tests;

[TestClass]
public sealed class ResponsiveLayoutConverterTests
{
    [TestMethod]
    public void SidebarUsesCompactLayoutBelowBreakpoint()
    {
        ResponsiveSidebarWidthConverter widthConverter = new();
        SidebarLabelVisibilityConverter visibilityConverter = new();

        GridLength width = (GridLength)widthConverter.Convert(
            959d,
            typeof(GridLength),
            parameter: null!,
            CultureInfo.InvariantCulture);
        Visibility visibility = (Visibility)visibilityConverter.Convert(
            959d,
            typeof(Visibility),
            parameter: null!,
            CultureInfo.InvariantCulture);

        Assert.AreEqual(new GridLength(80), width);
        Assert.AreEqual(Visibility.Collapsed, visibility);
    }

    [TestMethod]
    public void SidebarUsesExpandedLayoutAtBreakpoint()
    {
        ResponsiveSidebarWidthConverter widthConverter = new();
        SidebarLabelVisibilityConverter visibilityConverter = new();

        GridLength width = (GridLength)widthConverter.Convert(
            960d,
            typeof(GridLength),
            parameter: null!,
            CultureInfo.InvariantCulture);
        Visibility visibility = (Visibility)visibilityConverter.Convert(
            960d,
            typeof(Visibility),
            parameter: null!,
            CultureInfo.InvariantCulture);

        Assert.AreEqual(new GridLength(232), width);
        Assert.AreEqual(Visibility.Visible, visibility);
    }
}
