// Office Open XML variants (macro-enabled files, templates, slide shows) without Office: the package is copied with
// the main part's content type rewritten, and macros are dropped when the target may not hold them. The built-in
// readers accept the variants directly (DOCM → HWPX, XLSM → CSV, PPSX → DOCX ...).

using System.IO.Compression;
using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using Filee.Core.Conversion;
using Filee.Core.Presets;
using Filee.Engines.Office;
using Filee.Engines.Office.Sheets;
using static Filee.Engines.Tests.DocxBuilder;

namespace Filee.Engines.Tests;

public class OoxmlVariantTests(EngineFixture fx) : IClassFixture<EngineFixture>
{
    private static readonly XNamespace Types = "http://schemas.openxmlformats.org/package/2006/content-types";
    private static readonly XNamespace Rels = "http://schemas.openxmlformats.org/package/2006/relationships";
    private const string VbaRelationship = "http://schemas.microsoft.com/office/2006/relationships/vbaProject";

    private async Task<string> ConvertAsync(string input, string target)
    {
        var job = await fx.ConvertAsync([input], new Preset { TargetFormat = target });
        Assert.True(job.State == JobState.Completed, string.Join("; ", job.Files.Select(f => $"{f.ErrorKey} {f.ErrorDetail}")));
        return job.Outputs.Single();
    }

    /// <summary>
    /// Turns a macro-free package into the macro-enabled variant <paramref name="format"/>: main content type, a VBA
    /// project (with its data part, as Word keeps it) related from the main part, and for Excel an Excel 4.0 macro sheet.
    /// </summary>
    private static string AddMacros(string package, string format, string folder)
    {
        var path = Path.ChangeExtension(package, "." + format);
        File.Copy(package, path, overwrite: true);
        using var zip = ZipFile.Open(path, ZipArchiveMode.Update);
        var main = XDocument.Load(zip.GetEntry("_rels/.rels")!.Open()).Root!.Elements(Rels + "Relationship")
            .Single(r => ((string)r.Attribute("Type")!).EndsWith("/officeDocument", StringComparison.Ordinal)).Attribute("Target")!.Value.TrimStart('/');
        var mainFolder = main[..main.LastIndexOf('/')];
        var relsPath = $"{mainFolder}/_rels/{Path.GetFileName(main)}.rels";

        var types = Load(zip, "[Content_Types].xml");
        types.Root!.Elements(Types + "Override").Single(o => (string)o.Attribute("PartName")! == "/" + main)
            .SetAttributeValue("ContentType", OoxmlConverter.Variants[format].ContentType);
        types.Root.AddFirst(new XElement(Types + "Default", new XAttribute("Extension", "bin"), new XAttribute("ContentType", "application/vnd.ms-office.vbaProject")));
        types.Root.Add(new XElement(Types + "Override", new XAttribute("PartName", $"/{mainFolder}/vbaData.xml"), new XAttribute("ContentType", "application/vnd.ms-word.vbaData+xml")));

        var rels = zip.GetEntry(relsPath) is { } existing ? Load(zip, relsPath) : new XDocument(new XElement(Rels + "Relationships"));
        rels.Root!.Add(new XElement(Rels + "Relationship", new XAttribute("Id", "rIdVba"), new XAttribute("Type", VbaRelationship), new XAttribute("Target", "vbaProject.bin")));
        Write(zip, $"{mainFolder}/vbaProject.bin", [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1]);
        Write(zip, $"{mainFolder}/_rels/vbaProject.bin.rels",
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.microsoft.com/office/2006/relationships/wordVbaData\" Target=\"vbaData.xml\"/></Relationships>"u8.ToArray());
        Write(zip, $"{mainFolder}/vbaData.xml", "<wne:vbaSuppData xmlns:wne=\"http://schemas.microsoft.com/office/word/2006/wordml\"/>"u8.ToArray());

        if (format == "xlsm")
        {
            // An Excel 4.0 macro sheet, listed in the workbook like a sheet.
            types.Root.Add(new XElement(Types + "Override", new XAttribute("PartName", "/xl/macrosheets/sheet1.xml"), new XAttribute("ContentType", "application/vnd.ms-excel.macrosheet+xml")));
            rels.Root.Add(new XElement(Rels + "Relationship", new XAttribute("Id", "rIdMacro"), new XAttribute("Type", "http://schemas.microsoft.com/office/2006/relationships/xlMacrosheet"), new XAttribute("Target", "macrosheets/sheet1.xml")));
            Write(zip, "xl/macrosheets/sheet1.xml", "<xm:macrosheet xmlns:xm=\"http://schemas.microsoft.com/office/excel/2006/main\"/>"u8.ToArray());
            var workbook = Load(zip, "xl/workbook.xml");
            XNamespace s = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            XNamespace r = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            workbook.Root!.Element(s + "sheets")!.Add(new XElement(s + "sheet", new XAttribute("name", "Macro1"), new XAttribute("sheetId", 99), new XAttribute(r + "id", "rIdMacro")));
            Save(zip, "xl/workbook.xml", workbook);
        }
        Save(zip, "[Content_Types].xml", types);
        Save(zip, relsPath, rels);
        return path;
    }

    private static XDocument Load(ZipArchive zip, string name)
    {
        using var stream = zip.GetEntry(name)!.Open();
        return XDocument.Load(stream);
    }

    private static void Save(ZipArchive zip, string name, XDocument xml)
    {
        zip.GetEntry(name)?.Delete();
        using var stream = zip.CreateEntry(name).Open();
        xml.Save(stream);
    }

    private static void Write(ZipArchive zip, string name, byte[] bytes)
    {
        using var stream = zip.CreateEntry(name).Open();
        stream.Write(bytes);
    }

    /// <summary>No macro part, no content type or relationship pointing to one.</summary>
    private static void AssertNoMacros(string path)
    {
        using var zip = ZipFile.OpenRead(path);
        Assert.DoesNotContain(zip.Entries, e => e.Name is "vbaProject.bin" or "vbaData.xml" or "vbaProject.bin.rels" || e.FullName.Contains("macrosheets", StringComparison.Ordinal));
        var types = Load(zip, "[Content_Types].xml").ToString();
        Assert.DoesNotContain("vbaProject", types);
        Assert.DoesNotContain("macrosheet", types);
        foreach (var rels in zip.Entries.Where(e => e.FullName.EndsWith(".rels", StringComparison.Ordinal)))
            Assert.DoesNotContain(Load(zip, rels.FullName).Root!.Elements(Rels + "Relationship"), r => ((string)r.Attribute("Type")!).StartsWith("http://schemas.microsoft.com/office/2006/relationships/", StringComparison.Ordinal) && ((string)r.Attribute("Type")!).Contains("Macro", StringComparison.OrdinalIgnoreCase) || (string)r.Attribute("Type")! == VbaRelationship);
    }

    private static string MainContentType(string path)
    {
        using var zip = ZipFile.OpenRead(path);
        var main = XDocument.Load(zip.GetEntry("_rels/.rels")!.Open()).Root!.Elements(Rels + "Relationship")
            .Single(r => ((string)r.Attribute("Type")!).EndsWith("/officeDocument", StringComparison.Ordinal)).Attribute("Target")!.Value.TrimStart('/');
        return (string)Load(zip, "[Content_Types].xml").Root!.Elements(Types + "Override").Single(o => (string)o.Attribute("PartName")! == "/" + main).Attribute("ContentType")!;
    }

}
