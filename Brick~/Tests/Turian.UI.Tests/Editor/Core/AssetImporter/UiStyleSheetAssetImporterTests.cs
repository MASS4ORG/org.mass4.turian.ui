namespace Turian.Tests;

/// <summary>Tests for <see cref="UiStyleSheetAssetImporter"/> — extension match, validation, artifact copy.</summary>
public sealed class UiStyleSheetAssetImporterTests
{
    readonly UiStyleSheetAssetImporter importer = new();

    /// <summary>Only <c>.pss</c> files (any case) are accepted.</summary>
    [Theory]
    [InlineData("theme.pss", true)]
    [InlineData("theme.PSS", true)]
    [InlineData("theme.uss", false)]
    [InlineData("menu.ui", false)]
    [InlineData("", false)]
    public void IsValid_MatchesPssExtension(string path, bool expected) =>
        Assert.Equal(expected, importer.IsValid(path));

    /// <summary>Creating an asset from a path yields a <see cref="UiStyleSheetAsset"/> with that relative path.</summary>
    [Fact]
    public void CreateAsset_ReturnsUiStyleSheetAsset()
    {
        var asset = importer.CreateAsset("Assets/UI/theme.pss");
        var sheet = Assert.IsType<UiStyleSheetAsset>(asset);
        Assert.Equal("Assets/UI/theme.pss", sheet.RelativePath);
    }

    /// <summary>Importing copies a valid sheet to the cache unchanged.</summary>
    [Fact]
    public void ImportToCache_CopiesValidSheet()
    {
        var root = Path.Combine(Path.GetTempPath(), $"pss-import-{Guid.NewGuid():N}");
        var importDir = Path.Combine(root, "import");
        Directory.CreateDirectory(importDir);
        try
        {
            var source = Path.Combine(root, "theme.pss");
            File.WriteAllText(source,
                ".btn { background-color = #39435a; } .btn:hover { background-color = #4a90e2; }");

            var artifacts = importer.ImportToCache(importer.CreateAsset("theme.pss"), source, importDir);

            var copied = Path.Combine(importDir, Assert.Single(artifacts));
            Assert.True(File.Exists(copied));
            Assert.Contains(".btn", File.ReadAllText(copied), StringComparison.Ordinal);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        }
    }

    /// <summary>A malformed sheet is still copied through instead of failing the import.</summary>
    [Fact]
    public void ImportToCache_MalformedSheet_CopiesSourceWithoutThrowing()
    {
        var root = Path.Combine(Path.GetTempPath(), $"pss-bad-{Guid.NewGuid():N}");
        var importDir = Path.Combine(root, "import");
        Directory.CreateDirectory(importDir);
        try
        {
            var source = Path.Combine(root, "bad.pss");
            File.WriteAllText(source, ".btn { color = #fff; "); // unterminated block

            var artifacts = importer.ImportToCache(importer.CreateAsset("bad.pss"), source, importDir);

            Assert.True(File.Exists(Path.Combine(importDir, Assert.Single(artifacts))));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        }
    }

    /// <summary>Stylesheets are strict PSS: CSS <c>prop: value;</c>, <c>--name</c> and <c>var()</c> are rejected.</summary>
    [Theory]
    [InlineData(".btn { color: #fff; }")]
    [InlineData("--accent: #4a90e2;")]
    [InlineData("$accent = #4a90e2; .btn { color = var(--accent); }")]
    public void CssSyntaxIsRejected(string css)
    {
        var path = Path.Combine(Path.GetTempPath(), $"css-{Guid.NewGuid():N}.pss");
        File.WriteAllText(path, css);
        try
        {
            Assert.ThrowsAny<FormatException>(() => UiStyleSheetAsset.LoadContent(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
