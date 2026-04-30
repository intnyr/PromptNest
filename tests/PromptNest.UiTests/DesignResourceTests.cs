using System.Xml.Linq;

using FluentAssertions;

namespace PromptNest.UiTests;

public sealed class DesignResourceTests
{
    [Fact]
    public void PromptNestResourcesMergesFoundationalDictionaries()
    {
        XDocument resources = LoadDictionary("PromptNestResources.xaml");
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        string[] sources = resources
            .Descendants(presentation + "ResourceDictionary")
            .Attributes("Source")
            .Select(attribute => attribute.Value)
            .ToArray();

        sources.Should().BeEquivalentTo(
            "Colors.xaml",
            "Typography.xaml",
            "Layout.xaml",
            "ControlStyles.xaml");
    }

    [Fact]
    public void ColorsDictionaryDefinesCoreSurfaceAndStateBrushes()
    {
        XDocument colors = LoadDictionary("Colors.xaml");

        string[] requiredBrushes =
        [
            "PromptNestWindowBackgroundBrush",
            "PromptNestAppChromeBrush",
            "PromptNestSidebarBrush",
            "PromptNestNavigationRailBrush",
            "PromptNestPaneBrush",
            "PromptNestEditorBrush",
            "PromptNestBorderMutedBrush",
            "PromptNestTextPrimaryBrush",
            "PromptNestTextSecondaryBrush",
            "PromptNestIconSelectedBrush",
            "PromptNestAccentBrush",
            "PromptNestSelectedRowBrush",
            "PromptNestFocusStrokeBrush",
            "PromptNestSuccessBrush",
            "PromptNestWarningBrush",
            "PromptNestDangerBrush"
        ];

        GetResourceKeys(colors).Should().Contain(requiredBrushes);
    }

    [Fact]
    public void ControlStylesDictionaryDefinesReusableShellListAndEditorStyles()
    {
        XDocument styles = LoadDictionary("ControlStyles.xaml");

        string[] requiredStyles =
        [
            "PromptNestToolbarButtonStyle",
            "PromptNestPrimaryToolbarButtonStyle",
            "PromptNestSearchBoxStyle",
            "PromptNestPromptListRowStyle",
            "PromptNestFolderRowStyle",
            "PromptNestTagChipStyle",
            "PromptNestCommandTabStyle",
            "PromptNestEditorTextBoxStyle",
            "PromptNestStatusTextStyle"
        ];

        GetResourceKeys(styles).Should().Contain(requiredStyles);
    }

    [Fact]
    public void MainWindowDefinesStableShellHostRegions()
    {
        XDocument window = LoadAppXaml("Views", "MainWindow.xaml");

        string[] requiredNames =
        [
            "ShellRoot",
            "AppTitleBar",
            "NavigationRailHost",
            "SidebarHost",
            "LibraryHost",
            "EditorHost",
            "EditorPane",
            "EditorColumn"
        ];

        GetNamedElements(window).Should().Contain(requiredNames);
    }

    private static XDocument LoadDictionary(string fileName)
    {
        return LoadAppXaml("Styles", fileName);
    }

    private static XDocument LoadAppXaml(params string[] pathSegments)
    {
        string? solutionRoot = AppContext.BaseDirectory;
        while (solutionRoot is not null && !File.Exists(Path.Combine(solutionRoot, "PromptNest.sln")))
        {
            solutionRoot = Directory.GetParent(solutionRoot)?.FullName;
        }

        solutionRoot.Should().NotBeNull("the test is run from a build output under the solution");

        string path = Path.Combine([solutionRoot!, "src", "PromptNest.App", .. pathSegments]);
        return XDocument.Load(path);
    }

    private static string[] GetResourceKeys(XDocument dictionary)
    {
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

        return dictionary
            .Descendants()
            .Attributes(xaml + "Key")
            .Select(attribute => attribute.Value)
            .ToArray();
    }

    private static string[] GetNamedElements(XDocument dictionary)
    {
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

        return dictionary
            .Descendants()
            .Attributes(xaml + "Name")
            .Select(attribute => attribute.Value)
            .ToArray();
    }
}