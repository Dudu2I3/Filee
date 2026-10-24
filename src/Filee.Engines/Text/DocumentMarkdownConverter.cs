// Word, Excel, PowerPoint, CSV, plain text and HTML → Markdown, in-process: a built-in reader (DocumentReaders) turns the
// source into the document model and MarkdownWriter writes it, as for a PDF (PdfTextConverter). Pandoc did this in the
// full Filee; the lite build has none. Pictures go into a "<name>_files" folder next to the Markdown file.

using System.Text;
using Filee.Core.Conversion;
using Filee.Engines.Hwp.Hwpx;
using Filee.Engines.Infrastructure;
using Filee.Engines.Pdf;

namespace Filee.Engines.Text;

/// <summary>Writes Markdown from the documents Filee reads itself.</summary>
public sealed class DocumentMarkdownConverter : IConverter
{
    /// <summary>Formats read by the built-in readers whose Markdown is not another engine's job (PDF has its own).</summary>
    private static readonly string[] Sources = ["docx", "xlsx", "csv", "pptx", "txt", "html"];

    public string Id => "document-markdown";
    public string DisplayName => "Document to Markdown (built-in)";
    public int MaxParallelism => 0;

    // Direct only, like PDF → Markdown: no other route starts going through a Markdown file and its picture folder.
    public IReadOnlyList<ConversionEdge> Edges { get; } =
        Sources.Select(from => new ConversionEdge(from, "md", 10, DirectOnly: true)).ToList();

    public EngineStatus GetStatus() =>
        EngineStatus.Available(string.Join(", ", Sources.Select(f => f.ToUpperInvariant())) + " → Markdown", EngineVersions.BuiltIn);

    public async Task<IReadOnlyList<string>> ConvertAsync(ConversionStep step, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        progress?.Report(0.1);
        var document = await DocumentReaders.ReadAsync(step.InputPath, step.From, step.WorkDirectory, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report(0.6);

        foreach (var paragraph in document.Sections.SelectMany(s => s.Blocks).OfType<HParagraph>().Where(p => p.HeadingLevel > 0))
            PdfTextConverter.NotBold(paragraph.Inlines);

        var output = step.Output.Allocate("md");
        if (output is null)
            return [];
        Func<string, string?> pictures = step.Preset.Document.LeaveOutPictures
            ? _ => null
            : new MarkdownPictures(output, step.Output, step.WorkDirectory).Add;
        var markdown = MarkdownWriter.Write(document, pictures);
        await File.WriteAllTextAsync(output, markdown, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), cancellationToken);
        progress?.Report(1);
        return [output];
    }
}
