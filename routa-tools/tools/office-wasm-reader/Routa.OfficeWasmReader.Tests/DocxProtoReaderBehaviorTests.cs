using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Google.Protobuf;
using System.Globalization;
using System.Text;
using Xunit;

namespace Routa.OfficeWasmReader.Tests;

/// <summary>
/// Focused behavior tests for DocxDocumentProtoReader that test specific
/// domain contracts through the public Read() API. These tests are more
/// targeted than the golden contract tests and describe *why* the output
/// is what it is, making them useful guards during refactoring.
/// </summary>
public class DocxProtoReaderBehaviorTests
{
    private static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMB/axm8R0AAAAASUVORK5CYII=");

    // ── helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a minimal in-memory .docx with a single body paragraph.
    /// </summary>
    private static byte[] BuildDocx(Action<Body> configure)
    {
        return BuildDocx((_, body) => configure(body));
    }

    private static byte[] BuildDocx(Action<MainDocumentPart, Body> configure)
    {
        using var ms = new MemoryStream();
        using var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document);
        var mainPart = doc.AddMainDocumentPart();
        mainPart.Document = new Document(new Body());
        configure(mainPart, mainPart.Document.Body!);
        mainPart.Document.Save();
        doc.Save();
        return ms.ToArray();
    }

    private static byte[] SingleParagraphDocx(string text)
    {
        return BuildDocx(body =>
            body.AppendChild(new Paragraph(new Run(new Text(text)))));
    }

    private static byte[] AnchoredImageDocx(
        long verticalOffsetEmu,
        string behindDoc = "0",
        int relativeHeight = 0)
    {
        return BuildDocx((mainPart, body) =>
        {
            var imagePart = mainPart.AddImagePart(ImagePartType.Png, "rIdImage1");
            using var imageStream = new MemoryStream(TinyPng);
            imagePart.FeedData(imageStream);
            body.AppendChild(new Paragraph(new Run(new Drawing(AnchoredImageDrawingXml(
                verticalOffsetEmu,
                behindDoc: behindDoc,
                relativeHeight: relativeHeight)))));
        });
    }

    private static byte[] CroppedImageDocx()
    {
        return BuildDocx((mainPart, body) =>
        {
            var imagePart = mainPart.AddImagePart(ImagePartType.Png, "rIdImage1");
            using var imageStream = new MemoryStream(TinyPng);
            imagePart.FeedData(imageStream);
            body.AppendChild(new Paragraph(new Run(new Drawing(AnchoredImageDrawingXml(
                0,
                "<a:srcRect l=\"10000\" t=\"5000\" r=\"30000\" b=\"20000\"/>")))));
        });
    }

    private static byte[] OutlinedImageDocx()
    {
        return BuildDocx((mainPart, body) =>
        {
            var imagePart = mainPart.AddImagePart(ImagePartType.Png, "rIdImage1");
            using var imageStream = new MemoryStream(TinyPng);
            imagePart.FeedData(imageStream);
            body.AppendChild(new Paragraph(new Run(new Drawing(AnchoredImageDrawingXml(
                0,
                shapePropertiesExtra: "<a:ln w=\"12700\"><a:solidFill><a:srgbClr val=\"FF0000\"/></a:solidFill></a:ln>")))));
        });
    }

    private static byte[] ShadowedImageDocx()
    {
        return BuildDocx((mainPart, body) =>
        {
            var imagePart = mainPart.AddImagePart(ImagePartType.Png, "rIdImage1");
            using var imageStream = new MemoryStream(TinyPng);
            imagePart.FeedData(imageStream);
            body.AppendChild(new Paragraph(new Run(new Drawing(AnchoredImageDrawingXml(
                0,
                shapePropertiesExtra: """
<a:effectLst><a:outerShdw blurRad="19050" dist="9525" dir="5400000"><a:srgbClr val="000000"><a:alpha val="50000"/></a:srgbClr></a:outerShdw></a:effectLst>
""")))));
        });
    }

    private static byte[] GroupedImageDocx()
    {
        return BuildDocx((mainPart, body) =>
        {
            var imagePart = mainPart.AddImagePart(ImagePartType.Png, "rIdImage1");
            using var imageStream = new MemoryStream(TinyPng);
            imagePart.FeedData(imageStream);
            body.AppendChild(new Paragraph(new Run(new Drawing(GroupedImageDrawingXml()))));
        });
    }

    private static byte[] AlternateContentImageDocx()
    {
        return BuildDocx((mainPart, body) =>
        {
            var imagePart = mainPart.AddImagePart(ImagePartType.Png, "rIdImage1");
            using var imageStream = new MemoryStream(TinyPng);
            imagePart.FeedData(imageStream);
            var run = new Run();
            run.InnerXml = $"""
<mc:AlternateContent xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006">
  <mc:Choice Requires="wps">
    {AnchoredImageDrawingXml(0)}
  </mc:Choice>
  <mc:Fallback>
    {AnchoredImageDrawingXml(914400)}
  </mc:Fallback>
</mc:AlternateContent>
""";
            body.AppendChild(new Paragraph(run));
        });
    }

    private static byte[] TextBoxDocx()
    {
        return BuildDocx((_, body) =>
        {
            body.AppendChild(new Paragraph(new Run(new Drawing(TextBoxDrawingXml()))));
        });
    }

    private static byte[] AlternateContentTextBoxDocx()
    {
        return BuildDocx((_, body) =>
        {
            var run = new Run();
            run.InnerXml = $"""
<mc:AlternateContent xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006">
  <mc:Choice Requires="wps">
    {TextBoxDrawingXml()}
  </mc:Choice>
  <mc:Fallback>
    <w:pict xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"/>
  </mc:Fallback>
</mc:AlternateContent>
""";
            body.AppendChild(new Paragraph(run));
        });
    }

    private static string TextBoxDrawingXml()
    {
        return """
<w:drawing xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main" xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:wps="http://schemas.microsoft.com/office/word/2010/wordprocessingShape">
  <wp:anchor allowOverlap="1" behindDoc="0" distB="0" distT="0" distL="0" distR="0" layoutInCell="1" relativeHeight="0" simplePos="0">
    <wp:simplePos x="0" y="0"/>
    <wp:positionH relativeFrom="page"><wp:posOffset>952500</wp:posOffset></wp:positionH>
    <wp:positionV relativeFrom="page"><wp:posOffset>1905000</wp:posOffset></wp:positionV>
    <wp:extent cx="1905000" cy="952500"/>
    <wp:effectExtent b="0" l="0" r="0" t="0"/>
    <wp:wrapNone/>
    <wp:docPr id="2" name="Text Box 1"/>
    <a:graphic>
      <a:graphicData uri="http://schemas.microsoft.com/office/word/2010/wordprocessingShape">
        <wps:wsp>
          <wps:txbx>
            <w:txbxContent>
              <w:p>
                <w:r><w:t>Box text</w:t></w:r>
              </w:p>
            </w:txbxContent>
          </wps:txbx>
        </wps:wsp>
      </a:graphicData>
    </a:graphic>
  </wp:anchor>
</w:drawing>
""";
    }

    private static string AnchoredImageDrawingXml(
        long verticalOffsetEmu,
        string sourceRectangle = "",
        string behindDoc = "0",
        int relativeHeight = 0,
        string shapePropertiesExtra = "")
    {
        var offset = verticalOffsetEmu.ToString(CultureInfo.InvariantCulture);
        var relativeHeightValue = relativeHeight.ToString(CultureInfo.InvariantCulture);
        return $"""
<w:drawing xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main" xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:pic="http://schemas.openxmlformats.org/drawingml/2006/picture" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
  <wp:anchor allowOverlap="1" behindDoc="{behindDoc}" distB="0" distT="0" distL="0" distR="0" layoutInCell="1" relativeHeight="{relativeHeightValue}" simplePos="0">
    <wp:simplePos x="0" y="0"/>
    <wp:positionH relativeFrom="page"><wp:posOffset>0</wp:posOffset></wp:positionH>
    <wp:positionV relativeFrom="page"><wp:posOffset>{offset}</wp:posOffset></wp:positionV>
    <wp:extent cx="914400" cy="914400"/>
    <wp:effectExtent b="0" l="0" r="0" t="0"/>
    <wp:wrapTopAndBottom distB="0" distT="0"/>
    <wp:docPr id="1" name="test.png"/>
    <a:graphic>
      <a:graphicData uri="http://schemas.openxmlformats.org/drawingml/2006/picture">
        <pic:pic>
          <pic:nvPicPr><pic:cNvPr id="0" name="test.png"/><pic:cNvPicPr/></pic:nvPicPr>
          <pic:blipFill><a:blip r:embed="rIdImage1"/>{sourceRectangle}<a:stretch><a:fillRect/></a:stretch></pic:blipFill>
          <pic:spPr><a:xfrm><a:off x="0" y="0"/><a:ext cx="914400" cy="914400"/></a:xfrm><a:prstGeom prst="rect"/>{shapePropertiesExtra}</pic:spPr>
        </pic:pic>
      </a:graphicData>
    </a:graphic>
  </wp:anchor>
</w:drawing>
""";
    }

    private static string GroupedImageDrawingXml()
    {
        return """
<w:drawing xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main" xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:pic="http://schemas.openxmlformats.org/drawingml/2006/picture" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships" xmlns:wpg="http://schemas.microsoft.com/office/word/2010/wordprocessingGroup" xmlns:wps="http://schemas.microsoft.com/office/word/2010/wordprocessingShape">
  <wp:anchor allowOverlap="1" behindDoc="0" distB="0" distT="0" distL="0" distR="0" layoutInCell="1" relativeHeight="0" simplePos="0">
    <wp:simplePos x="0" y="0"/>
    <wp:positionH relativeFrom="page"><wp:posOffset>0</wp:posOffset></wp:positionH>
    <wp:positionV relativeFrom="page"><wp:posOffset>0</wp:posOffset></wp:positionV>
    <wp:extent cx="5731200" cy="2794000"/>
    <wp:effectExtent b="0" l="0" r="0" t="0"/>
    <wp:wrapNone/>
    <wp:docPr id="1" name="Group 1"/>
    <a:graphic>
      <a:graphicData uri="http://schemas.microsoft.com/office/word/2010/wordprocessingGroup">
        <wpg:wgp>
          <wpg:grpSpPr>
            <a:xfrm>
              <a:off x="634500" y="198525"/>
              <a:ext cx="5731200" cy="2794000"/>
              <a:chOff x="634500" y="198525"/>
              <a:chExt cx="6735750" cy="3288450"/>
            </a:xfrm>
          </wpg:grpSpPr>
          <wps:wsp>
            <wps:spPr>
              <a:xfrm>
                <a:off x="634500" y="198525"/>
                <a:ext cx="5731200" cy="2794000"/>
              </a:xfrm>
              <a:prstGeom prst="rect"/>
              <a:solidFill><a:srgbClr val="FFFFFF"/></a:solidFill>
              <a:ln><a:solidFill><a:srgbClr val="EDF1F3"/></a:solidFill></a:ln>
            </wps:spPr>
          </wps:wsp>
          <pic:pic>
            <pic:nvPicPr><pic:cNvPr id="2" name="diagram.png"/><pic:cNvPicPr/></pic:nvPicPr>
            <pic:blipFill>
              <a:blip r:embed="rIdImage1"/>
              <a:srcRect b="0" l="14917" r="11488" t="6681"/>
              <a:stretch><a:fillRect/></a:stretch>
            </pic:blipFill>
            <pic:spPr>
              <a:xfrm>
                <a:off x="2604800" y="287300"/>
                <a:ext cx="2795150" cy="3110900"/>
              </a:xfrm>
              <a:prstGeom prst="rect"/>
            </pic:spPr>
          </pic:pic>
        </wpg:wgp>
      </a:graphicData>
    </a:graphic>
  </wp:anchor>
</w:drawing>
""";
    }

    // ── empty / null input ────────────────────────────────────────────────────

    [Fact]
    public void Read_EmptyDocument_ReturnsNonEmptyBytes()
    {
        var docx = BuildDocx(_ => { });
        var result = DocxDocumentProtoReader.Read(docx);
        Assert.NotEmpty(result);
    }

    // ── text extraction ───────────────────────────────────────────────────────

    [Fact]
    public void Read_SingleParagraph_ProtoIsNonEmpty()
    {
        var docx = SingleParagraphDocx("Hello world");
        var result = DocxDocumentProtoReader.Read(docx);
        Assert.NotEmpty(result);
    }

    [Fact]
    public void Read_TwoParagraphs_ProtoLargerThanOne()
    {
        var singleDocx = SingleParagraphDocx("Alpha");
        var singleBytes = DocxDocumentProtoReader.Read(singleDocx).Length;
        var doubleDocx = BuildDocx(body =>
        {
            body.AppendChild(new Paragraph(new Run(new Text("Alpha"))));
            body.AppendChild(new Paragraph(new Run(new Text("Beta"))));
        });
        var doubleBytes = DocxDocumentProtoReader.Read(doubleDocx).Length;
        Assert.True(doubleBytes > singleBytes,
            "Document with more paragraphs should produce larger proto output");
    }

    [Fact]
    public void Read_DeterministicOutput_SameInputSameOutput()
    {
        var docx = SingleParagraphDocx("Stable content");
        var first = DocxDocumentProtoReader.Read(docx);
        var second = DocxDocumentProtoReader.Read(docx);
        Assert.Equal(first, second);
    }

    // ── table ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Read_SingleCellTable_ProtoIsNonEmpty()
    {
        var docx = BuildDocx(body =>
        {
            var table = new Table(
                new TableRow(
                    new TableCell(new Paragraph(new Run(new Text("Cell"))))));
            body.AppendChild(table);
        });
        var result = DocxDocumentProtoReader.Read(docx);
        Assert.NotEmpty(result);
    }

    [Fact]
    public void Read_Table_ProtoLargerThanParagraph()
    {
        var paragraphDocx = SingleParagraphDocx("Cell text");
        var tableDocx = BuildDocx(body =>
        {
            var table = new Table(
                new TableRow(
                    new TableCell(new Paragraph(new Run(new Text("Cell text"))))));
            body.AppendChild(table);
        });

        var paragraphSize = DocxDocumentProtoReader.Read(paragraphDocx).Length;
        var tableSize = DocxDocumentProtoReader.Read(tableDocx).Length;
        Assert.True(tableSize > paragraphSize,
            "Table element should produce more proto bytes than a plain paragraph");
    }

    // ── hyperlink ─────────────────────────────────────────────────────────────

    [Fact]
    public void Read_ParagraphWithHyperlink_ProtoIsNonEmpty()
    {
        var docx = BuildDocx(body =>
        {
            var para = new Paragraph();
            var hyperlink = new Hyperlink(new Run(new Text("Click me")))
            {
                Anchor = new StringValue("bookmark1"),
            };
            para.AppendChild(hyperlink);
            body.AppendChild(para);
        });

        var result = DocxDocumentProtoReader.Read(docx);
        Assert.NotEmpty(result);
    }

    [Fact]
    public void Read_GeneratedTableOfContentsContentControl_RetainsVisibleEntries()
    {
        var docx = BuildDocx(body =>
        {
            body.AppendChild(new SdtBlock(
                new SdtProperties(
                    new SdtContentDocPartObject(
                        new DocPartGallery { Val = "Table of Contents" },
                        new DocPartUnique { Val = true })),
                new SdtContentBlock(
                    new Paragraph(new Run(new Text("Thoughtworks approach to operations and maintenance"))),
                    new Paragraph(new Run(new Text("Service configuration"))))));
        });

        var result = DocxDocumentProtoReader.Read(docx);
        var protoText = Encoding.UTF8.GetString(result);
        Assert.Contains("Thoughtworks approach to operations and maintenance", protoText);
        Assert.Contains("Service configuration", protoText);
    }

    [Fact]
    public void Read_PageRelativeAnchorVerticalOffset_AffectsImageBoundingBox()
    {
        var topAnchored = DocxDocumentProtoReader.Read(AnchoredImageDocx(0));
        var lowerAnchored = DocxDocumentProtoReader.Read(AnchoredImageDocx(914_400));

        Assert.NotEqual(topAnchored, lowerAnchored);
    }

    [Fact]
    public void Read_ImageSourceRectangle_MatchesWalnutByIgnoringCropFill()
    {
        var uncropped = DocxDocumentProtoReader.Read(AnchoredImageDocx(0));
        var cropped = DocxDocumentProtoReader.Read(CroppedImageDocx());

        Assert.Equal(uncropped, cropped);
    }

    [Fact]
    public void Read_ImageOutline_AffectsImageProto()
    {
        var plain = DocxDocumentProtoReader.Read(AnchoredImageDocx(0));
        var outlined = DocxDocumentProtoReader.Read(OutlinedImageDocx());

        Assert.True(outlined.Length > plain.Length);
    }

    [Fact]
    public void Read_ImageShadow_AffectsImageProto()
    {
        var plain = DocxDocumentProtoReader.Read(AnchoredImageDocx(0));
        var shadowed = DocxDocumentProtoReader.Read(ShadowedImageDocx());

        Assert.True(shadowed.Length > plain.Length);
    }

    [Fact]
    public void Read_GroupedImage_UsesPictureTransformInsteadOfGroupFrame()
    {
        var result = DocxDocumentProtoReader.Read(GroupedImageDocx());
        var imageBox = DecodeFirstImageBox(result);
        var shapeBoxes = DecodeShapeBoxes(result);

        Assert.NotNull(imageBox);
        Assert.Single(shapeBoxes);
        Assert.Equal(0, shapeBoxes[0].XEmu);
        Assert.Equal(0, shapeBoxes[0].YEmu);
        Assert.Equal(5731200, shapeBoxes[0].WidthEmu);
        Assert.Equal(2794000, shapeBoxes[0].HeightEmu);
        Assert.Equal(ScaledEmu(2604800 - 634500, 5731200, 6735750), imageBox.Value.XEmu);
        Assert.Equal(ScaledEmu(287300 - 198525, 2794000, 3288450), imageBox.Value.YEmu);
        Assert.Equal(ScaledEmu(2795150, 5731200, 6735750), imageBox.Value.WidthEmu);
        Assert.Equal(ScaledEmu(3110900, 2794000, 3288450), imageBox.Value.HeightEmu);
        Assert.NotEqual(5731200, imageBox.Value.WidthEmu);
        Assert.NotEqual(2794000, imageBox.Value.HeightEmu);
    }

    [Fact]
    public void Read_AlternateContentImage_SkipsFallbackDuplicate()
    {
        var result = DocxDocumentProtoReader.Read(AlternateContentImageDocx());
        var imageBoxes = DecodeImageBoxes(result);

        Assert.Single(imageBoxes);
        Assert.Equal(0, imageBoxes[0].YEmu);
    }

    [Fact]
    public void Read_TextBox_PreservesPositionedText()
    {
        var result = DocxDocumentProtoReader.Read(TextBoxDocx());

        Assert.Contains("Box text", Encoding.UTF8.GetString(result));
    }

    [Fact]
    public void Read_AlternateContentTextBox_SkipsChoiceTextBoxContent()
    {
        var result = DocxDocumentProtoReader.Read(AlternateContentTextBoxDocx());

        Assert.DoesNotContain("Box text", Encoding.UTF8.GetString(result));
    }

    [Fact]
    public void Read_AnchoredImageLayering_AffectsImageProto()
    {
        var foreground = DocxDocumentProtoReader.Read(AnchoredImageDocx(
            0,
            behindDoc: "0",
            relativeHeight: 10));
        var behindText = DocxDocumentProtoReader.Read(AnchoredImageDocx(
            0,
            behindDoc: "1",
            relativeHeight: 10));

        Assert.NotEqual(foreground, behindText);
    }

    // ── review marks ──────────────────────────────────────────────────────────

    [Fact]
    public void Read_InsertedRun_ProtoIsNonEmpty()
    {
        var docx = BuildDocx(body =>
        {
            var para = new Paragraph(
                new InsertedRun(new Run(new Text("Inserted text")))
                {
                    Id = new StringValue("1"),
                    Author = "Author A",
                    Date = new DateTimeValue(DateTime.UtcNow),
                });
            body.AppendChild(para);
        });

        var result = DocxDocumentProtoReader.Read(docx);
        Assert.NotEmpty(result);
    }

    // ── section ───────────────────────────────────────────────────────────────

    [Fact]
    public void Read_DocumentWithExplicitPageSize_ProtoIsNonEmpty()
    {
        var docx = BuildDocx(body =>
        {
            body.AppendChild(new Paragraph(new Run(new Text("Content"))));
            body.AppendChild(new SectionProperties(
                new PageSize { Width = 12_240U, Height = 15_840U }));
        });

        var result = DocxDocumentProtoReader.Read(docx);
        Assert.NotEmpty(result);
    }

    [Fact]
    public void Read_SectionWithPageSizeOnly_DoesNotInventPageMargin()
    {
        var docx = BuildDocx(body =>
        {
            body.AppendChild(new Paragraph(new Run(new Text("Content"))));
            body.AppendChild(new SectionProperties(
                new PageSize { Width = 12_240U, Height = 15_840U }));
        });

        var result = DocxDocumentProtoReader.Read(docx);

        Assert.False(FirstSectionPageSetupHasMargin(result));
    }

    [Fact]
    public void Read_TrailingBodySectionAfterBreakExpansion_RetainsFinalSection()
    {
        var docx = BuildDocx(body =>
        {
            body.AppendChild(new Paragraph(new Run(new Text("First section"))));
            body.AppendChild(new Paragraph(
                new ParagraphProperties(new SectionProperties(
                    new SectionType { Val = SectionMarkValues.NextPage },
                    new PageSize { Width = 12_240U, Height = 15_840U })),
                new Run(new Text("Before hard-break section"))));
            body.AppendChild(new Paragraph(
                new Run(new Break { Type = BreakValues.Page }),
                new Run(new Text("Middle hard-break section"))));
            body.AppendChild(new Paragraph(
                new ParagraphProperties(new SectionProperties(
                    new SectionType { Val = SectionMarkValues.NextPage },
                    new PageSize { Width = 12_240U, Height = 15_840U })),
                new Run(new Text("Before trailing body section"))));
            body.AppendChild(new Paragraph(new Run(new Text("Final contact section"))));
            body.AppendChild(new SectionProperties(
                new SectionType { Val = SectionMarkValues.NextPage },
                new PageSize { Width = 12_240U, Height = 15_840U }));
        });

        var protoText = Encoding.UTF8.GetString(DocxDocumentProtoReader.Read(docx));
        Assert.Contains("section-4", protoText);
        Assert.Contains("Final contact section", protoText);
    }

    private static ImageBox? DecodeFirstImageBox(byte[] protoBytes)
    {
        var boxes = DecodeImageBoxes(protoBytes);
        return boxes.Count == 0 ? null : boxes[0];
    }

    private static List<ImageBox> DecodeImageBoxes(byte[] protoBytes)
    {
        var input = new CodedInputStream(protoBytes);
        var imageBoxes = new List<ImageBox>();
        while (input.ReadTag() is var tag && tag != 0)
        {
            if (WireFormat.GetTagFieldNumber(tag) == 5)
            {
                var imageBox = DecodeImageElementBox(input.ReadBytes().ToByteArray());
                if (imageBox is not null)
                {
                    imageBoxes.Add(imageBox.Value);
                }
            }
            else
            {
                input.SkipLastField();
            }
        }

        return imageBoxes;
    }

    private static List<ImageBox> DecodeShapeBoxes(byte[] protoBytes)
    {
        var input = new CodedInputStream(protoBytes);
        var shapeBoxes = new List<ImageBox>();
        while (input.ReadTag() is var tag && tag != 0)
        {
            if (WireFormat.GetTagFieldNumber(tag) == 5)
            {
                var shapeBox = DecodeShapeElementBox(input.ReadBytes().ToByteArray());
                if (shapeBox is not null)
                {
                    shapeBoxes.Add(shapeBox.Value);
                }
            }
            else
            {
                input.SkipLastField();
            }
        }

        return shapeBoxes;
    }

    private static ImageBox? DecodeImageElementBox(byte[] elementBytes)
    {
        var input = new CodedInputStream(elementBytes);
        ImageBox? box = null;
        var elementType = 0;
        while (input.ReadTag() is var tag && tag != 0)
        {
            switch (WireFormat.GetTagFieldNumber(tag))
            {
                case 1:
                    box = DecodeImageBox(input.ReadBytes().ToByteArray());
                    break;
                case 11:
                    elementType = input.ReadInt32();
                    break;
                default:
                    input.SkipLastField();
                    break;
            }
        }

        return elementType == 7 ? box : null;
    }

    private static ImageBox? DecodeShapeElementBox(byte[] elementBytes)
    {
        var input = new CodedInputStream(elementBytes);
        ImageBox? box = null;
        var elementType = 0;
        var hasFill = false;
        var hasLine = false;
        while (input.ReadTag() is var tag && tag != 0)
        {
            switch (WireFormat.GetTagFieldNumber(tag))
            {
                case 1:
                    box = DecodeImageBox(input.ReadBytes().ToByteArray());
                    break;
                case 11:
                    elementType = input.ReadInt32();
                    break;
                case 19:
                    hasFill = true;
                    input.SkipLastField();
                    break;
                case 30:
                    hasLine = true;
                    input.SkipLastField();
                    break;
                default:
                    input.SkipLastField();
                    break;
            }
        }

        return elementType == 1 && (hasFill || hasLine) ? box : null;
    }

    private static ImageBox DecodeImageBox(byte[] boxBytes)
    {
        var input = new CodedInputStream(boxBytes);
        long x = 0;
        long y = 0;
        long width = 0;
        long height = 0;
        while (input.ReadTag() is var tag && tag != 0)
        {
            switch (WireFormat.GetTagFieldNumber(tag))
            {
                case 1:
                    x = input.ReadInt64();
                    break;
                case 2:
                    y = input.ReadInt64();
                    break;
                case 3:
                    width = input.ReadInt64();
                    break;
                case 4:
                    height = input.ReadInt64();
                    break;
                default:
                    input.SkipLastField();
                    break;
            }
        }

        return new ImageBox(x, y, width, height);
    }

    private static bool FirstSectionPageSetupHasMargin(byte[] protoBytes)
    {
        var input = new CodedInputStream(protoBytes);
        while (input.ReadTag() is var tag && tag != 0)
        {
            if (WireFormat.GetTagFieldNumber(tag) == 13)
            {
                return SectionPageSetupHasMargin(input.ReadBytes().ToByteArray());
            }

            input.SkipLastField();
        }

        return false;
    }

    private static bool SectionPageSetupHasMargin(byte[] sectionBytes)
    {
        var input = new CodedInputStream(sectionBytes);
        while (input.ReadTag() is var tag && tag != 0)
        {
            if (WireFormat.GetTagFieldNumber(tag) == 3)
            {
                return PageSetupHasMargin(input.ReadBytes().ToByteArray());
            }

            input.SkipLastField();
        }

        return false;
    }

    private static bool PageSetupHasMargin(byte[] pageSetupBytes)
    {
        var input = new CodedInputStream(pageSetupBytes);
        while (input.ReadTag() is var tag && tag != 0)
        {
            if (WireFormat.GetTagFieldNumber(tag) == 3)
            {
                return true;
            }

            input.SkipLastField();
        }

        return false;
    }

    private static long ScaledEmu(long value, long outer, long child)
    {
        return (long)Math.Round(value * ((double)outer / child), MidpointRounding.AwayFromZero);
    }

    private readonly record struct ImageBox(long XEmu, long YEmu, long WidthEmu, long HeightEmu);
}
