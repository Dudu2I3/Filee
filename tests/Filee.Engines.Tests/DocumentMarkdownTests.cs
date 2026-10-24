// Word, Excel, PowerPoint, CSV, text and HTML → Markdown with the built-in readers and MarkdownWriter (no Pandoc).

using Filee.Core.Conversion;
using Filee.Core.Presets;
using static Filee.Engines.Tests.DocxBuilder;

namespace Filee.Engines.Tests;

public class DocumentMarkdownTests(EngineFixture fx) : IClassFixture<EngineFixture>
{
    [Theory]
    [InlineData("docx")]
    [InlineData("xlsx")]
    [InlineData("csv")]
    [InlineData("pptx")]
    [InlineData("txt")]
    [InlineData("html")]
    public void Built_in_documents_reach_markdown_in_one_step(string from)
    {
        var route = new ConverterCatalog(fx.Converters.Where(c => c.Id is not ("libreoffice" or "ffmpeg"))).CreatePlanner().Plan(from, "md");

        Assert.NotNull(route);
        Assert.Equal("document-markdown", Assert.Single(route.Steps).Converter.Id);
    }

    [Fact]
    public void Markdown_is_never_a_step_of_another_route()
    {
        var planner = fx.Catalog.CreatePlanner(["libreoffice"]);

        Assert.DoesNotContain(planner.Plan("docx", "txt")!.Steps, s => s.To == "md");
        Assert.DoesNotContain(planner.Plan("xlsx", "html")!.Steps, s => s.To == "md");
    }

    [Fact]
    public async Task A_word_document_keeps_its_headings_and_text()
    {
        var docx = new DocxBuilder();
        docx.Paragraph(P(R("Report"), "<w:pStyle w:val=\"Heading1\"/>"));
        docx.Paragraph(P(R("The ") + R("quick", "<w:b/>") + R(" fox.")));
        var path = docx.Save(Path.Combine(fx.NewFolder(), "report.docx"));

        var job = await fx.ConvertAsync([path], new Preset { TargetFormat = "md" });

        Assert.Equal(JobState.Completed, job.State);
        var markdown = await File.ReadAllTextAsync(job.Outputs.Single(), TestContext.Current.CancellationToken);
        Assert.Contains("# Report", markdown);
        Assert.Contains("**quick**", markdown);
    }

    [Fact]
    public async Task A_csv_table_becomes_a_markdown_table()
    {
        var path = Path.Combine(fx.NewFolder(), "data.csv");
        await File.WriteAllTextAsync(path, "name,qty\napple,3\npear,5\n", TestContext.Current.CancellationToken);

        var job = await fx.ConvertAsync([path], new Preset { TargetFormat = "md" });

        Assert.Equal(JobState.Completed, job.State);
        var markdown = await File.ReadAllTextAsync(job.Outputs.Single(), TestContext.Current.CancellationToken);
        Assert.Contains("apple", markdown);
        Assert.Contains("|", markdown);
    }
}
