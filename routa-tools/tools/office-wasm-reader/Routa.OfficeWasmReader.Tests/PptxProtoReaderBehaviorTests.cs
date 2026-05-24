using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using Google.Protobuf;
using System.Text;
using Xunit;
using P = DocumentFormat.OpenXml.Presentation;

namespace Routa.OfficeWasmReader.Tests;

/// <summary>
/// Focused behavior tests for PPTX protocol extraction paths that are hard to
/// exercise with coarse fixture-shell assertions.
/// </summary>
public class PptxProtoReaderBehaviorTests
{
    [Fact]
    public void Read_CustomGeometryEffectsAndChart_EmitsStructuredRecords()
    {
        var protoBytes = PptxPresentationProtoReader.Read(CustomGeometryEffectsAndChartPptx());
        var slides = MessagesForField(protoBytes, 1);
        var charts = MessagesForField(protoBytes, 9);

        Assert.Single(slides);
        Assert.Single(charts);

        var slideElements = MessagesForField(slides[0], 3);
        var customShape = Assert.Single(slideElements, element => StringField(element, 10) == "Custom Geometry");
        var radialShapeElement = Assert.Single(slideElements, element => StringField(element, 10) == "Radial Gradient");
        var customShapeFields = CountFields(customShape);
        var shape = Assert.Single(MessagesForField(customShape, 4));
        var radialShape = Assert.Single(MessagesForField(radialShapeElement, 4));
        var radialFill = Assert.Single(MessagesForField(radialShape, 5));
        var shapePath = Assert.Single(MessagesForField(shape, 9));
        var customLine = Assert.Single(MessagesForField(shape, 6));

        Assert.Equal(188, Int32Field(shape, 1)); // custom geometry
        Assert.Equal(2, Int32Field(radialFill, 1)); // gradient fill
        Assert.Equal(2, Int32Field(radialFill, 5)); // path gradient
        Assert.Equal(2, MessagesForField(radialFill, 3).Count);
        Assert.Equal(4, customShapeFields[15]); // shadow, glow, reflection, soft edges
        Assert.Equal(5, CountFields(shapePath)[3]); // move, line, quad, cubic, close
        Assert.Equal(2, Int32Field(Assert.Single(MessagesForField(customLine, 8)), 1)); // triangle head
        Assert.Equal(3, Int32Field(Assert.Single(MessagesForField(customLine, 9)), 1)); // stealth tail

        var chart = charts[0];
        var series = Assert.Single(MessagesForField(chart, 3));
        Assert.Equal("Quarterly Revenue", StringField(chart, 1));
        Assert.Contains("Q2", StringsForField(chart, 2));
        Assert.Equal("North", StringField(series, 1));
        Assert.Equal("series-00000000", StringField(series, 8));
        Assert.Equal(4, Int32Field(chart, 5)); // bar chart
    }

    [Fact]
    public void Read_MasterNonPlaceholderText_PreservesFooterText()
    {
        var protoBytes = PptxPresentationProtoReader.Read(MasterFooterTextPptx());
        var layouts = MessagesForField(protoBytes, 3);
        var master = Assert.Single(layouts, layout => StringField(layout, 9) == "master");
        var footer = Assert.Single(MessagesForField(master, 11), element => StringField(element, 10) == "Footer Text");
        var paragraph = Assert.Single(MessagesForField(footer, 6));
        var run = Assert.Single(MessagesForField(paragraph, 1));

        Assert.Equal("© 2022 Thoughtworks | Confidential", StringField(run, 1));
    }

    [Fact]
    public void Read_PicturePresetGeometry_EmitsShapeGeometryForImageMasking()
    {
        var protoBytes = PptxPresentationProtoReader.Read(PictureGeometryPptx());
        var slide = Assert.Single(MessagesForField(protoBytes, 1));
        var picture = Assert.Single(MessagesForField(slide, 3), element => StringField(element, 10) == "Masked Picture");
        var shape = Assert.Single(MessagesForField(picture, 4));
        var fill = Assert.Single(MessagesForField(picture, 19));

        Assert.Equal(7, Int32Field(picture, 11)); // image reference
        Assert.Equal(35, Int32Field(shape, 1)); // ellipse geometry
        Assert.Equal(4, Int32Field(fill, 1)); // picture fill
        Assert.Equal(45_000, Int32Field(fill, 12)); // alphaModFix
    }

    [Fact]
    public void Read_ArrowPresetGeometry_EmitsSpecificArrowShapeCodes()
    {
        var protoBytes = PptxPresentationProtoReader.Read(ArrowPresetGeometryPptx());
        var slide = Assert.Single(MessagesForField(protoBytes, 1));
        var slideElements = MessagesForField(slide, 3);

        AssertShapeGeometry(slideElements, "Curved Right", 202);
        AssertShapeGeometry(slideElements, "Curved Left", 201);
        AssertShapeGeometry(slideElements, "Curved Up", 203);
        AssertShapeGeometry(slideElements, "Curved Down", 204);
        AssertShapeGeometry(slideElements, "U Turn", 205);
        AssertShapeGeometry(slideElements, "Left Right Callout", 60);
        AssertShapeGeometry(slideElements, "Quad Callout", 62);
    }

    [Fact]
    public void Read_CommonPresetGeometry_EmitsSpecificShapeCodes()
    {
        var protoBytes = PptxPresentationProtoReader.Read(CommonPresetGeometryPptx());
        var slide = Assert.Single(MessagesForField(protoBytes, 1));
        var slideElements = MessagesForField(slide, 3);

        AssertShapeGeometry(slideElements, "Chord", 206);
        AssertShapeGeometry(slideElements, "Snip One Rect", 207);
        AssertShapeGeometry(slideElements, "Teardrop", 208);
        AssertShapeGeometry(slideElements, "Cloud", 209);
        AssertShapeGeometry(slideElements, "Corner", 210);
        AssertShapeGeometry(slideElements, "Octagon", 211);
        AssertShapeGeometry(slideElements, "Round One Rect", 212);
        AssertShapeGeometry(slideElements, "Plus", 176);
        AssertShapeGeometry(slideElements, "Half Frame", 213);
        AssertShapeGeometry(slideElements, "Math Equal", 177);
        AssertShapeGeometry(slideElements, "Folded Corner", 214);
        AssertShapeGeometry(slideElements, "Star Four", 215);
    }

    private static byte[] CustomGeometryEffectsAndChartPptx()
    {
        using var ms = new MemoryStream();
        using (var document = PresentationDocument.Create(ms, PresentationDocumentType.Presentation))
        {
            var presentationPart = document.AddPresentationPart();
            presentationPart.Presentation = new P.Presentation(
                new P.SlideSize { Cx = 12_192_000, Cy = 6_858_000 },
                new P.SlideIdList());

            var slidePart = presentationPart.AddNewPart<SlidePart>("rIdSlide1");
            slidePart.Slide = new P.Slide();
            slidePart.Slide.InnerXml = SlideXml();

            var chartPart = slidePart.AddNewPart<ChartPart>("rIdChart1");
            using (var chartStream = new MemoryStream(Encoding.UTF8.GetBytes(ChartXml())))
            {
                chartPart.FeedData(chartStream);
            }

            presentationPart.Presentation.SlideIdList!.Append(new P.SlideId
            {
                Id = 256U,
                RelationshipId = "rIdSlide1"
            });
            presentationPart.Presentation.Save();
            slidePart.Slide.Save();
            document.Save();
        }

        return ms.ToArray();
    }

    private static byte[] ArrowPresetGeometryPptx()
    {
        using var ms = new MemoryStream();
        using (var document = PresentationDocument.Create(ms, PresentationDocumentType.Presentation))
        {
            var presentationPart = document.AddPresentationPart();
            presentationPart.Presentation = new P.Presentation(
                new P.SlideSize { Cx = 12_192_000, Cy = 6_858_000 },
                new P.SlideIdList());

            var slidePart = presentationPart.AddNewPart<SlidePart>("rIdSlide1");
            slidePart.Slide = new P.Slide();
            slidePart.Slide.InnerXml = ArrowPresetSlideXml();

            presentationPart.Presentation.SlideIdList!.Append(new P.SlideId
            {
                Id = 256U,
                RelationshipId = "rIdSlide1"
            });
            presentationPart.Presentation.Save();
            slidePart.Slide.Save();
            document.Save();
        }

        return ms.ToArray();
    }

    private static byte[] CommonPresetGeometryPptx()
    {
        using var ms = new MemoryStream();
        using (var document = PresentationDocument.Create(ms, PresentationDocumentType.Presentation))
        {
            var presentationPart = document.AddPresentationPart();
            presentationPart.Presentation = new P.Presentation(
                new P.SlideSize { Cx = 12_192_000, Cy = 6_858_000 },
                new P.SlideIdList());

            var slidePart = presentationPart.AddNewPart<SlidePart>("rIdSlide1");
            slidePart.Slide = new P.Slide();
            slidePart.Slide.InnerXml = CommonPresetSlideXml();

            presentationPart.Presentation.SlideIdList!.Append(new P.SlideId
            {
                Id = 256U,
                RelationshipId = "rIdSlide1"
            });
            presentationPart.Presentation.Save();
            slidePart.Slide.Save();
            document.Save();
        }

        return ms.ToArray();
    }

    private static byte[] MasterFooterTextPptx()
    {
        using var ms = new MemoryStream();
        using (var document = PresentationDocument.Create(ms, PresentationDocumentType.Presentation))
        {
            var presentationPart = document.AddPresentationPart();
            presentationPart.Presentation = new P.Presentation(
                new P.SlideSize { Cx = 9_144_000, Cy = 5_143_500 },
                new P.SlideMasterIdList(),
                new P.SlideIdList());

            var masterPart = presentationPart.AddNewPart<SlideMasterPart>("rIdMaster1");
            masterPart.SlideMaster = new P.SlideMaster();
            masterPart.SlideMaster.InnerXml = MasterXml();

            var layoutPart = masterPart.AddNewPart<SlideLayoutPart>("rIdLayout1");
            layoutPart.SlideLayout = new P.SlideLayout();
            layoutPart.SlideLayout.InnerXml = LayoutXml();

            var slidePart = presentationPart.AddNewPart<SlidePart>("rIdSlide1");
            slidePart.Slide = new P.Slide();
            slidePart.Slide.InnerXml = EmptySlideXml();
            slidePart.AddPart(layoutPart, "rIdLayout1");

            presentationPart.Presentation.SlideMasterIdList!.Append(new P.SlideMasterId
            {
                Id = 2_147_483_648U,
                RelationshipId = "rIdMaster1"
            });
            presentationPart.Presentation.SlideIdList!.Append(new P.SlideId
            {
                Id = 256U,
                RelationshipId = "rIdSlide1"
            });
            presentationPart.Presentation.Save();
            masterPart.SlideMaster.Save();
            layoutPart.SlideLayout.Save();
            slidePart.Slide.Save();
            document.Save();
        }

        return ms.ToArray();
    }

    private static byte[] PictureGeometryPptx()
    {
        using var ms = new MemoryStream();
        using (var document = PresentationDocument.Create(ms, PresentationDocumentType.Presentation))
        {
            var presentationPart = document.AddPresentationPart();
            presentationPart.Presentation = new P.Presentation(
                new P.SlideSize { Cx = 9_144_000, Cy = 5_143_500 },
                new P.SlideIdList());

            var slidePart = presentationPart.AddNewPart<SlidePart>("rIdSlide1");
            slidePart.Slide = new P.Slide();
            slidePart.Slide.InnerXml = PictureGeometrySlideXml();

            var imagePart = slidePart.AddImagePart(ImagePartType.Png, "rIdImage1");
            using (var imageStream = new MemoryStream(Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAFgwJ/l8WU7wAAAABJRU5ErkJggg==")))
            {
                imagePart.FeedData(imageStream);
            }

            presentationPart.Presentation.SlideIdList!.Append(new P.SlideId
            {
                Id = 256U,
                RelationshipId = "rIdSlide1"
            });
            presentationPart.Presentation.Save();
            slidePart.Slide.Save();
            document.Save();
        }

        return ms.ToArray();
    }

    private static string MasterXml()
    {
        return """
<p:cSld xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main">
  <p:spTree>
    <p:nvGrpSpPr><p:cNvPr id="1" name=""/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr>
    <p:grpSpPr/>
    <p:sp>
      <p:nvSpPr><p:cNvPr id="2" name="Footer Text"/><p:cNvSpPr txBox="1"/><p:nvPr/></p:nvSpPr>
      <p:spPr>
        <a:xfrm><a:off x="368524" y="4759718"/><a:ext cx="3937800" cy="176100"/></a:xfrm>
        <a:prstGeom prst="rect"><a:avLst/></a:prstGeom><a:noFill/><a:ln><a:noFill/></a:ln>
      </p:spPr>
      <p:txBody><a:bodyPr/><a:lstStyle/><a:p><a:r><a:t>© 2022 Thoughtworks | Confidential</a:t></a:r></a:p></p:txBody>
    </p:sp>
  </p:spTree>
</p:cSld>
<p:clrMap bg1="lt1" tx1="dk1" bg2="lt2" tx2="dk2" accent1="accent1" accent2="accent2" accent3="accent3" accent4="accent4" accent5="accent5" accent6="accent6" hlink="hlink" folHlink="folHlink"/>
<p:sldLayoutIdLst xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><p:sldLayoutId id="2147483649" r:id="rIdLayout1"/></p:sldLayoutIdLst>
""";
    }

    private static string LayoutXml()
    {
        return """
<p:cSld xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" name="Blank">
  <p:spTree>
    <p:nvGrpSpPr><p:cNvPr id="1" name=""/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr>
    <p:grpSpPr/>
  </p:spTree>
</p:cSld>
<p:clrMapOvr xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main"><a:masterClrMapping/></p:clrMapOvr>
""";
    }

    private static string EmptySlideXml()
    {
        return """
<p:cSld xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main">
  <p:spTree>
    <p:nvGrpSpPr><p:cNvPr id="1" name=""/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr>
    <p:grpSpPr/>
  </p:spTree>
</p:cSld>
<p:clrMapOvr xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main"><a:masterClrMapping/></p:clrMapOvr>
""";
    }

    private static string PictureGeometrySlideXml()
    {
        return """
<p:cSld xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
  <p:spTree>
    <p:nvGrpSpPr><p:cNvPr id="1" name=""/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr>
    <p:grpSpPr/>
    <p:pic>
      <p:nvPicPr><p:cNvPr id="2" name="Masked Picture"/><p:cNvPicPr preferRelativeResize="0"/><p:nvPr/></p:nvPicPr>
      <p:blipFill rotWithShape="1"><a:blip r:embed="rIdImage1"><a:alphaModFix amt="45000"/></a:blip><a:stretch/></p:blipFill>
      <p:spPr>
        <a:xfrm><a:off x="914400" y="914400"/><a:ext cx="914400" cy="914400"/></a:xfrm>
        <a:prstGeom prst="ellipse"><a:avLst/></a:prstGeom>
        <a:noFill/>
        <a:ln w="9525"><a:solidFill><a:srgbClr val="003D4F"/></a:solidFill></a:ln>
      </p:spPr>
    </p:pic>
  </p:spTree>
</p:cSld>
<p:clrMapOvr xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main"><a:masterClrMapping/></p:clrMapOvr>
""";
    }

    private static string SlideXml()
    {
        return """
<p:cSld xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:c="http://schemas.openxmlformats.org/drawingml/2006/chart" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
  <p:spTree>
    <p:nvGrpSpPr><p:cNvPr id="1" name=""/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr>
    <p:grpSpPr/>
    <p:sp>
      <p:nvSpPr><p:cNvPr id="2" name="Custom Geometry"/><p:cNvSpPr/><p:nvPr/></p:nvSpPr>
      <p:spPr>
        <a:xfrm><a:off x="914400" y="914400"/><a:ext cx="2743200" cy="1371600"/></a:xfrm>
        <a:custGeom>
          <a:rect l="l" t="t" r="r" b="b"/>
          <a:pathLst>
            <a:path w="1000" h="1000" id="customPath">
              <a:moveTo><a:pt x="0" y="0"/></a:moveTo>
              <a:lnTo><a:pt x="1000" y="0"/></a:lnTo>
              <a:quadBezTo><a:pt x="1000" y="500"/><a:pt x="500" y="750"/></a:quadBezTo>
              <a:cubicBezTo><a:pt x="250" y="800"/><a:pt x="100" y="900"/><a:pt x="0" y="1000"/></a:cubicBezTo>
              <a:arcTo wR="500" hR="250" stAng="0" swAng="5400000"/>
              <a:close/>
            </a:path>
          </a:pathLst>
        </a:custGeom>
        <a:solidFill><a:srgbClr val="4472C4"/></a:solidFill>
        <a:ln w="9525"><a:solidFill><a:srgbClr val="003D4F"/></a:solidFill><a:headEnd type="triangle" w="med" len="med"/><a:tailEnd type="stealth" w="med" len="med"/></a:ln>
        <a:effectLst>
          <a:outerShdw blurRad="19050" dist="38100" dir="5400000" rotWithShape="0"><a:srgbClr val="000000"><a:alpha val="50000"/></a:srgbClr></a:outerShdw>
          <a:glow rad="25400"><a:srgbClr val="FFCC00"/></a:glow>
          <a:reflection blurRad="6350" stA="50000" stPos="0" endA="0" endPos="100000" dist="12700" dir="5400000" fadeDir="5400000" sx="100000" sy="100000" kx="0" ky="0" algn="b" rotWithShape="1"/>
          <a:softEdge rad="12700"/>
        </a:effectLst>
      </p:spPr>
      <p:txBody><a:bodyPr/><a:lstStyle/><a:p><a:r><a:t>Custom</a:t></a:r></a:p></p:txBody>
    </p:sp>
    <p:graphicFrame>
      <p:nvGraphicFramePr><p:cNvPr id="3" name="Chart 1"/><p:cNvGraphicFramePr/><p:nvPr/></p:nvGraphicFramePr>
      <p:xfrm><a:off x="3657600" y="914400"/><a:ext cx="4572000" cy="2743200"/></p:xfrm>
      <a:graphic><a:graphicData uri="http://schemas.openxmlformats.org/drawingml/2006/chart"><c:chart r:id="rIdChart1"/></a:graphicData></a:graphic>
    </p:graphicFrame>
    <p:sp>
      <p:nvSpPr><p:cNvPr id="4" name="Radial Gradient"/><p:cNvSpPr/><p:nvPr/></p:nvSpPr>
      <p:spPr>
        <a:xfrm><a:off x="914400" y="2743200"/><a:ext cx="1219200" cy="609600"/></a:xfrm>
        <a:prstGeom prst="rect"><a:avLst/></a:prstGeom>
        <a:gradFill>
          <a:gsLst>
            <a:gs pos="0"><a:srgbClr val="FFFFFF"/></a:gs>
            <a:gs pos="100000"><a:srgbClr val="808080"/></a:gs>
          </a:gsLst>
          <a:path path="circle"><a:fillToRect l="50000" t="50000" r="50000" b="50000"/></a:path>
        </a:gradFill>
      </p:spPr>
    </p:sp>
  </p:spTree>
</p:cSld>
<p:clrMapOvr xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main"><a:masterClrMapping/></p:clrMapOvr>
""";
    }

    private static string ArrowPresetSlideXml()
    {
        return """
<p:cSld xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main">
  <p:spTree>
    <p:nvGrpSpPr><p:cNvPr id="1" name=""/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr>
    <p:grpSpPr/>
    <p:sp>
      <p:nvSpPr><p:cNvPr id="2" name="Curved Right"/><p:cNvSpPr/><p:nvPr/></p:nvSpPr>
      <p:spPr><a:xfrm><a:off x="100000" y="100000"/><a:ext cx="800000" cy="500000"/></a:xfrm><a:prstGeom prst="curvedRightArrow"><a:avLst/></a:prstGeom><a:solidFill><a:srgbClr val="FF0000"/></a:solidFill></p:spPr>
    </p:sp>
    <p:sp>
      <p:nvSpPr><p:cNvPr id="3" name="Curved Left"/><p:cNvSpPr/><p:nvPr/></p:nvSpPr>
      <p:spPr><a:xfrm><a:off x="1000000" y="100000"/><a:ext cx="800000" cy="500000"/></a:xfrm><a:prstGeom prst="curvedLeftArrow"><a:avLst/></a:prstGeom><a:solidFill><a:srgbClr val="00FF00"/></a:solidFill></p:spPr>
    </p:sp>
    <p:sp>
      <p:nvSpPr><p:cNvPr id="4" name="Curved Up"/><p:cNvSpPr/><p:nvPr/></p:nvSpPr>
      <p:spPr><a:xfrm><a:off x="1900000" y="100000"/><a:ext cx="800000" cy="500000"/></a:xfrm><a:prstGeom prst="curvedUpArrow"><a:avLst/></a:prstGeom><a:solidFill><a:srgbClr val="0000FF"/></a:solidFill></p:spPr>
    </p:sp>
    <p:sp>
      <p:nvSpPr><p:cNvPr id="5" name="Curved Down"/><p:cNvSpPr/><p:nvPr/></p:nvSpPr>
      <p:spPr><a:xfrm><a:off x="2800000" y="100000"/><a:ext cx="800000" cy="500000"/></a:xfrm><a:prstGeom prst="curvedDownArrow"><a:avLst/></a:prstGeom><a:solidFill><a:srgbClr val="FFFF00"/></a:solidFill></p:spPr>
    </p:sp>
    <p:sp>
      <p:nvSpPr><p:cNvPr id="6" name="U Turn"/><p:cNvSpPr/><p:nvPr/></p:nvSpPr>
      <p:spPr><a:xfrm><a:off x="3700000" y="100000"/><a:ext cx="800000" cy="500000"/></a:xfrm><a:prstGeom prst="uturnArrow"><a:avLst/></a:prstGeom><a:solidFill><a:srgbClr val="00FFFF"/></a:solidFill></p:spPr>
    </p:sp>
    <p:sp>
      <p:nvSpPr><p:cNvPr id="7" name="Left Right Callout"/><p:cNvSpPr/><p:nvPr/></p:nvSpPr>
      <p:spPr><a:xfrm><a:off x="4600000" y="100000"/><a:ext cx="800000" cy="500000"/></a:xfrm><a:prstGeom prst="leftRightArrowCallout"><a:avLst/></a:prstGeom><a:solidFill><a:srgbClr val="FF00FF"/></a:solidFill></p:spPr>
    </p:sp>
    <p:sp>
      <p:nvSpPr><p:cNvPr id="8" name="Quad Callout"/><p:cNvSpPr/><p:nvPr/></p:nvSpPr>
      <p:spPr><a:xfrm><a:off x="5500000" y="100000"/><a:ext cx="800000" cy="500000"/></a:xfrm><a:prstGeom prst="quadArrowCallout"><a:avLst/></a:prstGeom><a:solidFill><a:srgbClr val="808080"/></a:solidFill></p:spPr>
    </p:sp>
  </p:spTree>
</p:cSld>
<p:clrMapOvr xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main"><a:masterClrMapping/></p:clrMapOvr>
""";
    }

    private static string CommonPresetSlideXml()
    {
        return """
<p:cSld xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main">
  <p:spTree>
    <p:nvGrpSpPr><p:cNvPr id="1" name=""/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr>
    <p:grpSpPr/>
    <p:sp>
      <p:nvSpPr><p:cNvPr id="2" name="Chord"/><p:cNvSpPr/><p:nvPr/></p:nvSpPr>
      <p:spPr><a:xfrm><a:off x="100000" y="100000"/><a:ext cx="600000" cy="400000"/></a:xfrm><a:prstGeom prst="chord"><a:avLst/></a:prstGeom><a:solidFill><a:srgbClr val="FF0000"/></a:solidFill></p:spPr>
    </p:sp>
    <p:sp>
      <p:nvSpPr><p:cNvPr id="3" name="Snip One Rect"/><p:cNvSpPr/><p:nvPr/></p:nvSpPr>
      <p:spPr><a:xfrm><a:off x="800000" y="100000"/><a:ext cx="600000" cy="400000"/></a:xfrm><a:prstGeom prst="snip1Rect"><a:avLst/></a:prstGeom><a:solidFill><a:srgbClr val="00FF00"/></a:solidFill></p:spPr>
    </p:sp>
    <p:sp>
      <p:nvSpPr><p:cNvPr id="4" name="Teardrop"/><p:cNvSpPr/><p:nvPr/></p:nvSpPr>
      <p:spPr><a:xfrm><a:off x="1500000" y="100000"/><a:ext cx="600000" cy="400000"/></a:xfrm><a:prstGeom prst="teardrop"><a:avLst/></a:prstGeom><a:solidFill><a:srgbClr val="0000FF"/></a:solidFill></p:spPr>
    </p:sp>
    <p:sp>
      <p:nvSpPr><p:cNvPr id="5" name="Cloud"/><p:cNvSpPr/><p:nvPr/></p:nvSpPr>
      <p:spPr><a:xfrm><a:off x="2200000" y="100000"/><a:ext cx="600000" cy="400000"/></a:xfrm><a:prstGeom prst="cloud"><a:avLst/></a:prstGeom><a:solidFill><a:srgbClr val="FFFF00"/></a:solidFill></p:spPr>
    </p:sp>
    <p:sp>
      <p:nvSpPr><p:cNvPr id="6" name="Corner"/><p:cNvSpPr/><p:nvPr/></p:nvSpPr>
      <p:spPr><a:xfrm><a:off x="2900000" y="100000"/><a:ext cx="600000" cy="400000"/></a:xfrm><a:prstGeom prst="corner"><a:avLst/></a:prstGeom><a:solidFill><a:srgbClr val="00FFFF"/></a:solidFill></p:spPr>
    </p:sp>
    <p:sp>
      <p:nvSpPr><p:cNvPr id="7" name="Octagon"/><p:cNvSpPr/><p:nvPr/></p:nvSpPr>
      <p:spPr><a:xfrm><a:off x="3600000" y="100000"/><a:ext cx="600000" cy="400000"/></a:xfrm><a:prstGeom prst="octagon"><a:avLst/></a:prstGeom><a:solidFill><a:srgbClr val="FF00FF"/></a:solidFill></p:spPr>
    </p:sp>
    <p:sp>
      <p:nvSpPr><p:cNvPr id="8" name="Round One Rect"/><p:cNvSpPr/><p:nvPr/></p:nvSpPr>
      <p:spPr><a:xfrm><a:off x="4300000" y="100000"/><a:ext cx="600000" cy="400000"/></a:xfrm><a:prstGeom prst="round1Rect"><a:avLst/></a:prstGeom><a:solidFill><a:srgbClr val="808080"/></a:solidFill></p:spPr>
    </p:sp>
    <p:sp>
      <p:nvSpPr><p:cNvPr id="9" name="Plus"/><p:cNvSpPr/><p:nvPr/></p:nvSpPr>
      <p:spPr><a:xfrm><a:off x="5000000" y="100000"/><a:ext cx="600000" cy="400000"/></a:xfrm><a:prstGeom prst="plus"><a:avLst/></a:prstGeom><a:solidFill><a:srgbClr val="AA0000"/></a:solidFill></p:spPr>
    </p:sp>
    <p:sp>
      <p:nvSpPr><p:cNvPr id="10" name="Half Frame"/><p:cNvSpPr/><p:nvPr/></p:nvSpPr>
      <p:spPr><a:xfrm><a:off x="5700000" y="100000"/><a:ext cx="600000" cy="400000"/></a:xfrm><a:prstGeom prst="halfFrame"><a:avLst/></a:prstGeom><a:solidFill><a:srgbClr val="00AA00"/></a:solidFill></p:spPr>
    </p:sp>
    <p:sp>
      <p:nvSpPr><p:cNvPr id="11" name="Math Equal"/><p:cNvSpPr/><p:nvPr/></p:nvSpPr>
      <p:spPr><a:xfrm><a:off x="6400000" y="100000"/><a:ext cx="600000" cy="400000"/></a:xfrm><a:prstGeom prst="mathEqual"><a:avLst/></a:prstGeom><a:solidFill><a:srgbClr val="0000AA"/></a:solidFill></p:spPr>
    </p:sp>
    <p:sp>
      <p:nvSpPr><p:cNvPr id="12" name="Folded Corner"/><p:cNvSpPr/><p:nvPr/></p:nvSpPr>
      <p:spPr><a:xfrm><a:off x="7100000" y="100000"/><a:ext cx="600000" cy="400000"/></a:xfrm><a:prstGeom prst="foldedCorner"><a:avLst/></a:prstGeom><a:solidFill><a:srgbClr val="AAAA00"/></a:solidFill></p:spPr>
    </p:sp>
    <p:sp>
      <p:nvSpPr><p:cNvPr id="13" name="Star Four"/><p:cNvSpPr/><p:nvPr/></p:nvSpPr>
      <p:spPr><a:xfrm><a:off x="7800000" y="100000"/><a:ext cx="600000" cy="400000"/></a:xfrm><a:prstGeom prst="star4"><a:avLst/></a:prstGeom><a:solidFill><a:srgbClr val="00AAAA"/></a:solidFill></p:spPr>
    </p:sp>
  </p:spTree>
</p:cSld>
<p:clrMapOvr xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main"><a:masterClrMapping/></p:clrMapOvr>
""";
    }

    private static string ChartXml()
    {
        return """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<c:chartSpace xmlns:c="http://schemas.openxmlformats.org/drawingml/2006/chart" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
  <c:chart>
    <c:title><c:tx><c:rich><a:bodyPr/><a:lstStyle/><a:p><a:r><a:t>Quarterly Revenue</a:t></a:r></a:p></c:rich></c:tx></c:title>
    <c:plotArea>
      <c:barChart>
        <c:barDir val="bar"/>
        <c:ser>
          <c:idx val="0"/>
          <c:order val="0"/>
          <c:tx><c:v>North</c:v></c:tx>
          <c:spPr><a:solidFill><a:srgbClr val="70AD47"/></a:solidFill></c:spPr>
          <c:cat><c:strRef><c:f>Sheet1!$A$1:$A$2</c:f><c:strCache><c:ptCount val="2"/><c:pt idx="0"><c:v>Q1</c:v></c:pt><c:pt idx="1"><c:v>Q2</c:v></c:pt></c:strCache></c:strRef></c:cat>
          <c:val><c:numRef><c:f>Sheet1!$B$1:$B$2</c:f><c:numCache><c:formatCode>General</c:formatCode><c:ptCount val="2"/><c:pt idx="0"><c:v>12</c:v></c:pt><c:pt idx="1"><c:v>18.5</c:v></c:pt></c:numCache></c:numRef></c:val>
        </c:ser>
      </c:barChart>
    </c:plotArea>
    <c:legend><c:legendPos val="r"/></c:legend>
  </c:chart>
</c:chartSpace>
""";
    }

    private static Dictionary<int, int> CountFields(byte[] bytes)
    {
        var counts = new Dictionary<int, int>();
        var input = new CodedInputStream(bytes);
        while (input.ReadTag() is var tag && tag != 0)
        {
            var fieldNumber = WireFormat.GetTagFieldNumber(tag);
            counts[fieldNumber] = counts.GetValueOrDefault(fieldNumber) + 1;
            input.SkipLastField();
        }

        return counts;
    }

    private static List<byte[]> MessagesForField(byte[] bytes, int targetField)
    {
        var messages = new List<byte[]>();
        var input = new CodedInputStream(bytes);
        while (input.ReadTag() is var tag && tag != 0)
        {
            if (WireFormat.GetTagFieldNumber(tag) == targetField && WireFormat.GetTagWireType(tag) == WireFormat.WireType.LengthDelimited)
            {
                messages.Add(input.ReadBytes().ToByteArray());
                continue;
            }

            input.SkipLastField();
        }

        return messages;
    }

    private static List<string> StringsForField(byte[] bytes, int targetField)
    {
        var values = new List<string>();
        var input = new CodedInputStream(bytes);
        while (input.ReadTag() is var tag && tag != 0)
        {
            if (WireFormat.GetTagFieldNumber(tag) == targetField && WireFormat.GetTagWireType(tag) == WireFormat.WireType.LengthDelimited)
            {
                values.Add(input.ReadString());
                continue;
            }

            input.SkipLastField();
        }

        return values;
    }

    private static string StringField(byte[] bytes, int targetField) =>
        StringsForField(bytes, targetField).FirstOrDefault() ?? "";

    private static void AssertShapeGeometry(List<byte[]> elements, string name, int expectedGeometry)
    {
        var element = Assert.Single(elements, item => StringField(item, 10) == name);
        var shape = Assert.Single(MessagesForField(element, 4));
        Assert.Equal(expectedGeometry, Int32Field(shape, 1));
    }

    private static int Int32Field(byte[] bytes, int targetField)
    {
        var input = new CodedInputStream(bytes);
        while (input.ReadTag() is var tag && tag != 0)
        {
            if (WireFormat.GetTagFieldNumber(tag) == targetField && WireFormat.GetTagWireType(tag) == WireFormat.WireType.Varint)
            {
                return input.ReadInt32();
            }

            input.SkipLastField();
        }

        return 0;
    }
}
