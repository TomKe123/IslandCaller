using System.IO.Compression;
using System.Security;
using System.Text;

namespace IslandCaller.Services;

public sealed class CallRecordExportService(HistoryService historyService)
{
    private readonly HistoryService _historyService = historyService;

    public void ExportToXlsx(string filePath)
    {
        var records = _historyService.GetAllCallRecords();
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        using var archive = ZipFile.Open(filePath, ZipArchiveMode.Create);
        WriteEntry(archive, "[Content_Types].xml", BuildContentTypesXml());
        WriteEntry(archive, "_rels/.rels", BuildRootRelsXml());
        WriteEntry(archive, "xl/workbook.xml", BuildWorkbookXml());
        WriteEntry(archive, "xl/_rels/workbook.xml.rels", BuildWorkbookRelsXml());
        WriteEntry(archive, "xl/styles.xml", BuildStylesXml());
        WriteEntry(archive, "xl/worksheets/sheet1.xml", BuildWorksheetXml(records));
    }

    private static void WriteEntry(ZipArchive archive, string entryPath, string content)
    {
        var entry = archive.CreateEntry(entryPath, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(content);
    }

    private static string BuildContentTypesXml()
    {
        return """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
  <Default Extension="xml" ContentType="application/xml"/>
  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
  <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
  <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
</Types>
""";
    }

    private static string BuildRootRelsXml()
    {
        return """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
</Relationships>
""";
    }

    private static string BuildWorkbookXml()
    {
        return """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
  <sheets>
    <sheet name="点名记录" sheetId="1" r:id="rId1"/>
  </sheets>
</workbook>
""";
    }

    private static string BuildWorkbookRelsXml()
    {
        return """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
  <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
</Relationships>
""";
    }

    private static string BuildStylesXml()
    {
        return """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
  <fonts count="1">
    <font>
      <sz val="11"/>
      <name val="Calibri"/>
    </font>
  </fonts>
  <fills count="2">
    <fill><patternFill patternType="none"/></fill>
    <fill><patternFill patternType="gray125"/></fill>
  </fills>
  <borders count="1">
    <border><left/><right/><top/><bottom/><diagonal/></border>
  </borders>
  <cellStyleXfs count="1">
    <xf numFmtId="0" fontId="0" fillId="0" borderId="0"/>
  </cellStyleXfs>
  <cellXfs count="2">
    <xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>
    <xf numFmtId="165" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/>
  </cellXfs>
  <cellStyles count="1">
    <cellStyle name="Normal" xfId="0" builtinId="0"/>
  </cellStyles>
  <numFmts count="1">
    <numFmt numFmtId="165" formatCode="yyyy-mm-dd hh:mm:ss"/>
  </numFmts>
</styleSheet>
""";
    }

    private static string BuildWorksheetXml(IReadOnlyList<HistoryService.CallRecordItem> records)
    {
        StringBuilder rows = new();
        rows.AppendLine("<row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>序号</t></is></c><c r=\"B1\" t=\"inlineStr\"><is><t>姓名</t></is></c><c r=\"C1\" t=\"inlineStr\"><is><t>时间</t></is></c></row>");

        for (int i = 0; i < records.Count; i++)
        {
            var record = records[i];
            int rowNumber = i + 2;
            rows.Append("<row r=\"").Append(rowNumber).Append("\">");
            rows.Append("<c r=\"A").Append(rowNumber).Append("\"><v>").Append(record.Index).Append("</v></c>");
            rows.Append("<c r=\"B").Append(rowNumber).Append("\" t=\"inlineStr\"><is><t>")
                .Append(Escape(record.Name)).Append("</t></is></c>");
            rows.Append("<c r=\"C").Append(rowNumber).Append("\" s=\"1\"><v>")
                .Append(ToExcelSerialNumber(record.OccurredAt))
                .Append("</v></c>");
            rows.AppendLine("</row>");
        }

        return $"""
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
  <sheetViews>
    <sheetView workbookViewId="0"/>
  </sheetViews>
  <sheetFormatPr defaultRowHeight="15"/>
  <cols>
    <col min="1" max="1" width="10" customWidth="1"/>
    <col min="2" max="2" width="20" customWidth="1"/>
    <col min="3" max="3" width="24" customWidth="1"/>
  </cols>
  <sheetData>
{rows}  </sheetData>
</worksheet>
""";
    }

    private static string Escape(string value)
    {
        return SecurityElement.Escape(value) ?? string.Empty;
    }

    private static string ToExcelSerialNumber(DateTime value)
    {
        DateTime local = value.Kind == DateTimeKind.Unspecified ? value : value.ToLocalTime();
        double serial = local.ToOADate();
        return serial.ToString("0.########", System.Globalization.CultureInfo.InvariantCulture);
    }
}
