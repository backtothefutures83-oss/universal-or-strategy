using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using Google.Protobuf;
using System.Globalization;
using System.Xml.Linq;
using A = DocumentFormat.OpenXml.Drawing;
using C = DocumentFormat.OpenXml.Drawing.Charts;
using P = DocumentFormat.OpenXml.Presentation;

namespace Routa.OfficeWasmReader;

internal static class PptxPresentationProtoReader
{
    private const int ElementTypeText = 1;
    private const int ElementTypeShape = 5;
    private const int ElementTypeChartReference = 6;
    private const int ElementTypeImageReference = 7;
    private const int ElementTypeTable = 9;
    private const int ShapeGeometryCustom = 188;
    private const int FillTypeSolid = 1;
    private const int FillTypeGradient = 2;
    private const int FillTypePicture = 4;
    private const int ColorTypeRgb = 1;
    private const int ColorTypeScheme = 2;
    private const int ColorTypeSystem = 3;
    private const int GradientKindLinear = 1;
    private const int GradientKindPath = 2;
    private const int EffectTypeShadow = 1;
    private const int EffectTypeGlow = 3;
    private const int EffectTypeReflection = 4;
    private const int EffectTypeSoftEdges = 5;
    private const int ConnectorLineCapFlat = 1;
    private const int ConnectorLineCapRound = 2;
    private const int ConnectorLineCapSquare = 3;
    private const int ConnectorLineJoinRound = 1;
    private const int ConnectorLineJoinBevel = 2;
    private const int ConnectorLineJoinMiter = 3;
    private const int ConnectorLineEndNone = 1;
    private const int ConnectorLineEndTriangle = 2;
    private const int ConnectorLineEndStealth = 3;
    private const int ConnectorLineEndDiamond = 4;
    private const int ConnectorLineEndOval = 5;
    private const int ConnectorLineEndArrow = 6;
    private const int ConnectorLineEndSmall = 1;
    private const int ConnectorLineEndMedium = 2;
    private const int ConnectorLineEndLarge = 3;

    public static byte[] Read(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        using var document = PresentationDocument.Open(stream, false);
        var presentationPart = document.PresentationPart;
        var slideSize = presentationPart?.Presentation.SlideSize;
        var widthEmu = ToLong(slideSize?.Cx) ?? 12_192_000L;
        var heightEmu = ToLong(slideSize?.Cy) ?? 6_858_000L;
        var slideIds = presentationPart?.Presentation.SlideIdList?.Elements<P.SlideId>() ?? Enumerable.Empty<P.SlideId>();
        var slideParts = ResolveSlideParts(presentationPart, slideIds).Take(OpenXmlReaderLimits.MaxSlides).ToList();
        var slideMasterParts = DistinctByUri(presentationPart?.SlideMasterParts ?? Enumerable.Empty<SlideMasterPart>()).ToList();
        var slideLayoutParts = DistinctByUri(slideMasterParts.SelectMany(part => part.SlideLayoutParts)).ToList();
        var rawTransformsByPart = RawTransformIndex.FromPackage(bytes);
        var themePart = slideParts
            .Select(part => part.SlideLayoutPart?.SlideMasterPart?.ThemePart)
            .FirstOrDefault(part => part is not null)
            ?? slideMasterParts
                .OrderBy(part => part.Uri.OriginalString, StringComparer.Ordinal)
                .Select(part => part.ThemePart)
                .FirstOrDefault(part => part is not null);
        var tableStylesPart = presentationPart?.TableStylesPart;
        var presentationParts = slideParts.Cast<OpenXmlPart>()
            .Concat(slideLayoutParts)
            .Concat(slideMasterParts)
            .ToList();
        var imageParts = DistinctByUri(presentationParts.SelectMany(ImagePartsFrom))
            .OrderBy(part => part.Uri.OriginalString, StringComparer.Ordinal)
            .ToList();
        var chartParts = DistinctByUri(presentationParts.SelectMany(ChartPartsFrom))
            .OrderBy(part => part.Uri.OriginalString, StringComparer.Ordinal)
            .ToList();

        return Message(output =>
        {
            var slideIndex = 1;
            foreach (var slidePart in slideParts)
            {
                WriteMessage(output, 1, WriteSlide(slidePart, slideIndex, widthEmu, heightEmu, rawTransformsByPart));
                slideIndex++;
            }

            var theme = WriteTheme(themePart);
            if (theme is not null)
            {
                WriteMessage(output, 2, theme);
            }

            foreach (var slideMasterPart in slideMasterParts)
            {
                WriteMessage(output, 3, WriteSlideMasterLayout(slideMasterPart, rawTransformsByPart));
            }

            foreach (var slideLayoutPart in slideLayoutParts)
            {
                WriteMessage(output, 3, WriteSlideLayout(slideLayoutPart, rawTransformsByPart));
            }

            foreach (var imagePart in imageParts)
            {
                WriteMessage(output, 4, WriteRootImage(imagePart));
            }

            var tableStyles = WriteTableStyles(tableStylesPart);
            if (tableStyles is not null)
            {
                WriteMessage(output, 7, tableStyles);
            }

            foreach (var chartPart in chartParts)
            {
                WriteMessage(output, 9, WriteChart(chartPart));
            }
        });
    }

    private static byte[] WriteSlide(
        SlidePart slidePart,
        int slideIndex,
        long widthEmu,
        long heightEmu,
        IReadOnlyDictionary<string, RawTransformIndex> rawTransformsByPart)
    {
        return Message(output =>
        {
            WriteInt32(output, 1, slideIndex);
            var layoutId = slidePart.SlideLayoutPart?.Uri.OriginalString;
            WriteString(output, 2, layoutId);

            foreach (var element in ExtractElements(slidePart.Slide.CommonSlideData?.ShapeTree, slidePart, rawTransformsByPart))
            {
                WriteMessage(output, 3, element);
            }

            WriteInt64(output, 5, widthEmu);
            WriteInt64(output, 6, heightEmu);
            var background = WriteBackground(slidePart.Slide.CommonSlideData?.Background);
            if (background is not null)
            {
                WriteMessage(output, 10, background);
            }

            WriteString(output, 11, slidePart.Uri.OriginalString);
            if (slidePart.NotesSlidePart is { } notesSlidePart)
            {
                WriteMessage(output, 12, WriteNotesSlide(slidePart, notesSlidePart));
            }
        });
    }

    private static byte[] WriteNotesSlide(SlidePart slidePart, NotesSlidePart notesSlidePart)
    {
        return Message(output =>
        {
            WriteString(output, 2, slidePart.SlideLayoutPart?.Uri.OriginalString);
            foreach (var element in ExtractNotesElements(notesSlidePart.NotesSlide.CommonSlideData?.ShapeTree))
            {
                WriteMessage(output, 3, element);
            }

            WriteString(output, 11, slidePart.Uri.OriginalString);
        });
    }

    private static byte[]? WriteTheme(ThemePart? themePart)
    {
        var themeElements = themePart?.Theme?.ThemeElements;
        if (themeElements is null)
        {
            return null;
        }

        return Message(output =>
        {
            var colorScheme = themeElements.ColorScheme;
            if (colorScheme is not null)
            {
                WriteMessage(output, 1, WriteColorScheme(colorScheme));
            }

            var formatScheme = themeElements.FormatScheme;
            foreach (var fill in formatScheme?.BackgroundFillStyleList?.ChildElements ?? Enumerable.Empty<OpenXmlElement>())
            {
                var fillProto = WriteFillFromElement(fill);
                if (fillProto is not null)
                {
                    WriteMessage(output, 2, fillProto);
                }
            }

            foreach (var line in formatScheme?.LineStyleList?.Elements<A.Outline>() ?? Enumerable.Empty<A.Outline>())
            {
                WriteMessage(output, 3, WriteLine(line));
            }

            foreach (var effectStyle in formatScheme?.EffectStyleList?.Elements<A.EffectStyle>() ?? Enumerable.Empty<A.EffectStyle>())
            {
                WriteMessageAlways(output, 4, WriteEffectStyle(effectStyle));
            }
        });
    }

    private static byte[] WriteSlideMasterLayout(
        SlideMasterPart slideMasterPart,
        IReadOnlyDictionary<string, RawTransformIndex> rawTransformsByPart)
    {
        return Message(output =>
        {
            WriteString(output, 1, slideMasterPart.Uri.OriginalString);
            WriteString(output, 9, "master");
            var background = WriteBackground(slideMasterPart.SlideMaster.CommonSlideData?.Background);
            if (background is not null)
            {
                WriteMessage(output, 10, background);
            }

            foreach (var element in ExtractElements(slideMasterPart.SlideMaster.CommonSlideData?.ShapeTree, slideMasterPart, rawTransformsByPart, true))
            {
                WriteMessage(output, 11, element);
            }

            var colorMap = slideMasterPart.SlideMaster.ColorMap;
            if (colorMap is not null)
            {
                WriteMessage(output, 16, WriteColorMap(colorMap));
            }

            var textStyles = slideMasterPart.SlideMaster.TextStyles;
            foreach (var levelStyle in ExtractLevelStyles(textStyles?.BodyStyle, writeDefaults: true))
            {
                WriteMessage(output, 12, levelStyle);
            }

            foreach (var levelStyle in ExtractLevelStyles(textStyles?.TitleStyle, writeDefaults: true))
            {
                WriteMessage(output, 13, levelStyle);
            }

            foreach (var levelStyle in ExtractLevelStyles(textStyles?.OtherStyle, writeDefaults: true))
            {
                WriteMessage(output, 14, levelStyle);
            }
        });
    }

    private static byte[] WriteSlideLayout(
        SlideLayoutPart slideLayoutPart,
        IReadOnlyDictionary<string, RawTransformIndex> rawTransformsByPart)
    {
        return Message(output =>
        {
            WriteString(output, 1, slideLayoutPart.Uri.OriginalString);
            WriteString(output, 8, slideLayoutPart.SlideLayout.CommonSlideData?.Name?.Value);
            var background = WriteBackground(slideLayoutPart.SlideLayout.CommonSlideData?.Background);
            if (background is not null)
            {
                WriteMessage(output, 10, background);
            }

            foreach (var element in ExtractElements(slideLayoutPart.SlideLayout.CommonSlideData?.ShapeTree, slideLayoutPart, rawTransformsByPart, true))
            {
                WriteMessage(output, 11, element);
            }

            WriteString(output, 15, slideLayoutPart.SlideMasterPart?.Uri.OriginalString);
        });
    }

    private static IEnumerable<SlidePart> ResolveSlideParts(PresentationPart? presentationPart, IEnumerable<P.SlideId> slideIds)
    {
        if (presentationPart is null)
        {
            yield break;
        }

        foreach (var slideId in slideIds)
        {
            var relationshipId = slideId.RelationshipId?.Value;
            if (string.IsNullOrEmpty(relationshipId))
            {
                continue;
            }

            if (presentationPart.GetPartById(relationshipId) is SlidePart slidePart)
            {
                yield return slidePart;
            }
        }
    }

    private static IEnumerable<byte[]> ExtractElements(
        P.ShapeTree? shapeTree,
        OpenXmlPartContainer partContainer,
        IReadOnlyDictionary<string, RawTransformIndex>? rawTransformsByPart = null,
        bool layoutLike = false)
    {
        if (shapeTree is null)
        {
            yield break;
        }

        var rawTransforms = RawTransformsForPart(partContainer, rawTransformsByPart);
        foreach (var element in ExtractElements(shapeTree.ChildElements, partContainer, layoutLike, rawTransforms: rawTransforms))
        {
            yield return element;
        }
    }

    private static IEnumerable<byte[]> ExtractNotesElements(P.ShapeTree? shapeTree)
    {
        if (shapeTree is null)
        {
            yield break;
        }

        var shapeIndex = 0;
        foreach (var shape in shapeTree.Elements<P.Shape>())
        {
            var element = WriteNotesShapeElement(shape, shapeIndex);
            if (element is not null)
            {
                yield return element;
            }

            shapeIndex++;
        }
    }

    private static RawTransformIndex? RawTransformsForPart(
        OpenXmlPartContainer partContainer,
        IReadOnlyDictionary<string, RawTransformIndex>? rawTransformsByPart)
    {
        return partContainer is OpenXmlPart part &&
            rawTransformsByPart?.TryGetValue(part.Uri.OriginalString, out var rawTransforms) == true
            ? rawTransforms
            : null;
    }

    private static IEnumerable<byte[]> ExtractElements(
        IEnumerable<OpenXmlElement>? childElements,
        OpenXmlPartContainer partContainer,
        bool layoutLike = false,
        GroupTransformContext? groupTransform = null,
        RawTransformIndex? rawTransforms = null)
    {
        if (childElements is null)
        {
            yield break;
        }

        var zIndex = 0;
        foreach (var child in childElements)
        {
            foreach (var element in ExtractElement(child, partContainer, zIndex, layoutLike, groupTransform, rawTransforms))
            {
                yield return element;
            }

            zIndex++;
        }
    }

    private static IEnumerable<byte[]> ExtractElement(
        OpenXmlElement child,
        OpenXmlPartContainer partContainer,
        int zIndex,
        bool layoutLike,
        GroupTransformContext? groupTransform,
        RawTransformIndex? rawTransforms)
    {
        switch (child)
        {
            case P.Shape shape:
                if (WriteShapeElement(partContainer, shape, zIndex, layoutLike, groupTransform, rawTransforms) is { } shapeElement)
                {
                    yield return shapeElement;
                }
                break;
            case P.Picture picture:
                if (WritePictureElement(partContainer, picture, zIndex, groupTransform) is { } pictureElement)
                {
                    yield return pictureElement;
                }
                break;
            case P.GraphicFrame graphicFrame:
                if (WriteGraphicFrameElement(partContainer, graphicFrame, zIndex, groupTransform) is { } frameElement)
                {
                    yield return frameElement;
                }
                break;
            case P.ConnectionShape connectionShape:
                if (WriteConnectionShapeElement(partContainer, connectionShape, zIndex, groupTransform, rawTransforms) is { } connectorElement)
                {
                    yield return connectorElement;
                }
                break;
            case P.GroupShape groupShape:
                foreach (var groupElement in ExtractGroupShapeElements(partContainer, groupShape, layoutLike, groupTransform, rawTransforms))
                {
                    yield return groupElement;
                }
                break;
        }
    }

    private static IEnumerable<ImagePart> ImagePartsFrom(OpenXmlPart part)
    {
        return part switch
        {
            SlidePart slidePart => slidePart.ImageParts,
            SlideLayoutPart slideLayoutPart => slideLayoutPart.ImageParts,
            SlideMasterPart slideMasterPart => slideMasterPart.ImageParts,
            _ => Enumerable.Empty<ImagePart>(),
        };
    }

    private static IEnumerable<ChartPart> ChartPartsFrom(OpenXmlPart part)
    {
        return part switch
        {
            SlidePart slidePart => slidePart.ChartParts,
            SlideLayoutPart slideLayoutPart => slideLayoutPart.ChartParts,
            SlideMasterPart slideMasterPart => slideMasterPart.ChartParts,
            _ => Enumerable.Empty<ChartPart>(),
        };
    }

    private static IEnumerable<T> DistinctByUri<T>(IEnumerable<T?> parts)
        where T : OpenXmlPart
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var part in parts)
        {
            var uri = part?.Uri.OriginalString;
            if (part is not null && uri is not null && seen.Add(uri))
            {
                yield return part;
            }
        }
    }

    private static byte[]? WriteShapeElement(
        OpenXmlPartContainer partContainer,
        P.Shape shape,
        int zIndex,
        bool layoutLike = false,
        GroupTransformContext? groupTransform = null,
        RawTransformIndex? rawTransforms = null)
    {
        var nonVisual = shape.NonVisualShapeProperties?.NonVisualDrawingProperties;
        var placeholder = shape.NonVisualShapeProperties?.ApplicationNonVisualDrawingProperties?.GetFirstChild<P.PlaceholderShape>();
        var shapeProperties = shape.ShapeProperties;
        var bbox = shapeProperties?.Transform2D;
        var isLayoutPlaceholder = layoutLike && placeholder is not null;
        var paragraphs = isLayoutPlaceholder ? new List<byte[]>() : ExtractParagraphs(shape.TextBody).ToList();
        var hasText = paragraphs.Count > 0;

        return Message(output =>
        {
            if (bbox is not null)
            {
                WriteMessage(output, 1, WriteShapeBoundingBox(shapeProperties, groupTransform, nonVisual, rawTransforms));
            }

            var fill = FillFromShapeProperties(partContainer, shapeProperties);
            var line = OutlineFromProperties(shapeProperties);
            var shapeProto = WriteShape(shapeProperties, fill, line, IsTextNamedShape(nonVisual), layoutLike && !isLayoutPlaceholder);
            if (shapeProto is not null)
            {
                WriteMessage(output, 4, shapeProto);
            }

            foreach (var paragraph in paragraphs)
            {
                WriteMessage(output, 6, paragraph);
            }

            WriteString(output, 10, nonVisual?.Name?.Value ?? $"Shape {zIndex}");
            WriteInt32(output, 11, hasText ? ElementTypeText : ElementTypeShape);
            if (isLayoutPlaceholder)
            {
                WriteInt32Always(output, 12, ToInt32(placeholder?.Index) ?? 0);
            }
            else if (placeholder?.Index?.Value is { } placeholderIndex)
            {
                WriteInt32Always(output, 12, (int)Math.Min(int.MaxValue, placeholderIndex));
            }

            WriteString(output, 13, EnumText(placeholder?.Type));

            foreach (var effect in ExtractEffects(shapeProperties))
            {
                WriteMessage(output, 15, effect);
            }

            var bodyTextStyle = WriteBodyTextStyle(shape.TextBody?.BodyProperties, layoutLike);
            if (bodyTextStyle is not null)
            {
                WriteMessage(output, 14, bodyTextStyle);
            }

            if (isLayoutPlaceholder)
            {
                foreach (var levelStyle in ExtractLevelStyles(shape.TextBody?.ListStyle))
                {
                    WriteMessage(output, 16, levelStyle);
                }
            }

            WriteString(output, 27, nonVisual?.Id?.Value.ToString() ?? (zIndex + 1).ToString());
            WriteString(output, 34, CreationId(nonVisual));
        });
    }

    private static byte[]? WriteNotesShapeElement(P.Shape shape, int zIndex)
    {
        var nonVisual = shape.NonVisualShapeProperties?.NonVisualDrawingProperties;
        var placeholder = shape.NonVisualShapeProperties?.ApplicationNonVisualDrawingProperties?.GetFirstChild<P.PlaceholderShape>();
        if (placeholder is null && shape.TextBody is null)
        {
            return null;
        }

        var placeholderType = EnumText(placeholder?.Type);
        var paragraphs = ExtractNotesParagraphs(shape.TextBody).ToList();
        var isTextPlaceholder = paragraphs.Count > 0 || !string.Equals(placeholderType, "sldImg", StringComparison.Ordinal);

        return Message(output =>
        {
            foreach (var paragraph in paragraphs)
            {
                WriteMessage(output, 6, paragraph);
            }

            WriteString(output, 10, nonVisual?.Name?.Value ?? $"Notes Shape {zIndex}");
            if (isTextPlaceholder)
            {
                WriteInt32(output, 11, ElementTypeText);
                WriteMessageAlways(output, 14, Message(_ => { }));
            }

            if (placeholder is not null)
            {
                var placeholderIndex = placeholder.Index?.Value ?? 0;
                WriteInt32Always(output, 12, (int)Math.Min(int.MaxValue, placeholderIndex));
            }

            WriteString(output, 13, placeholderType);
            WriteString(output, 27, nonVisual?.Id?.Value.ToString() ?? (zIndex + 1).ToString());
        });
    }

    private static IEnumerable<byte[]> ExtractNotesParagraphs(OpenXmlElement? textBody)
    {
        if (textBody is null)
        {
            yield break;
        }

        foreach (var paragraph in textBody.Elements<A.Paragraph>())
        {
            var runs = ExtractRuns(paragraph, includeEmptyText: true, preserveBreaks: false, preserveFields: false).ToList();
            yield return Message(output =>
            {
                foreach (var run in runs)
                {
                    WriteMessage(output, 1, run);
                }

                WriteMessageAlways(output, 2, Message(_ => { }));
            });
        }
    }

    private static byte[]? WritePictureElement(
        OpenXmlPartContainer partContainer,
        P.Picture picture,
        int zIndex,
        GroupTransformContext? groupTransform = null)
    {
        var nonVisual = picture.NonVisualPictureProperties?.NonVisualDrawingProperties;
        var transform = picture.ShapeProperties?.Transform2D;
        var blip = picture.BlipFill?.Blip;
        var relationshipId = blip?.Embed?.Value;
        ImagePart? imagePart = null;
        if (!string.IsNullOrEmpty(relationshipId))
        {
            imagePart = partContainer.GetPartById(relationshipId) as ImagePart;
        }
        var imageId = imagePart?.Uri.OriginalString;

        return Message(output =>
        {
            if (transform is not null)
            {
                WriteMessage(output, 1, WriteBoundingBox(transform, groupTransform));
            }

            if (!string.IsNullOrEmpty(imageId))
            {
                WriteMessage(output, 3, WriteImageReference(imageId));
            }

            WriteString(output, 10, nonVisual?.Name?.Value ?? $"Picture {zIndex}");
            WriteInt32(output, 11, imagePart is null ? ElementTypeShape : ElementTypeImageReference);
            if (!string.IsNullOrEmpty(relationshipId) && !string.IsNullOrEmpty(imageId))
            {
                WriteMessage(output, 19, WritePictureFill(picture, relationshipId, imageId));
            }

            var line = OutlineFromProperties(picture.ShapeProperties);
            var shape = WriteShape(picture.ShapeProperties, fill: null, line);
            if (shape is not null)
            {
                WriteMessage(output, 4, shape);
            }

            foreach (var effect in ExtractEffects(picture.ShapeProperties))
            {
                WriteMessage(output, 15, effect);
            }

            var imageMask = WriteImageMask(picture);
            if (imageMask is not null)
            {
                WriteMessage(output, 33, imageMask);
            }

        });
    }

    private static byte[]? WriteGraphicFrameElement(
        OpenXmlPartContainer partContainer,
        P.GraphicFrame graphicFrame,
        int zIndex,
        GroupTransformContext? groupTransform = null)
    {
        var nonVisual = graphicFrame.NonVisualGraphicFrameProperties?.NonVisualDrawingProperties;
        var transform = graphicFrame.Transform;
        var table = graphicFrame.Descendants<A.Table>().FirstOrDefault();
        var chartReference = graphicFrame.Descendants<C.ChartReference>().FirstOrDefault();
        if (transform is null && table is null && chartReference is null && !IsSmartArt(graphicFrame))
        {
            return null;
        }

        return Message(output =>
        {
            if (transform is not null)
            {
                WriteMessage(output, 1, WriteGraphicFrameBoundingBox(transform, table, groupTransform));
            }

            if (chartReference is not null)
            {
                var chartId = ResolveChartId(partContainer, chartReference);
                if (!string.IsNullOrEmpty(chartId))
                {
                    WriteMessage(output, 18, WriteChartReference(chartId));
                    WriteInt32(output, 11, ElementTypeChartReference);
                }
                else
                {
                    WriteInt32(output, 11, ElementTypeShape);
                }
            }
            else if (table is not null)
            {
                WriteMessage(output, 21, WriteTable(table));
                WriteInt32(output, 11, ElementTypeTable);
            }
            else if (IsSmartArt(graphicFrame))
            {
                foreach (var child in ExtractSmartArtChildren(partContainer, graphicFrame))
                {
                    WriteMessage(output, 17, child);
                }

                WriteInt32(output, 11, ElementTypeShape);
            }
            else
            {
                WriteInt32(output, 11, ElementTypeShape);
            }

            WriteString(output, 10, nonVisual?.Name?.Value ?? $"Graphic Frame {zIndex}");
        });
    }

    private static byte[]? WriteConnectionShapeElement(
        OpenXmlPartContainer partContainer,
        P.ConnectionShape connectionShape,
        int zIndex,
        GroupTransformContext? groupTransform = null,
        RawTransformIndex? rawTransforms = null)
    {
        var nonVisual = connectionShape.NonVisualConnectionShapeProperties?.NonVisualDrawingProperties;
        var properties = connectionShape.ShapeProperties;
        var transform = properties?.Transform2D;
        var fill = FillFromShapeProperties(partContainer, properties);
        var line = OutlineFromProperties(properties);

        return Message(output =>
        {
            if (transform is not null)
            {
                WriteMessage(output, 1, WriteShapeBoundingBox(properties, groupTransform, nonVisual, rawTransforms));
            }

            var shape = WriteShape(properties, fill, line, suppressLineDetails: true);
            if (shape is not null)
            {
                WriteMessage(output, 4, shape);
            }
            WriteString(output, 10, nonVisual?.Name?.Value ?? $"Connector {zIndex}");
            WriteInt32(output, 11, ElementTypeShape);
            WriteString(output, 27, nonVisual?.Id?.Value.ToString() ?? (zIndex + 1).ToString());
            WriteMessage(output, 28, WriteConnector(connectionShape, line));
        });
    }

    private static IEnumerable<byte[]> ExtractGroupShapeElements(
        OpenXmlPartContainer partContainer,
        P.GroupShape groupShape,
        bool layoutLike,
        GroupTransformContext? groupTransform,
        RawTransformIndex? rawTransforms)
    {
        var transform = groupShape.GroupShapeProperties?.GetFirstChild<A.TransformGroup>();
        var childTransform = transform is null ? groupTransform : GroupTransformContext.From(transform, groupTransform);
        foreach (var child in ExtractElements(groupShape.ChildElements, partContainer, layoutLike, childTransform, rawTransforms))
        {
            yield return child;
        }
    }

    private static IEnumerable<byte[]> ExtractParagraphs(OpenXmlElement? textBody)
    {
        if (textBody is null)
        {
            yield break;
        }

        var paragraphIndex = 0;
        foreach (var paragraph in textBody.Elements<A.Paragraph>())
        {
            var runs = ExtractRuns(paragraph).ToList();
            if (runs.Count == 0 && paragraph.Elements<A.Run>().Any())
            {
                runs = ExtractRuns(paragraph, includeEmptyText: true).Take(1).ToList();
            }
            var paragraphProperties = paragraph.ParagraphProperties;
            var listStyleParagraphProperties = ListStyleParagraphProperties(textBody, paragraphProperties);
            yield return Message(output =>
            {
                foreach (var run in runs)
                {
                    WriteMessage(output, 1, run);
                }

                var paragraphTextStyle = WriteParagraphTextStyle(paragraphProperties, listStyleParagraphProperties);
                if (paragraphTextStyle is not null)
                {
                    WriteMessageAlways(output, 2, paragraphTextStyle);
                }

                if (paragraphProperties?.LeftMargin?.Value is { } marginLeft)
                {
                    WriteInt32Always(output, 4, marginLeft);
                }

                if (paragraphProperties?.Indent?.Value is { } indent)
                {
                    WriteInt32Always(output, 5, indent);
                }

                var paragraphStyle = WriteParagraphStyle(paragraphProperties);
                if (paragraphStyle is not null)
                {
                    WriteMessage(output, 10, paragraphStyle);
                }
            });
            paragraphIndex++;
        }
    }

    private static OpenXmlElement? ListStyleParagraphProperties(OpenXmlElement textBody, A.ParagraphProperties? paragraphProperties)
    {
        var listStyle = textBody.GetFirstChild<A.ListStyle>();
        if (listStyle is null)
        {
            return null;
        }

        var level = Math.Max(1, Math.Min(9, (IntAttribute(paragraphProperties, "lvl") ?? 0) + 1));
        return listStyle.ChildElements.FirstOrDefault(element =>
            string.Equals(element.LocalName, $"lvl{level}pPr", StringComparison.Ordinal));
    }

    private static IEnumerable<byte[]> ExtractRuns(
        A.Paragraph paragraph,
        bool includeEmptyText = false,
        bool preserveBreaks = true,
        bool preserveFields = false)
    {
        foreach (var child in paragraph.ChildElements)
        {
            var text = child switch
            {
                A.Run run => PreservePresentationText(run.Text?.Text),
                A.Break => preserveBreaks ? "\n" : "",
                A.Field field => preserveFields ? PreservePresentationText(field.Text?.Text) : "",
                _ => "",
            };
            if (text.Length == 0 && !includeEmptyText)
            {
                continue;
            }

            var runProperties = child switch
            {
                A.Run run => run.RunProperties,
                _ => null,
            };
            yield return Message(output =>
            {
                if (includeEmptyText)
                {
                    WriteStringAlways(output, 1, text);
                }
                else
                {
                    WriteString(output, 1, text);
                }
                if (runProperties is not null)
                {
                    WriteMessage(output, 2, WriteTextStyle(runProperties));
                }
            });
        }
    }

    private static byte[] WriteBoundingBox(A.Transform2D transform, GroupTransformContext? groupTransform = null)
    {
        var box = BoundingBox.From(transform, groupTransform);
        return WriteBoundingBox(box);
    }

    private static byte[] WriteGraphicFrameBoundingBox(
        P.Transform transform,
        A.Table? table,
        GroupTransformContext? groupTransform = null)
    {
        var box = BoundingBox.From(transform, groupTransform);
        if (table is null)
        {
            return WriteBoundingBox(box);
        }

        var tableWidth = TableGridWidth(table);
        var tableHeight = TableGridHeight(table);
        return WriteBoundingBox(box with
        {
            Width = tableWidth > 0 ? groupTransform?.TransformWidth(tableWidth) ?? tableWidth : box.Width,
            Height = tableHeight > 0 ? groupTransform?.TransformHeight(tableHeight) ?? tableHeight : box.Height,
        });
    }

    private static long TableGridWidth(A.Table table)
    {
        var width = 0L;
        foreach (var column in table.TableGrid?.Elements<A.GridColumn>() ?? Enumerable.Empty<A.GridColumn>())
        {
            width += column.Width?.Value ?? 0;
        }

        return width;
    }

    private static long TableGridHeight(A.Table table)
    {
        var height = 0L;
        foreach (var row in table.Elements<A.TableRow>())
        {
            height += row.Height?.Value ?? 0;
        }

        return height;
    }

    private static byte[] WriteShapeBoundingBox(
        P.ShapeProperties? properties,
        GroupTransformContext? groupTransform = null,
        P.NonVisualDrawingProperties? nonVisual = null,
        RawTransformIndex? rawTransforms = null)
    {
        var transform = properties?.Transform2D;
        if (properties is null || transform is null)
        {
            return [];
        }

        var box =
            RawBoundingBox(nonVisual, rawTransforms) ??
            CustomGeometryBoundingBox(properties, transform, groupTransform) ??
            BoundingBox.From(transform, groupTransform);
        return WriteBoundingBox(box);
    }

    private static BoundingBox? RawBoundingBox(P.NonVisualDrawingProperties? nonVisual, RawTransformIndex? rawTransforms)
    {
        var id = nonVisual?.Id?.Value.ToString();
        return id is not null && rawTransforms?.TryGet(id, out var box) == true ? box : null;
    }

    private static byte[] WriteBoundingBox(BoundingBox box)
    {
        return Message(output =>
        {
            WriteInt64Always(output, 1, box.X);
            WriteInt64Always(output, 2, box.Y);
            WriteInt64Always(output, 3, box.Width);
            WriteInt64Always(output, 4, box.Height);
            WriteInt32(output, 5, box.Rotation);
            WriteBool(output, 6, box.HorizontalFlip);
            WriteBool(output, 7, box.VerticalFlip);
        });
    }

    private static BoundingBox? CustomGeometryBoundingBox(
        P.ShapeProperties properties,
        A.Transform2D transform,
        GroupTransformContext? groupTransform)
    {
        var geometry = properties.GetFirstChild<A.CustomGeometry>();
        var transformX = TransformValue(transform, "off", "x");
        var transformY = TransformValue(transform, "off", "y");
        var transformWidth = TransformValue(transform, "ext", "cx");
        var transformHeight = TransformValue(transform, "ext", "cy");
        if (geometry is null ||
            transformX is null ||
            transformY is null ||
            transformWidth is null ||
            transformHeight is null ||
            transformWidth.Value == 0 ||
            transformHeight.Value == 0)
        {
            return null;
        }

        double? minX = null;
        double? minY = null;
        double? maxX = null;
        double? maxY = null;
        foreach (var path in geometry.GetFirstChild<A.PathList>()?.Elements<A.Path>() ?? [])
        {
            var pathWidth = ToLong(path.Width);
            var pathHeight = ToLong(path.Height);
            if (pathWidth is null || pathHeight is null || pathWidth.Value == 0 || pathHeight.Value == 0)
            {
                continue;
            }

            foreach (var point in path.Descendants().Where(element => element.LocalName == "pt"))
            {
                var pointX = ToLong(AttributeValue(point, "x"));
                var pointY = ToLong(AttributeValue(point, "y"));
                if (pointX is null || pointY is null)
                {
                    continue;
                }

                var x = transformX.Value + transformWidth.Value * (pointX.Value / (double)pathWidth.Value);
                var y = transformY.Value + transformHeight.Value * (pointY.Value / (double)pathHeight.Value);
                minX = minX is null ? x : Math.Min(minX.Value, x);
                minY = minY is null ? y : Math.Min(minY.Value, y);
                maxX = maxX is null ? x : Math.Max(maxX.Value, x);
                maxY = maxY is null ? y : Math.Max(maxY.Value, y);
            }
        }

        if (minX is null || minY is null || maxX is null || maxY is null)
        {
            return null;
        }

        if (groupTransform is not null)
        {
            return new BoundingBox(
                groupTransform.TransformRoundX(minX.Value),
                groupTransform.TransformRoundY(minY.Value),
                groupTransform.TransformFloorWidth(maxX.Value - minX.Value),
                groupTransform.TransformFloorHeight(maxY.Value - minY.Value),
                transform.Rotation?.Value,
                CombineFlip(groupTransform.EffectiveHorizontalFlip, transform.HorizontalFlip?.Value),
                CombineFlip(groupTransform.EffectiveVerticalFlip, transform.VerticalFlip?.Value));
        }

        return BoundingBox.FromRaw(
            RoundEmu(minX.Value),
            RoundEmu(minY.Value),
            RoundEmu(maxX.Value - minX.Value),
            RoundEmu(maxY.Value - minY.Value),
            transform.Rotation?.Value,
            transform.HorizontalFlip?.Value,
            transform.VerticalFlip?.Value,
            groupTransform);
    }

    private static byte[] WriteBoundingBox(P.Transform transform, GroupTransformContext? groupTransform = null)
    {
        var box = BoundingBox.From(transform, groupTransform);
        return Message(output =>
        {
            WriteInt64Always(output, 1, box.X);
            WriteInt64Always(output, 2, box.Y);
            WriteInt64Always(output, 3, box.Width);
            WriteInt64Always(output, 4, box.Height);
        });
    }

    private static byte[] WriteBoundingBox(A.TransformGroup transform)
    {
        return Message(output =>
        {
            WriteInt64Always(output, 1, ToLong(transform.Offset?.X));
            WriteInt64Always(output, 2, ToLong(transform.Offset?.Y));
            WriteInt64Always(output, 3, ToLong(transform.Extents?.Cx));
            WriteInt64Always(output, 4, ToLong(transform.Extents?.Cy));
        });
    }

    private static byte[]? WriteShape(
        OpenXmlElement? properties,
        byte[]? fill,
        A.Outline? line,
        bool suppressLineStyle = false,
        bool writeDefaultLine = false,
        bool suppressLineDetails = false)
    {
        var geometry = properties?.GetFirstChild<A.PresetGeometry>();
        var customGeometry = properties?.GetFirstChild<A.CustomGeometry>();
        var geometryCode = customGeometry is not null ? ShapeGeometryCustom : GeometryCode(geometry);
        var adjustments = ExtractAdjustments(geometry).ToList();
        var customPaths = ExtractCustomGeometryPaths(customGeometry).ToList();
        if (geometry is null && customGeometry is null && fill is null && line is null && properties?.GetFirstChild<A.NoFill>() is null && adjustments.Count == 0)
        {
            return null;
        }

        return Message(output =>
        {
            WriteInt32(output, 1, geometryCode);
            if (fill is not null)
            {
                WriteMessage(output, 5, fill);
            }
            else if (properties?.GetFirstChild<A.NoFill>() is not null)
            {
                WriteMessageAlways(output, 5, Message(_ => { }));
            }

            if (line is not null)
            {
                WriteMessage(output, 6, WriteLine(line, suppressLineStyle, suppressLineDetails));
            }
            else if (writeDefaultLine)
            {
                WriteMessage(output, 6, WriteDefaultLine());
            }

            foreach (var adjustment in adjustments)
            {
                WriteMessage(output, 7, adjustment);
            }

            var rectangle = customGeometry?.GetFirstChild<A.Rectangle>();
            if (rectangle is not null)
            {
                WriteMessage(output, 8, WriteCustomGeometryRectangle(rectangle));
            }

            foreach (var path in customPaths)
            {
                WriteMessage(output, 9, path);
            }
        });
    }

    private static byte[] WriteCustomGeometryRectangle(A.Rectangle rectangle)
    {
        return Message(output =>
        {
            WriteString(output, 1, AttributeValue(rectangle, "t"));
            WriteString(output, 2, AttributeValue(rectangle, "l"));
            WriteString(output, 3, AttributeValue(rectangle, "r"));
            WriteString(output, 4, AttributeValue(rectangle, "b"));
        });
    }

    private static IEnumerable<byte[]> ExtractCustomGeometryPaths(A.CustomGeometry? geometry)
    {
        var pathList = geometry?.GetFirstChild<A.PathList>();
        if (pathList is null)
        {
            yield break;
        }

        foreach (var path in pathList.Elements<A.Path>())
        {
            var commands = ExtractCustomPathCommands(path).ToList();
            yield return Message(output =>
            {
                WriteInt64(output, 1, ToLong(path.Width));
                WriteInt64(output, 2, ToLong(path.Height));
                foreach (var command in commands)
                {
                    WriteMessage(output, 3, command);
                }
                WriteString(output, 4, AttributeValue(path, "id"));
            });
        }
    }

    private static IEnumerable<byte[]> ExtractCustomPathCommands(A.Path path)
    {
        foreach (var command in path.ChildElements)
        {
            switch (command.LocalName)
            {
                case "moveTo":
                    yield return Message(output => WriteMessage(output, 1, WriteCustomPathPoint(command.GetFirstChild<A.Point>())));
                    break;
                case "lnTo":
                    yield return Message(output => WriteMessage(output, 2, WriteCustomPathPoint(command.GetFirstChild<A.Point>())));
                    break;
                case "close":
                    yield return Message(output => WriteMessageAlways(output, 3, Message(_ => { })));
                    break;
                case "quadBezTo":
                    yield return Message(output => WriteMessage(output, 4, WriteQuadraticBezier(command.Elements<A.Point>().ToList())));
                    break;
                case "cubicBezTo":
                    yield return Message(output => WriteMessage(output, 5, WriteCubicBezier(command.Elements<A.Point>().ToList())));
                    break;
                case "arcTo":
                    yield return Message(output => WriteMessage(output, 6, WriteCustomPathArc(command)));
                    break;
            }
        }
    }

    private static byte[] WriteCustomPathPoint(A.Point? point)
    {
        return Message(output =>
        {
            WriteInt64(output, 1, ToLong(AttributeValue(point, "x")));
            WriteInt64(output, 2, ToLong(AttributeValue(point, "y")));
        });
    }

    private static byte[] WriteQuadraticBezier(IReadOnlyList<A.Point> points)
    {
        return Message(output =>
        {
            WriteInt64(output, 1, ToLong(AttributeValue(points.ElementAtOrDefault(0), "x")));
            WriteInt64(output, 2, ToLong(AttributeValue(points.ElementAtOrDefault(0), "y")));
            WriteInt64(output, 3, ToLong(AttributeValue(points.ElementAtOrDefault(1), "x")));
            WriteInt64(output, 4, ToLong(AttributeValue(points.ElementAtOrDefault(1), "y")));
        });
    }

    private static byte[] WriteCubicBezier(IReadOnlyList<A.Point> points)
    {
        return Message(output =>
        {
            WriteInt64(output, 1, ToLong(AttributeValue(points.ElementAtOrDefault(0), "x")));
            WriteInt64(output, 2, ToLong(AttributeValue(points.ElementAtOrDefault(0), "y")));
            WriteInt64(output, 3, ToLong(AttributeValue(points.ElementAtOrDefault(1), "x")));
            WriteInt64(output, 4, ToLong(AttributeValue(points.ElementAtOrDefault(1), "y")));
            WriteInt64(output, 5, ToLong(AttributeValue(points.ElementAtOrDefault(2), "x")));
            WriteInt64(output, 6, ToLong(AttributeValue(points.ElementAtOrDefault(2), "y")));
        });
    }

    private static byte[] WriteCustomPathArc(OpenXmlElement arc)
    {
        return Message(output =>
        {
            WriteInt64(output, 1, ToLong(AttributeValue(arc, "wR")));
            WriteInt64(output, 2, ToLong(AttributeValue(arc, "hR")));
            WriteInt64(output, 3, ToLong(AttributeValue(arc, "stAng")));
            WriteInt64(output, 4, ToLong(AttributeValue(arc, "swAng")));
        });
    }

    private static byte[] WriteDefaultLine()
    {
        return Message(output =>
        {
            WriteMessageAlways(output, 3, Message(_ => { }));
        });
    }

    private static byte[]? WriteBodyTextStyle(A.BodyProperties? bodyProperties, bool writeLayoutDefaults = false)
    {
        if (bodyProperties is null)
        {
            return null;
        }

        return Message(output =>
        {
            WriteInt32(output, 1, BodyAnchorCode(bodyProperties.Anchor));
            WriteInt32(output, 2, VerticalTextCode(bodyProperties.Vertical));

            WriteInt32Always(output, 10, bodyProperties.BottomInset?.Value ?? 0);
            WriteInt32Always(output, 11, bodyProperties.LeftInset?.Value ?? 0);
            WriteInt32Always(output, 12, bodyProperties.RightInset?.Value ?? 0);
            WriteInt32Always(output, 13, bodyProperties.TopInset?.Value ?? 0);
            if (writeLayoutDefaults)
            {
                WriteBoolValue(output, 14, true);
            }

            WriteInt32(output, 20, TextWrappingCode(bodyProperties.Wrap));
            var autoFit = WriteAutoFit(bodyProperties);
            if (autoFit is not null)
            {
                WriteMessageAlways(output, 21, autoFit);
            }
            else if (writeLayoutDefaults)
            {
                WriteMessageAlways(output, 21, WriteNoAutoFit());
            }
        });
    }

    private static byte[]? WriteParagraphTextStyle(
        A.ParagraphProperties? paragraphProperties,
        OpenXmlElement? listStyleParagraphProperties = null)
    {
        if (paragraphProperties is null && listStyleParagraphProperties is null)
        {
            return null;
        }

        var textProperties =
            paragraphProperties?.GetFirstChild<A.DefaultRunProperties>() ??
            listStyleParagraphProperties?.GetFirstChild<A.DefaultRunProperties>();
        var styleParagraphProperties = paragraphProperties ?? listStyleParagraphProperties;

        return Message(output =>
        {
            WriteTextStyleProperties(output, textProperties, styleParagraphProperties);

            var alignment = AlignmentCode(AttributeValue(styleParagraphProperties, "algn"));
            if (alignment == 4)
            {
                alignment = null;
            }

            WriteInt32(output, 8, alignment);
        });
    }

    private static byte[]? WriteParagraphStyle(OpenXmlElement? paragraphProperties)
    {
        if (paragraphProperties is null)
        {
            return null;
        }

        return Message(output =>
        {
            var bulletCharacter = paragraphProperties.GetFirstChild<A.CharacterBullet>()?.Char?.Value;
            if (bulletCharacter is not null || paragraphProperties.GetFirstChild<A.NoBullet>() is not null)
            {
                WriteStringAlways(output, 1, bulletCharacter ?? "");
            }
            if (IntAttribute(paragraphProperties, "marL") is { } marginLeft)
            {
                WriteInt32Always(output, 2, marginLeft);
            }

            if (IntAttribute(paragraphProperties, "indent") is { } indent)
            {
                WriteInt32Always(output, 3, indent);
            }

            if (paragraphProperties.GetFirstChild<A.LineSpacing>()?.GetFirstChild<A.SpacingPercent>()?.Val?.Value is { } lineSpacing)
            {
                WriteInt32Always(output, 4, lineSpacing);
            }
        });
    }

    private static byte[]? WriteAutoFit(OpenXmlElement bodyProperties)
    {
        if (bodyProperties.ChildElements.Any(element => string.Equals(element.LocalName, "normAutofit", StringComparison.Ordinal)))
        {
            return Message(output =>
            {
                WriteMessageAlways(output, 2, Message(_ => { }));
            });
        }

        if (bodyProperties.ChildElements.Any(element => string.Equals(element.LocalName, "spAutoFit", StringComparison.Ordinal)))
        {
            return Message(output =>
            {
                WriteMessageAlways(output, 3, Message(_ => { }));
            });
        }

        if (bodyProperties.ChildElements.Any(element => string.Equals(element.LocalName, "noAutofit", StringComparison.Ordinal)))
        {
            return WriteNoAutoFit();
        }

        return null;
    }

    private static byte[] WriteNoAutoFit()
    {
        return Message(output =>
        {
            WriteMessageAlways(output, 1, Message(_ => { }));
        });
    }

    private static byte[]? WriteBackground(P.Background? background)
    {
        if (background is null)
        {
            return null;
        }

        return Message(output =>
        {
            var styleReference = background.GetFirstChild<P.BackgroundStyleReference>();
            if (styleReference is not null)
            {
                WriteMessage(output, 2, WriteBackgroundStyleReference(styleReference));
                return;
            }

            var fill = SolidFillFromBackground(background);
            if (fill is not null)
            {
                WriteMessage(output, 3, WriteFill(fill));
            }
        });
    }

    private static byte[] WriteBackgroundStyleReference(P.BackgroundStyleReference styleReference)
    {
        return Message(output =>
        {
            if (styleReference.Index?.Value is { } index)
            {
                WriteInt32(output, 1, (int)Math.Min(int.MaxValue, index));
            }

            var schemeColor = styleReference.GetFirstChild<A.SchemeColor>();
            if (schemeColor?.Val is not null)
            {
                WriteString(output, 2, EnumText(schemeColor.Val));
            }
        });
    }

    private static byte[] WriteColorScheme(A.ColorScheme colorScheme)
    {
        return Message(output =>
        {
            WriteString(output, 1, colorScheme.Name?.Value);
            foreach (var child in OrderedColorSchemeElements(colorScheme))
            {
                var color = ColorFromElement(child);
                if (color is not null)
                {
                    WriteMessage(output, 2, WriteThemeColor(child.LocalName, color));
                }
            }
        });
    }

    private static byte[] WriteThemeColor(string name, ColorValue color)
    {
        return Message(output =>
        {
            WriteString(output, 1, name);
            WriteMessage(output, 2, WriteColor(color));
        });
    }

    private static byte[] WriteColorMap(P.ColorMap colorMap)
    {
        return Message(output =>
        {
            WriteString(output, 1, EnumText(colorMap.Accent1));
            WriteString(output, 2, EnumText(colorMap.Accent2));
            WriteString(output, 3, EnumText(colorMap.Accent3));
            WriteString(output, 4, EnumText(colorMap.Accent4));
            WriteString(output, 5, EnumText(colorMap.Accent5));
            WriteString(output, 6, EnumText(colorMap.Accent6));
            WriteString(output, 7, EnumText(colorMap.Background1));
            WriteString(output, 8, EnumText(colorMap.Background2));
            WriteString(output, 9, EnumText(colorMap.Text1));
            WriteString(output, 10, EnumText(colorMap.Text2));
            WriteString(output, 11, EnumText(colorMap.Hyperlink));
            WriteString(output, 12, EnumText(colorMap.FollowedHyperlink));
        });
    }

    private static IEnumerable<byte[]> ExtractLevelStyles(OpenXmlElement? styleContainer, bool writeDefaults = false)
    {
        if (styleContainer is null)
        {
            yield break;
        }

        foreach (var child in styleContainer.ChildElements)
        {
            var level = LevelFromParagraphStyleName(child.LocalName);
            if (level is null)
            {
                continue;
            }

            yield return Message(output =>
            {
                WriteInt32(output, 2, level.Value);
                var textStyle = WriteLevelTextStyle(child, writeDefaults);
                if (textStyle is not null)
                {
                    WriteMessage(output, 3, textStyle);
                }

                var paragraphStyle = WriteParagraphStyle(child);
                if (paragraphStyle is not null)
                {
                    WriteMessage(output, 4, paragraphStyle);
                }

                if (SpacingBefore(child) is { } spaceBefore)
                {
                    WriteInt32Always(output, 5, spaceBefore);
                }

                if (SpacingAfter(child) is { } spaceAfter)
                {
                    WriteInt32Always(output, 6, spaceAfter);
                }
                else if (writeDefaults)
                {
                    WriteInt32Always(output, 6, 0);
                }
            });
        }
    }

    private static IEnumerable<OpenXmlElement> OrderedColorSchemeElements(A.ColorScheme colorScheme)
    {
        var byName = colorScheme.ChildElements
            .GroupBy(element => element.LocalName, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        foreach (var name in new[] { "accent1", "accent2", "accent3", "accent4", "accent5", "accent6", "dk1", "lt1", "dk2", "lt2", "hlink", "folHlink" })
        {
            if (byName.TryGetValue(name, out var element))
            {
                yield return element;
            }
        }
    }

    private static int? BodyAnchorCode(OpenXmlSimpleType? value)
    {
        return EnumText(value) switch
        {
            "t" => 1,
            "ctr" => 2,
            "b" => 3,
            "just" => 4,
            "dist" => 5,
            _ => null,
        };
    }

    private static int? TextWrappingCode(OpenXmlSimpleType? value)
    {
        return EnumText(value) switch
        {
            "none" => 1,
            "square" => 2,
            _ => null,
        };
    }

    private static int? VerticalTextCode(OpenXmlSimpleType? value)
    {
        return EnumText(value) switch
        {
            "horz" => 1,
            "vert" => 2,
            "vert270" => 3,
            "wordArtVert" => 4,
            "eaVert" => 5,
            _ => null,
        };
    }

    private static int? AlignmentCode(OpenXmlSimpleType? value)
    {
        return AlignmentCode(EnumText(value));
    }

    private static int? AlignmentCode(string? value)
    {
        return value switch
        {
            "l" => 1,
            "ctr" => 2,
            "r" => 3,
            "just" => 4,
            "justLow" => 5,
            "dist" => 6,
            "thaiDist" => 7,
            _ => null,
        };
    }

    private static string EnumText(OpenXmlSimpleType? value)
    {
        return value?.InnerText ?? "";
    }

    private static int? LevelFromParagraphStyleName(string localName)
    {
        return localName.Length == 7 &&
            localName.StartsWith("lvl", StringComparison.Ordinal) &&
            localName.EndsWith("pPr", StringComparison.Ordinal) &&
            int.TryParse(localName.AsSpan(3, 1), out var level)
            ? level
            : null;
    }

    private static IEnumerable<byte[]> ExtractAdjustments(A.PresetGeometry? geometry)
    {
        foreach (var guide in geometry?.AdjustValueList?.ChildElements ?? Enumerable.Empty<OpenXmlElement>())
        {
            var name = AttributeValue(guide, "name");
            var formula = AttributeValue(guide, "fmla");
            if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(formula))
            {
                continue;
            }

            yield return Message(output =>
            {
                WriteString(output, 1, name);
                WriteString(output, 2, formula);
            });
        }
    }

    private static byte[] WriteFill(A.SolidFill fill)
    {
        return Message(output =>
        {
            WriteInt32(output, 1, FillTypeSolid);
            var color = ColorFromSolidFill(fill);
            if (color is not null)
            {
                WriteMessage(output, 2, WriteColor(color));
            }
        });
    }

    private static byte[]? WriteFillFromElement(OpenXmlElement fill)
    {
        return fill switch
        {
            A.SolidFill solidFill => WriteFill(solidFill),
            A.GradientFill gradientFill => WriteGradientFill(gradientFill),
            _ => null,
        };
    }

    private static byte[] WriteGradientFill(A.GradientFill fill)
    {
        return Message(output =>
        {
            WriteInt32(output, 1, FillTypeGradient);
            foreach (var stop in fill.GradientStopList?.Elements<A.GradientStop>() ?? Enumerable.Empty<A.GradientStop>())
            {
                var color = ColorFromElement(stop);
                if (color is null)
                {
                    continue;
                }

                WriteMessage(output, 3, Message(stopOutput =>
                {
                    if (stop.Position?.Value is { } position)
                    {
                        WriteInt32Always(stopOutput, 1, position);
                    }

                    WriteMessage(stopOutput, 2, WriteColor(color));
                }));
            }

            var linear = fill.GetFirstChild<A.LinearGradientFill>();
            if (linear is not null)
            {
                WriteInt32(output, 5, GradientKindLinear);
                if (linear.Angle?.Value is { } angle)
                {
                    WriteDouble(output, 6, angle / 60000d);
                }

                WriteBoolValue(output, 7, linear.Scaled?.Value);
            }
            else if (fill.GetFirstChild<A.PathGradientFill>() is not null)
            {
                WriteInt32(output, 5, GradientKindPath);
            }
        });
    }

    private static byte[] WritePictureFill(P.Picture picture, string relationshipId, string imageId)
    {
        return WriteImageFill(
            relationshipId,
            imageId,
            picture.BlipFill?.SourceRectangle,
            picture.BlipFill?.GetFirstChild<A.Stretch>()?.GetFirstChild<A.FillRectangle>(),
            ToInt32(picture.BlipFill?.Blip?.GetFirstChild<A.AlphaModulationFixed>()?.Amount));
    }

    private static byte[] WriteImageFill(
        string relationshipId,
        string imageId,
        A.SourceRectangle? sourceRectangle,
        OpenXmlElement? fillRectangle,
        int? alphaModFix = null)
    {
        var hasSourceRectangle = HasExplicitRectangleAttributes(sourceRectangle);
        var hasFillRectangle = HasExplicitRectangleAttributes(fillRectangle);
        return Message(output =>
        {
            WriteInt32(output, 1, FillTypePicture);
            WriteString(output, 4, relationshipId);
            WriteMessage(output, 11, WriteImageReference(imageId));
            WriteInt32(output, 12, alphaModFix);
            if (hasSourceRectangle)
            {
                WriteMessage(output, 14, WriteRectangleAttributes(sourceRectangle));
            }
            else if (hasFillRectangle)
            {
                WriteMessage(output, 14, WriteRectangleAttributes(fillRectangle));
            }
            else
            {
                WriteMessageAlways(output, 15, Message(_ => { }));
            }
        });
    }

    private static bool HasExplicitRectangleAttributes(OpenXmlElement? rectangle)
    {
        return NonZeroIntAttribute(rectangle, "l") is not null ||
            NonZeroIntAttribute(rectangle, "t") is not null ||
            NonZeroIntAttribute(rectangle, "r") is not null ||
            NonZeroIntAttribute(rectangle, "b") is not null;
    }

    private static byte[] WriteRectangleAttributes(OpenXmlElement? rectangle)
    {
        return Message(output =>
        {
            WriteInt32(output, 1, NonZeroIntAttribute(rectangle, "l"));
            WriteInt32(output, 2, NonZeroIntAttribute(rectangle, "t"));
            WriteInt32(output, 3, NonZeroIntAttribute(rectangle, "r"));
            WriteInt32(output, 4, NonZeroIntAttribute(rectangle, "b"));
        });
    }

    private static int? NonZeroIntAttribute(OpenXmlElement? element, string name)
    {
        var value = IntAttribute(element, name);
        return value == 0 ? null : value;
    }

    private static byte[]? WriteImageMask(P.Picture picture)
    {
        var sourceRectangle = picture.BlipFill?.SourceRectangle;
        var geometry = PresetGeometryName(picture.ShapeProperties?.GetFirstChild<A.PresetGeometry>());
        var hasCrop =
            sourceRectangle?.Left is not null ||
            sourceRectangle?.Top is not null ||
            sourceRectangle?.Right is not null ||
            sourceRectangle?.Bottom is not null;
        if (string.IsNullOrEmpty(geometry) || string.Equals(geometry, "rect", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Message(output =>
        {
            WriteString(output, 1, geometry);
            WriteInt32(output, 2, ToInt32(sourceRectangle?.Left));
            WriteInt32(output, 3, ToInt32(sourceRectangle?.Top));
            WriteInt32(output, 4, ToInt32(sourceRectangle?.Right));
            WriteInt32(output, 5, ToInt32(sourceRectangle?.Bottom));
        });
    }

    private static byte[] WriteLine(OpenXmlElement line, bool suppressStyle = false, bool suppressDetails = false)
    {
        return Message(output =>
        {
            var fill = line.GetFirstChild<A.SolidFill>();
            var noFill = line.GetFirstChild<A.NoFill>() is not null;
            var lineStyle = LineStyle(line);
            if (noFill)
            {
                WriteMessageAlways(output, 3, Message(_ => { }));
                return;
            }

            if (lineStyle != 0 && !suppressStyle)
            {
                WriteInt32(output, 1, lineStyle);
            }

            if (fill is not null)
            {
                WriteInt32(output, 2, LineWidth(line));
                WriteMessage(output, 3, WriteFill(fill));
            }
            else
            {
                WriteInt32(output, 2, LineWidth(line));
            }

            if (!suppressDetails)
            {
                WriteInt32(output, 4, LineCompound(AttributeValue(line, "cmpd")));
                WriteInt32(output, 6, ConnectorLineCap(line is A.Outline outline ? outline.CapType?.Value.ToString() : null, AttributeValue(line, "cap") ?? ""));
                WriteInt32(output, 7, ConnectorLineJoin(line));
                var head = WriteConnectorLineEnd(line.GetFirstChild<A.HeadEnd>() ?? FirstChildByLocalName(line, "headEnd"));
                if (head is not null)
                {
                    WriteMessage(output, 8, head);
                }

                var tail = WriteConnectorLineEnd(line.GetFirstChild<A.TailEnd>() ?? FirstChildByLocalName(line, "tailEnd"));
                if (tail is not null)
                {
                    WriteMessage(output, 9, tail);
                }
            }
        });
    }

    private static int LineStyle(OpenXmlElement line)
    {
        return line.GetFirstChild<A.PresetDash>()?.Val?.InnerText switch
        {
            "solid" => 1,
            "dash" => 2,
            "dot" => 3,
            "dashDot" => 4,
            "lgDash" => 6,
            "sysDash" => 7,
            "sysDot" => 8,
            "lgDashDot" => 9,
            "sysDashDot" => 10,
            "lgDashDotDot" => 11,
            "sysDashDotDot" => 12,
            _ => line.GetFirstChild<A.SolidFill>() is not null ? 1 : 0,
        };
    }

    private static int LineCompound(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            "sng" or "single" => 1,
            "dbl" or "double" => 2,
            "thickthin" => 3,
            "thinthick" => 4,
            "trid" or "triple" => 5,
            _ => 0,
        };
    }

    private static int? LineWidth(OpenXmlElement line)
    {
        if (line is A.Outline outline)
        {
            return ToInt32(outline.Width);
        }

        var width = line.GetAttribute("w", string.Empty).Value;
        return int.TryParse(width, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    private static byte[] WriteEffectStyle(A.EffectStyle effectStyle)
    {
        return Message(output =>
        {
            foreach (var shadow in effectStyle.Descendants<A.OuterShadow>())
            {
                WriteMessage(output, 1, Message(effectOutput =>
                {
                    WriteInt32(effectOutput, 1, EffectTypeShadow);
                    WriteMessage(effectOutput, 2, Message(shadowOutput =>
                    {
                        var color = ColorFromElement(shadow);
                        if (color is not null)
                        {
                            WriteMessage(shadowOutput, 2, WriteColor(color));
                        }

                        WriteInt32(shadowOutput, 3, ToInt32(shadow.BlurRadius));
                        WriteInt32(shadowOutput, 4, ToInt32(shadow.Distance));
                        WriteInt32(shadowOutput, 5, shadow.Direction?.Value);
                        WriteString(shadowOutput, 6, EnumText(shadow.Alignment));
                        WriteBoolValue(shadowOutput, 7, shadow.RotateWithShape?.Value);
                    }));
                }));
            }
        });
    }

    private static byte[] WriteTextStyle(A.RunProperties runProperties)
    {
        return Message(output =>
        {
            WriteTextStyleProperties(output, runProperties, null);
        });
    }

    private static byte[]? WriteLevelTextStyle(OpenXmlElement paragraphProperties, bool writeDefaults = false)
    {
        var defaultRunProperties = paragraphProperties.GetFirstChild<A.DefaultRunProperties>();
        if (defaultRunProperties is null && AttributeValue(paragraphProperties, "algn") is null)
        {
            return null;
        }

        return Message(output =>
        {
            WriteTextStyleProperties(output, defaultRunProperties, paragraphProperties, writeDefaults);
        });
    }

    private static void WriteTextStyleProperties(
        CodedOutputStream output,
        OpenXmlElement? textProperties,
        OpenXmlElement? paragraphProperties,
        bool writeDefaults = false)
    {
        var bold = BoolAttribute(textProperties, "b");
        var italic = BoolAttribute(textProperties, "i");
        if (writeDefaults)
        {
            WriteBoolValue(output, 4, bold ?? false);
            WriteBoolValue(output, 5, italic ?? false);
        }
        else
        {
            WriteBool(output, 4, bold);
            WriteBool(output, 5, italic);
        }

        WriteInt32(output, 6, IntAttribute(textProperties, "sz"));
        var fill = textProperties?.GetFirstChild<A.SolidFill>();
        if (fill is not null)
        {
            WriteMessage(output, 7, WriteFill(fill));
        }

        var highlight = textProperties?.GetFirstChild<A.Highlight>();
        if (highlight is not null)
        {
            WriteString(output, 17, PptxTextHighlightScheme(highlight));
        }

        var alignment = AlignmentCode(AttributeValue(paragraphProperties, "algn"));
        WriteInt32(output, 8, alignment == 4 ? null : alignment);

        var underline = AttributeValue(textProperties, "u");
        if (writeDefaults)
        {
            WriteStringAlways(output, 9, string.IsNullOrEmpty(underline) ? "none" : underline);
        }
        else if (!string.IsNullOrEmpty(underline) && underline != "none")
        {
            WriteString(output, 9, underline);
        }

        var typeface =
            textProperties?.GetFirstChild<A.LatinFont>()?.Typeface?.Value ??
            textProperties?.GetFirstChild<A.EastAsianFont>()?.Typeface?.Value ??
            textProperties?.GetFirstChild<A.ComplexScriptFont>()?.Typeface?.Value;
        WriteString(output, 18, typeface);
    }

    private static string? PptxTextHighlightScheme(A.Highlight highlight)
    {
        var color = ColorFromElement(highlight);
        if (color is null)
        {
            return null;
        }

        var value = color.LastColor ?? color.Value;
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        return $"__pptxHighlight:{value}";
    }

    private static byte[] WriteImage(ImagePart imagePart, string alt)
    {
        return Message(output =>
        {
            WriteString(output, 1, NormalizeImageContentType(imagePart.ContentType));
            using var stream = imagePart.GetStream();
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            var bytes = memory.ToArray();
            if (bytes.Length <= OpenXmlReaderLimits.MaxImageBytes)
            {
                WriteBytes(output, 2, bytes);
            }

            WriteString(output, 4, alt);
        });
    }

    private static byte[] WriteRootImage(ImagePart imagePart)
    {
        return Message(output =>
        {
            WriteString(output, 1, NormalizeImageContentType(imagePart.ContentType));
            using var stream = imagePart.GetStream();
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            var bytes = memory.ToArray();
            if (bytes.Length <= OpenXmlReaderLimits.MaxImageBytes)
            {
                WriteBytes(output, 2, bytes);
            }

            WriteString(output, 3, imagePart.Uri.OriginalString);
        });
    }

    private static byte[] WriteImageReference(string relationshipId)
    {
        return Message(output =>
        {
            WriteString(output, 1, relationshipId);
        });
    }

    private static byte[] WriteTable(A.Table table)
    {
        return Message(output =>
        {
            var rowIndex = 0;
            foreach (var row in table.Elements<A.TableRow>().Take(OpenXmlReaderLimits.MaxRowsPerTable))
            {
                WriteMessage(output, 1, WriteTableRow(row, rowIndex));
                rowIndex++;
            }

            foreach (var column in table.TableGrid?.Elements<A.GridColumn>() ?? Enumerable.Empty<A.GridColumn>())
            {
                if (column.Width?.Value is { } width)
                {
                    WriteInt32Always(output, 2, (int)Math.Min(int.MaxValue, width));
                }
            }

            var properties = WriteTableProperties(table);
            if (properties is not null)
            {
                WriteMessage(output, 3, properties);
            }
        });
    }

    private static byte[]? WriteTableStyles(TableStylesPart? tableStylesPart)
    {
        var root = tableStylesPart?.RootElement;
        if (root is null)
        {
            return null;
        }

        return Message(output =>
        {
            WriteString(output, 1, AttributeValue(root, "def"));
            WriteString(output, 2, root.OuterXml);
        });
    }

    private static byte[] WriteTableRow(A.TableRow row, int rowIndex)
    {
        return Message(rowOutput =>
        {
            var cellIndex = 0;
            foreach (var cell in row.Elements<A.TableCell>().Take(OpenXmlReaderLimits.MaxCellsPerRow))
            {
                WriteMessage(rowOutput, 1, WriteTableCell(cell, rowIndex, cellIndex));
                cellIndex++;
            }

            WriteInt32(rowOutput, 2, ToInt32(row.Height));
        });
    }

    private static byte[] WriteTableCell(A.TableCell cell, int rowIndex, int cellIndex)
    {
        return Message(cellOutput =>
        {
            var textStyle = WriteBodyTextStyle(cell.TextBody?.BodyProperties);
            if (textStyle is not null)
            {
                WriteMessage(cellOutput, 2, textStyle);
            }

            foreach (var paragraph in ExtractParagraphs(cell.TextBody))
            {
                WriteMessageAlways(cellOutput, 3, paragraph);
            }

            var fill = FillFromDirectProperties(cell.TableCellProperties);
            if (fill is not null)
            {
                WriteMessage(cellOutput, 5, fill);
            }

            var borders = WriteTableCellLines(cell.TableCellProperties);
            if (borders is not null)
            {
                WriteMessage(cellOutput, 6, borders);
            }

            WriteInt32(cellOutput, 8, cell.GridSpan?.Value);
            WriteInt32(cellOutput, 9, cell.RowSpan?.Value);
            WriteBoolValue(cellOutput, 10, cell.HorizontalMerge?.Value);
            WriteBoolValue(cellOutput, 11, cell.VerticalMerge?.Value);
            WriteString(cellOutput, 12, cell.TableCellProperties?.Vertical?.Value.ToString());
            WriteInt32(cellOutput, 13, ToInt32(cell.TableCellProperties?.LeftMargin));
            WriteInt32(cellOutput, 14, ToInt32(cell.TableCellProperties?.RightMargin));
            WriteInt32(cellOutput, 15, ToInt32(cell.TableCellProperties?.TopMargin));
            WriteInt32(cellOutput, 16, ToInt32(cell.TableCellProperties?.BottomMargin));
            WriteString(cellOutput, 17, AttributeValue(cell.TableCellProperties, "anchor"));
            WriteBoolValue(cellOutput, 18, cell.TableCellProperties?.AnchorCenter?.Value);
            WriteString(cellOutput, 19, cell.TableCellProperties?.HorizontalOverflow?.Value.ToString());
        });
    }

    private static byte[]? WriteTableProperties(A.Table table)
    {
        var properties = table.TableProperties;
        if (properties is null)
        {
            return null;
        }

        var fill = FillFromDirectProperties(properties);
        return Message(output =>
        {
            if (fill is not null)
            {
                WriteMessage(output, 1, fill);
            }

            WriteBoolValue(output, 2, properties.RightToLeft?.Value);
            WriteBoolValue(output, 3, properties.FirstRow?.Value);
            WriteBoolValue(output, 4, properties.FirstColumn?.Value);
            WriteBoolValue(output, 5, properties.LastRow?.Value);
            WriteBoolValue(output, 6, properties.LastColumn?.Value);
            WriteBoolValue(output, 7, properties.BandRow?.Value);
            WriteBoolValue(output, 8, properties.BandColumn?.Value);
            WriteString(output, 9, properties.GetFirstChild<A.TableStyleId>()?.Text);
            foreach (var effect in ExtractEffects(properties))
            {
                WriteMessage(output, 10, effect);
            }
        });
    }

    private static byte[]? WriteTableCellLines(A.TableCellProperties? properties)
    {
        if (properties is null)
        {
            return null;
        }

        var top = TableLineFromProperties(properties.GetFirstChild<A.TopBorderLineProperties>());
        var right = TableLineFromProperties(properties.GetFirstChild<A.RightBorderLineProperties>());
        var bottom = TableLineFromProperties(properties.GetFirstChild<A.BottomBorderLineProperties>());
        var left = TableLineFromProperties(properties.GetFirstChild<A.LeftBorderLineProperties>());
        var diagonalDown = TableLineFromProperties(properties.GetFirstChild<A.BottomLeftToTopRightBorderLineProperties>());
        var diagonalUp = TableLineFromProperties(properties.GetFirstChild<A.TopLeftToBottomRightBorderLineProperties>());
        if (top is null && right is null && bottom is null && left is null && diagonalDown is null && diagonalUp is null)
        {
            return null;
        }

        return Message(output =>
        {
            if (top is not null) WriteMessage(output, 1, WriteTableCellLine(top));
            if (right is not null) WriteMessage(output, 2, WriteTableCellLine(right));
            if (bottom is not null) WriteMessage(output, 3, WriteTableCellLine(bottom));
            if (left is not null) WriteMessage(output, 4, WriteTableCellLine(left));
            if (diagonalDown is not null) WriteMessage(output, 5, WriteTableCellLine(diagonalDown));
            if (diagonalUp is not null) WriteMessage(output, 6, WriteTableCellLine(diagonalUp));
        });
    }

    private static byte[] WriteTableCellLine(OpenXmlElement line)
    {
        return WriteLine(
            line,
            suppressStyle: line.GetFirstChild<A.PresetDash>() is null,
            suppressDetails: true);
    }

    private static OpenXmlElement? TableLineFromProperties(OpenXmlElement? properties)
    {
        if (properties is null || properties.GetFirstChild<A.NoFill>() is not null)
        {
            return null;
        }

        return properties.GetFirstChild<A.SolidFill>() is not null || LineWidth(properties) is not null ? properties : null;
    }

    private static string ParagraphText(A.Paragraph paragraph)
    {
        return TextNormalization.Clean(string.Concat(paragraph.Descendants<A.Text>().Select(text => text.Text)));
    }

    private static string ResolveChartId(OpenXmlPartContainer partContainer, C.ChartReference chartReference)
    {
        var relationshipId = chartReference.Id?.Value;
        if (string.IsNullOrEmpty(relationshipId))
        {
            return "";
        }

        return partContainer.GetPartById(relationshipId) is ChartPart chartPart
            ? chartPart.Uri.OriginalString
            : relationshipId;
    }

    private static byte[] WriteChartReference(string chartId) =>
        PptxChartProtoWriter.WriteReference(chartId);

    private static byte[] WriteChart(ChartPart chartPart) =>
        PptxChartProtoWriter.WriteChart(chartPart, WriteFill);

    private static byte[] WriteConnector(P.ConnectionShape connectionShape, A.Outline? line)
    {
        var connectorProperties =
            connectionShape.NonVisualConnectionShapeProperties?.NonVisualConnectorShapeDrawingProperties;

        return Message(output =>
        {
            var startConnection = connectorProperties?.StartConnection;
            var endConnection = connectorProperties?.EndConnection;
            WriteString(output, 1, startConnection?.Id?.Value.ToString());
            WriteInt32(output, 2, ToInt32(startConnection?.Index));
            WriteString(output, 3, endConnection?.Id?.Value.ToString());
            WriteInt32(output, 4, ToInt32(endConnection?.Index));
            var lineStyle = WriteConnectorLineStyle(line);
            if (lineStyle is not null)
            {
                WriteMessage(output, 5, lineStyle);
            }
        });
    }

    private static byte[]? WriteConnectorLineStyle(A.Outline? line)
    {
        if (line is null)
        {
            return null;
        }

        return Message(output =>
        {
            WriteInt32(output, 5, ConnectorLineCap(line.CapType?.Value.ToString(), AttributeValue(line, "cap") ?? ""));
            WriteInt32(output, 6, ConnectorLineJoin(line));
            var head = WriteConnectorLineEnd(line.GetFirstChild<A.HeadEnd>() ?? FirstChildByLocalName(line, "headEnd"));
            if (head is not null)
            {
                WriteMessage(output, 7, head);
            }

            var tail = WriteConnectorLineEnd(line.GetFirstChild<A.TailEnd>() ?? FirstChildByLocalName(line, "tailEnd"));
            if (tail is not null)
            {
                WriteMessage(output, 8, tail);
            }
        });
    }

    private static byte[]? WriteConnectorLineEnd(OpenXmlElement? end)
    {
        if (end is null)
        {
            return null;
        }

        return WriteConnectorLineEnd(
            AttributeValue(end, "type"),
            AttributeValue(end, "w"),
            AttributeValue(end, "len"));
    }

    private static byte[]? WriteConnectorLineEnd(string? type, string? width, string? length)
    {
        var mappedType = ConnectorLineEndType(type);
        var mappedWidth = ConnectorLineEndSize(width);
        var mappedLength = ConnectorLineEndSize(length);
        if (mappedType == 0 && mappedWidth == 0 && mappedLength == 0)
        {
            return null;
        }

        return Message(output =>
        {
            WriteInt32(output, 1, mappedType);
            WriteInt32(output, 2, mappedWidth);
            WriteInt32(output, 3, mappedLength);
        });
    }

    private static int ConnectorLineCap(string? typedValue, string rawValue)
    {
        var value = string.IsNullOrWhiteSpace(rawValue) ? typedValue : rawValue;
        return value?.ToLowerInvariant() switch
        {
            "flat" => ConnectorLineCapFlat,
            "rnd" or "round" => ConnectorLineCapRound,
            "sq" or "square" => ConnectorLineCapSquare,
            _ => 0,
        };
    }

    private static OpenXmlElement? FirstChildByLocalName(OpenXmlElement? element, string localName)
    {
        return element?.ChildElements.FirstOrDefault(child => string.Equals(child.LocalName, localName, StringComparison.Ordinal));
    }

    private static int ConnectorLineJoin(OpenXmlElement line)
    {
        if (line.GetFirstChild<A.Round>() is not null) return ConnectorLineJoinRound;
        if (line.GetFirstChild<A.Bevel>() is not null) return ConnectorLineJoinBevel;
        if (line.GetFirstChild<A.Miter>() is not null) return ConnectorLineJoinMiter;
        return 0;
    }

    private static int ConnectorLineEndType(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            "none" => ConnectorLineEndNone,
            "triangle" => ConnectorLineEndTriangle,
            "stealth" => ConnectorLineEndStealth,
            "diamond" => ConnectorLineEndDiamond,
            "oval" => ConnectorLineEndOval,
            "arrow" => ConnectorLineEndArrow,
            _ => 0,
        };
    }

    private static int ConnectorLineEndSize(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            "sm" or "small" => ConnectorLineEndSmall,
            "med" or "medium" => ConnectorLineEndMedium,
            "lg" or "large" => ConnectorLineEndLarge,
            _ => 0,
        };
    }

    private static IEnumerable<byte[]> ExtractEffects(OpenXmlElement? element)
    {
        if (element is null)
        {
            yield break;
        }

        foreach (var shadow in element.Descendants<A.OuterShadow>())
        {
            yield return WriteShadowEffect(shadow);
        }

        foreach (var glow in element.Descendants<A.Glow>())
        {
            yield return WriteGlowEffect(glow);
        }

        foreach (var reflection in element.Descendants<A.Reflection>())
        {
            yield return WriteReflectionEffect(reflection);
        }

        foreach (var softEdges in element.Descendants<A.SoftEdge>())
        {
            yield return WriteSoftEdgesEffect(softEdges);
        }
    }

    private static byte[] WriteShadowEffect(A.OuterShadow shadow)
    {
        return Message(output =>
        {
            WriteInt32(output, 1, EffectTypeShadow);
            WriteMessage(output, 2, Message(shadowOutput =>
            {
                var color = ColorFromElement(shadow);
                if (color is not null)
                {
                    WriteMessage(shadowOutput, 2, WriteColor(color));
                }

                WriteInt32(shadowOutput, 3, ToInt32(shadow.BlurRadius));
                WriteInt32(shadowOutput, 4, ToInt32(shadow.Distance));
                WriteInt32(shadowOutput, 5, shadow.Direction?.Value);
                WriteString(shadowOutput, 6, EnumText(shadow.Alignment));
                WriteBoolValue(shadowOutput, 7, shadow.RotateWithShape?.Value);
            }));
        });
    }

    private static byte[] WriteGlowEffect(A.Glow glow)
    {
        return Message(output =>
        {
            WriteInt32(output, 1, EffectTypeGlow);
            WriteMessage(output, 3, Message(glowOutput =>
            {
                var color = ColorFromElement(glow);
                if (color is not null)
                {
                    WriteMessage(glowOutput, 1, WriteColor(color));
                }

                WriteInt64(glowOutput, 2, ToLong(glow.Radius));
            }));
        });
    }

    private static byte[] WriteReflectionEffect(A.Reflection reflection)
    {
        return Message(output =>
        {
            WriteInt32(output, 1, EffectTypeReflection);
            WriteMessage(output, 4, Message(reflectionOutput =>
            {
                WriteInt64(reflectionOutput, 1, ToLong(reflection.BlurRadius));
                WriteInt32(reflectionOutput, 2, reflection.StartOpacity?.Value);
                WriteInt32(reflectionOutput, 3, reflection.StartPosition?.Value);
                WriteInt32(reflectionOutput, 4, reflection.EndAlpha?.Value);
                WriteInt32(reflectionOutput, 5, reflection.EndPosition?.Value);
                WriteInt64(reflectionOutput, 6, ToLong(reflection.Distance));
                WriteInt32(reflectionOutput, 7, reflection.Direction?.Value);
                WriteInt32(reflectionOutput, 8, reflection.FadeDirection?.Value);
                WriteInt32(reflectionOutput, 9, reflection.HorizontalRatio?.Value);
                WriteInt32(reflectionOutput, 10, reflection.VerticalRatio?.Value);
                WriteInt32(reflectionOutput, 11, reflection.HorizontalSkew?.Value);
                WriteInt32(reflectionOutput, 12, reflection.VerticalSkew?.Value);
                WriteString(reflectionOutput, 13, reflection.Alignment?.Value.ToString());
                WriteBoolValue(reflectionOutput, 14, reflection.RotateWithShape?.Value);
            }));
        });
    }

    private static byte[] WriteSoftEdgesEffect(A.SoftEdge softEdges)
    {
        return Message(output =>
        {
            WriteInt32(output, 1, EffectTypeSoftEdges);
            WriteMessage(output, 5, Message(softEdgesOutput =>
            {
                WriteInt64(softEdgesOutput, 1, ToLong(softEdges.Radius));
            }));
        });
    }

    private static bool IsSmartArt(P.GraphicFrame graphicFrame)
    {
        return graphicFrame.Descendants<A.GraphicData>()
            .Any(data => data.Uri?.Value?.Contains("/diagram", StringComparison.OrdinalIgnoreCase) == true);
    }

    private static IEnumerable<byte[]> ExtractSmartArtChildren(OpenXmlPartContainer partContainer, P.GraphicFrame graphicFrame)
    {
        var relationshipIds = graphicFrame.Descendants<A.GraphicData>()
            .Where(data => data.Uri?.Value?.Contains("/diagram", StringComparison.OrdinalIgnoreCase) == true)
            .SelectMany(data => data.Descendants())
            .SelectMany(element => element.GetAttributes())
            .Where(attribute => string.Equals(attribute.LocalName, "dm", StringComparison.OrdinalIgnoreCase))
            .Select(attribute => attribute.Value)
            .Where(value => !string.IsNullOrEmpty(value))
            .Select(value => value!);
        var childIndex = 0;
        foreach (var relationshipId in relationshipIds)
        {
            if (partContainer.GetPartById(relationshipId) is not OpenXmlPart diagramPart)
            {
                continue;
            }

            foreach (var paragraph in diagramPart.RootElement?.Descendants<A.Paragraph>() ?? Enumerable.Empty<A.Paragraph>())
            {
                var paragraphProto = Message(output =>
                {
                    WriteMessage(output, 1, Message(runOutput =>
                    {
                        WriteString(runOutput, 1, ParagraphText(paragraph));
                        WriteString(runOutput, 4, $"smart-art-run-{childIndex:x8}");
                    }));
                    WriteString(output, 9, $"smart-art-paragraph-{childIndex:x8}");
                });

                yield return Message(output =>
                {
                    WriteMessage(output, 6, paragraphProto);
                    WriteString(output, 10, $"SmartArt Text {childIndex + 1}");
                    WriteInt32(output, 11, ElementTypeText);
                    WriteString(output, 27, $"smart-art-{childIndex:x8}");
                });
                childIndex++;
            }
        }
    }

    private static A.SolidFill? SolidFillFromBackground(P.Background? background)
    {
        return background?.BackgroundProperties?.GetFirstChild<A.SolidFill>() ??
            background?.BackgroundProperties?.Descendants<A.SolidFill>().FirstOrDefault();
    }

    private static A.SolidFill? SolidFillFromProperties(OpenXmlElement? properties)
    {
        return properties?.GetFirstChild<A.SolidFill>();
    }

    private static byte[]? FillFromDirectProperties(OpenXmlElement? properties)
    {
        var solidFill = SolidFillFromProperties(properties);
        if (solidFill is not null)
        {
            return WriteFill(solidFill);
        }

        var gradientFill = properties?.GetFirstChild<A.GradientFill>();
        return gradientFill is null ? null : WriteGradientFill(gradientFill);
    }

    private static byte[]? FillFromShapeProperties(OpenXmlPartContainer partContainer, OpenXmlElement? properties)
    {
        if (properties?.GetFirstChild<A.BlipFill>() is { } blipFill)
        {
            var relationshipId = blipFill.Blip?.Embed?.Value;
            if (!string.IsNullOrEmpty(relationshipId) && partContainer.GetPartById(relationshipId) is ImagePart imagePart)
            {
                return WriteImageFill(
                    relationshipId,
                    imagePart.Uri.OriginalString,
                    blipFill.SourceRectangle,
                    blipFill.GetFirstChild<A.Stretch>()?.GetFirstChild<A.FillRectangle>());
            }
        }

        return FillFromDirectProperties(properties);
    }

    private static A.Outline? OutlineFromProperties(OpenXmlElement? properties)
    {
        var outline = properties?.GetFirstChild<A.Outline>();
        if (outline is null)
        {
            return null;
        }

        return outline.GetFirstChild<A.SolidFill>() is not null || outline.GetFirstChild<A.NoFill>() is not null || outline.Width is not null ? outline : null;
    }

    private static ColorValue? ColorFromSolidFill(A.SolidFill fill)
    {
        return ColorFromElement(fill);
    }

    private static ColorValue? ColorFromElement(OpenXmlElement element)
    {
        var rgb = element.GetFirstChild<A.RgbColorModelHex>() ?? element.Descendants<A.RgbColorModelHex>().FirstOrDefault();
        if (rgb?.Val?.Value is { Length: > 0 } value)
        {
            return ColorValueFromElement(ColorTypeRgb, value, rgb, LastColor: null);
        }

        var scheme = element.GetFirstChild<A.SchemeColor>() ?? element.Descendants<A.SchemeColor>().FirstOrDefault();
        if (scheme?.Val?.Value is not null)
        {
            return ColorValueFromElement(ColorTypeScheme, EnumText(scheme.Val), scheme, LastColor: null);
        }

        var system = element.GetFirstChild<A.SystemColor>() ?? element.Descendants<A.SystemColor>().FirstOrDefault();
        if (system?.Val?.Value is not null)
        {
            return ColorValueFromElement(
                ColorTypeSystem,
                EnumText(system.Val),
                system,
                system.LastColor?.Value);
        }

        return null;
    }

    private static ColorValue ColorValueFromElement(int type, string value, OpenXmlElement element, string? LastColor)
    {
        return new ColorValue(
            type,
            value,
            LastColor,
            TintFrom(element),
            ShadeFrom(element),
            LuminanceModulationFrom(element),
            LuminanceOffsetFrom(element),
            SaturationModulationFrom(element),
            AlphaFrom(element));
    }

    private static byte[] WriteColor(ColorValue color)
    {
        return Message(output =>
        {
            WriteInt32(output, 1, color.Type);
            WriteString(output, 2, color.Value);
            if (color.HasTransform)
            {
                WriteMessage(output, 3, Message(transformOutput =>
                {
                    WriteInt32(transformOutput, 1, color.Tint);
                    WriteInt32(transformOutput, 2, color.Shade);
                    WriteInt32(transformOutput, 3, color.LuminanceModulation);
                    WriteInt32(transformOutput, 4, color.LuminanceOffset);
                    WriteInt32(transformOutput, 5, color.SaturationModulation);
                    if (color.Alpha is not null)
                    {
                        WriteInt32Always(transformOutput, 6, color.Alpha.Value);
                    }
                }));
            }

            WriteString(output, 4, color.LastColor);
        });
    }

    private static int? AlphaFrom(OpenXmlElement element)
    {
        return element.GetFirstChild<A.Alpha>()?.Val?.Value;
    }

    private static int? TintFrom(OpenXmlElement element)
    {
        return element.GetFirstChild<A.Tint>()?.Val?.Value;
    }

    private static int? ShadeFrom(OpenXmlElement element)
    {
        return element.GetFirstChild<A.Shade>()?.Val?.Value;
    }

    private static int? LuminanceModulationFrom(OpenXmlElement element)
    {
        return element.GetFirstChild<A.LuminanceModulation>()?.Val?.Value;
    }

    private static int? LuminanceOffsetFrom(OpenXmlElement element)
    {
        return element.GetFirstChild<A.LuminanceOffset>()?.Val?.Value;
    }

    private static int? SaturationModulationFrom(OpenXmlElement element)
    {
        return element.GetFirstChild<A.SaturationModulation>()?.Val?.Value;
    }

    private static string NormalizeImageContentType(string contentType)
    {
        return contentType;
    }

    private static string PreservePresentationText(string? value)
    {
        return value ?? "";
    }

    private static string? AttributeValue(OpenXmlElement? element, string localName)
    {
        return element?.GetAttributes()
            .FirstOrDefault(attribute => string.Equals(attribute.LocalName, localName, StringComparison.Ordinal))
            .Value;
    }

    private static long? TransformValue(OpenXmlElement? transform, string childLocalName, string attributeLocalName)
    {
        var value = transform?.ChildElements
            .FirstOrDefault(child => string.Equals(child.LocalName, childLocalName, StringComparison.Ordinal));
        return ToLong(AttributeValue(value, attributeLocalName));
    }

    private static string? CreationId(OpenXmlElement? element)
    {
        return element?.Descendants()
            .FirstOrDefault(child => string.Equals(child.LocalName, "creationId", StringComparison.Ordinal)) is { } creationId
            ? AttributeValue(creationId, "id") ?? AttributeValue(creationId, "val")
            : null;
    }

    private static bool IsTextNamedShape(P.NonVisualDrawingProperties? nonVisual)
    {
        return nonVisual?.Name?.Value is { } name &&
            name.StartsWith("Text ", StringComparison.Ordinal);
    }

    private static int? IntAttribute(OpenXmlElement? element, string localName)
    {
        return int.TryParse(AttributeValue(element, localName), out var value) ? value : null;
    }

    private static int? SpacingBefore(OpenXmlElement element)
    {
        var spacing = element.ChildElements.FirstOrDefault(child => string.Equals(child.LocalName, "spcBef", StringComparison.Ordinal));
        if (spacing is null)
        {
            return null;
        }

        var points = spacing.ChildElements.FirstOrDefault(child => string.Equals(child.LocalName, "spcPts", StringComparison.Ordinal));
        if (IntAttribute(points, "val") is { } pointValue)
        {
            return pointValue;
        }

        return 0;
    }

    private static int? SpacingAfter(OpenXmlElement element)
    {
        var spacing = element.ChildElements.FirstOrDefault(child => string.Equals(child.LocalName, "spcAft", StringComparison.Ordinal));
        if (spacing is null)
        {
            return null;
        }

        var points = spacing.ChildElements.FirstOrDefault(child => string.Equals(child.LocalName, "spcPts", StringComparison.Ordinal));
        if (IntAttribute(points, "val") is { } pointValue)
        {
            return pointValue;
        }

        return 0;
    }

    private static bool? BoolAttribute(OpenXmlElement? element, string localName)
    {
        return AttributeValue(element, localName) switch
        {
            "1" => true,
            "true" => true,
            "0" => false,
            "false" => false,
            _ => null,
        };
    }

    private static int GeometryCode(A.PresetGeometry? geometry)
    {
        var value = PresetGeometryName(geometry)?.ToLowerInvariant();
        return value switch
        {
            "rect" => 5,
            "roundrect" => 26,
            "ellipse" => 35,
            "line" => 1,
            "straightconnector1" => 96,
            "bentconnector2" => 97,
            "bentconnector3" => 98,
            "bentconnector4" => 99,
            "bentconnector5" => 100,
            "curvedconnector2" => 101,
            "curvedconnector3" => 102,
            "curvedconnector4" => 103,
            "rttriangle" => 4,
            "triangle" => 3,
            "trapezoid" => 8,
            "pentagon" => 10,
            "round2samerect" => 28,
            "round2diagrect" => 29,
            "snip2samerect" => 32,
            "snip2diagrect" => 33,
            "diamond" => 6,
            "parallelogram" => 7,
            "homeplate" => 37,
            "chevron" => 38,
            "star5" => 17,
            "star6" => 18,
            "star8" => 20,
            "star32" => 25,
            "hexagon" => 11,
            "blockarc" => 41,
            "donut" => 42,
            "mathplus" => 176,
            "pie" => 40,
            "leftarrow" => 45,
            "rightarrow" => 44,
            "uparrow" => 46,
            "downarrow" => 47,
            "stripedrightarrow" => 48,
            "curvedleftarrow" => 201,
            "curvedrightarrow" => 202,
            "curveduparrow" => 203,
            "curveddownarrow" => 204,
            "uturnarrow" => 205,
            "bentuparrow" => 50,
            "leftrightarrow" => 51,
            "updownarrow" => 52,
            "leftrightarrowcallout" => 60,
            "quadarrowcallout" => 62,
            "bentarrow" => 63,
            "can" => 74,
            "lightningbolt" => 75,
            "cube" => 73,
            "heart" => 76,
            "star4" => 215,
            "chord" => 206,
            "snip1rect" => 207,
            "teardrop" => 208,
            "cloud" => 209,
            "corner" => 210,
            "octagon" => 211,
            "round1rect" => 212,
            "plus" => 176,
            "halfframe" => 213,
            "mathequal" => 177,
            "foldedcorner" => 214,
            "moon" => 78,
            "frame" => 84,
            "diagstripe" => 87,
            "arc" => 89,
            "leftbracket" => 90,
            "rightbracket" => 91,
            "leftbrace" => 92,
            "rightbrace" => 93,
            "bracketpair" => 94,
            "bracepair" => 95,
            "wedgerectcallout" => 117,
            "wedgeroundrectcallout" => 118,
            "wedgeellipsecallout" => 119,
            "cloudcallout" => 120,
            "flowchartprocess" => 132,
            "flowchartdecision" => 133,
            "flowchartinputoutput" => 134,
            "flowchartpredefinedprocess" => 135,
            "flowchartinternalstorage" => 136,
            "flowchartdocument" => 137,
            "flowchartmultidocument" => 138,
            "flowchartterminator" => 139,
            "flowchartpreparation" => 140,
            "flowchartmanualinput" => 141,
            "flowchartmanualoperation" => 142,
            "flowchartconnector" => 143,
            "flowchartpunchedcard" => 144,
            "flowchartpunchedtape" => 145,
            "flowchartsummingjunction" => 146,
            "flowchartor" => 147,
            "flowchartcollate" => 148,
            "flowchartsort" => 149,
            "flowchartextract" => 150,
            "flowchartmerge" => 151,
            "flowchartofflinestorage" => 152,
            "flowchartonlinestorage" => 153,
            "flowchartmagnetictape" => 154,
            "flowchartmagneticdisk" => 155,
            "flowchartmagneticdrum" => 156,
            "flowchartdisplay" => 157,
            "flowchartdelay" => 158,
            "flowchartalternateprocess" => 159,
            "flowchartoffpageconnector" => 160,
            _ => 5,
        };
    }

    private readonly record struct BoundingBox(
        long? X,
        long? Y,
        long? Width,
        long? Height,
        int? Rotation,
        bool? HorizontalFlip,
        bool? VerticalFlip)
    {
        public static BoundingBox From(A.Transform2D transform, GroupTransformContext? groupTransform)
        {
            return From(
                TransformValue(transform, "off", "x"),
                TransformValue(transform, "off", "y"),
                TransformValue(transform, "ext", "cx"),
                TransformValue(transform, "ext", "cy"),
                transform.Rotation?.Value,
                transform.HorizontalFlip?.Value,
                transform.VerticalFlip?.Value,
                groupTransform);
        }

        public static BoundingBox From(P.Transform transform, GroupTransformContext? groupTransform)
        {
            return From(
                TransformValue(transform, "off", "x"),
                TransformValue(transform, "off", "y"),
                TransformValue(transform, "ext", "cx"),
                TransformValue(transform, "ext", "cy"),
                null,
                null,
                null,
                groupTransform);
        }

        public static BoundingBox FromRaw(
            long? x,
            long? y,
            long? width,
            long? height,
            int? rotation,
            bool? horizontalFlip,
            bool? verticalFlip,
            GroupTransformContext? groupTransform)
        {
            return From(x, y, width, height, rotation, horizontalFlip, verticalFlip, groupTransform);
        }

        private static BoundingBox From(
            long? x,
            long? y,
            long? width,
            long? height,
            int? rotation,
            bool? horizontalFlip,
            bool? verticalFlip,
            GroupTransformContext? groupTransform)
        {
            if (groupTransform is null)
            {
                return new BoundingBox(x, y, width, height, rotation, horizontalFlip, verticalFlip);
            }

            return new BoundingBox(
                groupTransform.TransformX(x),
                groupTransform.TransformY(y),
                groupTransform.TransformWidth(width),
                groupTransform.TransformHeight(height),
                rotation,
                CombineFlip(groupTransform.EffectiveHorizontalFlip, horizontalFlip),
                CombineFlip(groupTransform.EffectiveVerticalFlip, verticalFlip));
        }
    }

    private sealed record GroupTransformContext(
        double X,
        double Y,
        double ChildX,
        double ChildY,
        double ScaleX,
        double ScaleY,
        bool? HorizontalFlip,
        bool? VerticalFlip,
        bool? EffectiveHorizontalFlip,
        bool? EffectiveVerticalFlip)
    {
        public static GroupTransformContext From(A.TransformGroup transform, GroupTransformContext? parent)
        {
            var rawX = TransformValue(transform, "off", "x") ?? 0;
            var rawY = TransformValue(transform, "off", "y") ?? 0;
            var rawWidth = TransformValue(transform, "ext", "cx") ?? TransformValue(transform, "chExt", "cx") ?? 0;
            var rawHeight = TransformValue(transform, "ext", "cy") ?? TransformValue(transform, "chExt", "cy") ?? 0;
            var childX = TransformValue(transform, "chOff", "x") ?? 0;
            var childY = TransformValue(transform, "chOff", "y") ?? 0;
            var childWidth = TransformValue(transform, "chExt", "cx") ?? rawWidth;
            var childHeight = TransformValue(transform, "chExt", "cy") ?? rawHeight;
            var x = parent?.TransformX(rawX) ?? rawX;
            var y = parent?.TransformY(rawY) ?? rawY;
            var width = parent?.TransformWidth(rawWidth) ?? rawWidth;
            var height = parent?.TransformHeight(rawHeight) ?? rawHeight;
            var scaleX = childWidth == 0 ? parent?.ScaleX ?? 1 : (double)width / childWidth;
            var scaleY = childHeight == 0 ? parent?.ScaleY ?? 1 : (double)height / childHeight;
            var horizontalFlip = transform.HorizontalFlip?.Value;
            var verticalFlip = transform.VerticalFlip?.Value;
            var effectiveHorizontalFlip = CombineFlip(parent?.EffectiveHorizontalFlip, horizontalFlip);
            var effectiveVerticalFlip = CombineFlip(parent?.EffectiveVerticalFlip, verticalFlip);

            return new GroupTransformContext(
                x,
                y,
                childX,
                childY,
                scaleX,
                scaleY,
                horizontalFlip,
                verticalFlip,
                effectiveHorizontalFlip,
                effectiveVerticalFlip);
        }

        public static GroupTransformContext FromRaw(
            long rawX,
            long rawY,
            long rawWidth,
            long rawHeight,
            long childX,
            long childY,
            long childWidth,
            long childHeight,
            bool horizontalFlip,
            bool verticalFlip,
            GroupTransformContext? parent)
        {
            var x = parent?.TransformX(rawX) ?? rawX;
            var y = parent?.TransformY(rawY) ?? rawY;
            var width = parent?.TransformWidth(rawWidth) ?? rawWidth;
            var height = parent?.TransformHeight(rawHeight) ?? rawHeight;
            var scaleX = childWidth == 0 ? parent?.ScaleX ?? 1 : (double)width / childWidth;
            var scaleY = childHeight == 0 ? parent?.ScaleY ?? 1 : (double)height / childHeight;
            return new GroupTransformContext(
                x,
                y,
                childX,
                childY,
                scaleX,
                scaleY,
                horizontalFlip,
                verticalFlip,
                CombineFlip(parent?.EffectiveHorizontalFlip, horizontalFlip),
                CombineFlip(parent?.EffectiveVerticalFlip, verticalFlip));
        }

        public long? TransformX(long? value) => value is null ? null : RoundEmu(TransformXCoordinate(value.Value));

        public long? TransformY(long? value) => value is null ? null : RoundEmu(TransformYCoordinate(value.Value));

        public long? TransformWidth(long? value) => value is null ? null : RoundEmu(value.Value * ScaleX);

        public long? TransformHeight(long? value) => value is null ? null : RoundEmu(value.Value * ScaleY);

        public long TransformCeilingX(double value) => (long)Math.Ceiling(TransformXCoordinate(value));

        public long TransformCeilingY(double value) => (long)Math.Ceiling(TransformYCoordinate(value));

        public long TransformRoundX(double value) => RoundEmu(TransformXCoordinate(value));

        public long TransformRoundY(double value) => RoundEmu(TransformYCoordinate(value));

        public long TransformFloorX(double value) => (long)Math.Floor(TransformXCoordinate(value));

        public long TransformFloorY(double value) => (long)Math.Floor(TransformYCoordinate(value));

        public long TransformFloorWidth(double value) => (long)Math.Floor(value * ScaleX);

        public long TransformFloorHeight(double value) => (long)Math.Floor(value * ScaleY);

        private double TransformXCoordinate(double value)
        {
            if (HorizontalFlip == true)
            {
                return EffectiveHorizontalFlip == true
                    ? X - (value + ChildX) * ScaleX
                    : X + (value + ChildX) * ScaleX;
            }

            return EffectiveHorizontalFlip == true
                ? X - (value - ChildX) * ScaleX
                : X + (value - ChildX) * ScaleX;
        }

        private double TransformYCoordinate(double value)
        {
            if (VerticalFlip == true)
            {
                return EffectiveVerticalFlip == true
                    ? Y - (value + ChildY) * ScaleY
                    : Y + (value + ChildY) * ScaleY;
            }

            return EffectiveVerticalFlip == true
                ? Y - (value - ChildY) * ScaleY
                : Y + (value - ChildY) * ScaleY;
        }

        private static long RoundEmu(double value)
        {
            return (long)Math.Round(value, MidpointRounding.AwayFromZero);
        }
    }

    private static string? PresetGeometryName(A.PresetGeometry? geometry)
    {
        var preset = geometry?.Preset;
        return string.IsNullOrWhiteSpace(preset?.InnerText)
            ? preset?.Value.ToString()
            : preset.InnerText;
    }

    private static bool? CombineFlip(bool? parentFlip, bool? elementFlip)
    {
        if (parentFlip is null || parentFlip == false)
        {
            return elementFlip;
        }

        return !(elementFlip ?? false);
    }

    private static long? ToLong(Int64Value? value)
    {
        return value?.Value;
    }

    private static long? ToLong(Int32Value? value)
    {
        return value?.Value;
    }

    private static long? ToLong(string? value)
    {
        return long.TryParse(value, out var parsed) ? parsed : null;
    }

    private static long RoundEmu(double value)
    {
        return (long)Math.Round(value, MidpointRounding.AwayFromZero);
    }

    private static int? ToInt32(Int64Value? value)
    {
        if (value is null)
        {
            return null;
        }

        return (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, value.Value));
    }

    private static int? ToInt32(Int32Value? value)
    {
        return value?.Value;
    }

    private static int? ToInt32(UInt32Value? value)
    {
        if (value is null)
        {
            return null;
        }

        return value.Value > int.MaxValue ? int.MaxValue : (int)value.Value;
    }

    private static uint? ToUInt32(Int32Value? value)
    {
        return value is null ? null : (uint)Math.Max(0, value.Value);
    }

    private static byte[] Message(Action<CodedOutputStream> write)
    {
        using var stream = new MemoryStream();
        var output = new CodedOutputStream(stream);
        write(output);
        output.Flush();
        return stream.ToArray();
    }

    private static void WriteMessage(CodedOutputStream output, int fieldNumber, byte[] bytes)
    {
        if (bytes.Length == 0)
        {
            return;
        }

        output.WriteTag(fieldNumber, WireFormat.WireType.LengthDelimited);
        output.WriteBytes(ByteString.CopyFrom(bytes));
    }

    private static void WriteMessageAlways(CodedOutputStream output, int fieldNumber, byte[] bytes)
    {
        output.WriteTag(fieldNumber, WireFormat.WireType.LengthDelimited);
        output.WriteBytes(ByteString.CopyFrom(bytes));
    }

    private static void WriteString(CodedOutputStream output, int fieldNumber, string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        output.WriteTag(fieldNumber, WireFormat.WireType.LengthDelimited);
        output.WriteString(value);
    }

    private static void WriteStringAlways(CodedOutputStream output, int fieldNumber, string value)
    {
        output.WriteTag(fieldNumber, WireFormat.WireType.LengthDelimited);
        output.WriteString(value);
    }

    private static void WriteBytes(CodedOutputStream output, int fieldNumber, byte[] value)
    {
        if (value.Length == 0)
        {
            return;
        }

        output.WriteTag(fieldNumber, WireFormat.WireType.LengthDelimited);
        output.WriteBytes(ByteString.CopyFrom(value));
    }

    private static void WriteInt32(CodedOutputStream output, int fieldNumber, int? value)
    {
        if (value is null or 0)
        {
            return;
        }

        output.WriteTag(fieldNumber, WireFormat.WireType.Varint);
        output.WriteInt32(value.Value);
    }

    private static void WriteInt32Always(CodedOutputStream output, int fieldNumber, int value)
    {
        output.WriteTag(fieldNumber, WireFormat.WireType.Varint);
        output.WriteInt32(value);
    }

    private static void WriteInt64(CodedOutputStream output, int fieldNumber, long? value)
    {
        if (value is null or 0)
        {
            return;
        }

        output.WriteTag(fieldNumber, WireFormat.WireType.Varint);
        output.WriteInt64(value.Value);
    }

    private static void WriteUInt32Always(CodedOutputStream output, int fieldNumber, uint value)
    {
        output.WriteTag(fieldNumber, WireFormat.WireType.Varint);
        output.WriteUInt32(value);
    }

    private static void WriteInt64Always(CodedOutputStream output, int fieldNumber, long? value)
    {
        if (value is null)
        {
            return;
        }

        output.WriteTag(fieldNumber, WireFormat.WireType.Varint);
        output.WriteInt64(value.Value);
    }

    private static void WriteBool(CodedOutputStream output, int fieldNumber, bool? value)
    {
        if (value != true)
        {
            return;
        }

        output.WriteTag(fieldNumber, WireFormat.WireType.Varint);
        output.WriteBool(true);
    }

    private static void WriteBoolValue(CodedOutputStream output, int fieldNumber, bool? value)
    {
        if (value is null)
        {
            return;
        }

        output.WriteTag(fieldNumber, WireFormat.WireType.Varint);
        output.WriteBool(value.Value);
    }

    private static void WriteDouble(CodedOutputStream output, int fieldNumber, double? value)
    {
        if (value is null or 0)
        {
            return;
        }

        output.WriteTag(fieldNumber, WireFormat.WireType.Fixed64);
        output.WriteDouble(value.Value);
    }

    private sealed class RawTransformIndex
    {
        private readonly Dictionary<string, BoundingBox> boxes;

        private RawTransformIndex(Dictionary<string, BoundingBox> boxes)
        {
            this.boxes = boxes;
        }

        public bool TryGet(string id, out BoundingBox box) => boxes.TryGetValue(id, out box);

        public static IReadOnlyDictionary<string, RawTransformIndex> FromPackage(byte[] packageBytes)
        {
            var indexes = new Dictionary<string, RawTransformIndex>(StringComparer.Ordinal);

            try
            {
                using var stream = new MemoryStream(packageBytes, writable: false);
                using var archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Read);
                foreach (var entry in archive.Entries)
                {
                    if (!IsPresentationXmlPart(entry.FullName))
                    {
                        continue;
                    }

                    using var entryStream = entry.Open();
                    using var reader = new StreamReader(entryStream);
                    var index = FromXml(reader.ReadToEnd());
                    if (index is not null)
                    {
                        indexes[$"/{entry.FullName}"] = index;
                    }
                }
            }
            catch
            {
                return indexes;
            }

            return indexes;
        }

        private static bool IsPresentationXmlPart(string name)
        {
            return name.StartsWith("ppt/slides/", StringComparison.Ordinal) ||
                name.StartsWith("ppt/slideLayouts/", StringComparison.Ordinal) ||
                name.StartsWith("ppt/slideMasters/", StringComparison.Ordinal);
        }

        private static RawTransformIndex? FromXml(string xml)
        {
            if (string.IsNullOrWhiteSpace(xml))
            {
                return null;
            }

            var root = XElement.Parse(xml);
            var shapeTree = root.Descendants().FirstOrDefault(element => LocalNameEquals(element, "spTree"));
            if (shapeTree is null)
            {
                return null;
            }

            var boxes = new Dictionary<string, BoundingBox>(StringComparer.Ordinal);
            foreach (var child in shapeTree.Elements())
            {
                VisitElement(child, null, boxes);
            }

            return boxes.Count == 0 ? null : new RawTransformIndex(boxes);
        }

        private static void VisitElement(XElement element, GroupTransformContext? groupTransform, Dictionary<string, BoundingBox> boxes)
        {
            if (LocalNameEquals(element, "grpSp"))
            {
                var transform = element.Elements().FirstOrDefault(child => LocalNameEquals(child, "grpSpPr"))
                    ?.Elements()
                    .FirstOrDefault(child => LocalNameEquals(child, "xfrm"));
                var childTransform = transform is null ? groupTransform : RawGroupTransform(transform, groupTransform);
                foreach (var child in element.Elements())
                {
                    if (!LocalNameEquals(child, "nvGrpSpPr") && !LocalNameEquals(child, "grpSpPr"))
                    {
                        VisitElement(child, childTransform, boxes);
                    }
                }

                return;
            }

            if (!IsPositionedElement(element))
            {
                return;
            }

            var id = NonVisualPropertiesElement(element)?.Descendants()
                .FirstOrDefault(descendant => LocalNameEquals(descendant, "cNvPr"))
                ?.Attributes()
                .FirstOrDefault(attribute => LocalNameEquals(attribute, "id"))
                ?.Value;
            var transformElement = DirectTransformElement(element);
            if (string.IsNullOrEmpty(id) || transformElement is null)
            {
                return;
            }

            boxes[id] = RawElementBoundingBox(transformElement, groupTransform);
        }

        private static bool IsPositionedElement(XElement element)
        {
            return LocalNameEquals(element, "sp") ||
                LocalNameEquals(element, "pic") ||
                LocalNameEquals(element, "cxnSp") ||
                LocalNameEquals(element, "graphicFrame");
        }

        private static XElement? DirectTransformElement(XElement element)
        {
            if (LocalNameEquals(element, "graphicFrame"))
            {
                return element.Elements().FirstOrDefault(child => LocalNameEquals(child, "xfrm"));
            }

            return ShapePropertiesElement(element)
                ?.Elements()
                .FirstOrDefault(child => LocalNameEquals(child, "xfrm"));
        }

        private static XElement? ShapePropertiesElement(XElement element)
        {
            return element.Elements().FirstOrDefault(child => LocalNameEquals(child, "spPr"));
        }

        private static XElement? NonVisualPropertiesElement(XElement element)
        {
            return element.Elements().FirstOrDefault(child =>
                LocalNameEquals(child, "nvSpPr") ||
                LocalNameEquals(child, "nvPicPr") ||
                LocalNameEquals(child, "nvCxnSpPr") ||
                LocalNameEquals(child, "nvGraphicFramePr"));
        }

        private static BoundingBox RawElementBoundingBox(
            XElement transform,
            GroupTransformContext? groupTransform)
        {
            var x = RawTransformValue(transform, "off", "x");
            var y = RawTransformValue(transform, "off", "y");
            var width = RawTransformValue(transform, "ext", "cx");
            var height = RawTransformValue(transform, "ext", "cy");
            var rotation = ToInt32(RawAttributeValue(transform, "rot"));
            var horizontalFlip = ToBool(RawAttributeValue(transform, "flipH"));
            var verticalFlip = ToBool(RawAttributeValue(transform, "flipV"));

            if (groupTransform is not null)
            {
                return new BoundingBox(
                    x is null ? null : groupTransform.TransformRoundX(x.Value),
                    y is null ? null : groupTransform.TransformRoundY(y.Value),
                    width is null ? null : groupTransform.TransformFloorWidth(width.Value),
                    height is null ? null : groupTransform.TransformFloorHeight(height.Value),
                    rotation,
                    CombineFlip(groupTransform.EffectiveHorizontalFlip, horizontalFlip),
                    CombineFlip(groupTransform.EffectiveVerticalFlip, verticalFlip));
            }

            return BoundingBox.FromRaw(x, y, width, height, rotation, horizontalFlip, verticalFlip, groupTransform);
        }

        private static GroupTransformContext RawGroupTransform(XElement transform, GroupTransformContext? parent)
        {
            var rawX = RawTransformValue(transform, "off", "x") ?? 0;
            var rawY = RawTransformValue(transform, "off", "y") ?? 0;
            var rawWidth = RawTransformValue(transform, "ext", "cx") ?? RawTransformValue(transform, "chExt", "cx") ?? 0;
            var rawHeight = RawTransformValue(transform, "ext", "cy") ?? RawTransformValue(transform, "chExt", "cy") ?? 0;
            var childX = RawTransformValue(transform, "chOff", "x") ?? 0;
            var childY = RawTransformValue(transform, "chOff", "y") ?? 0;
            var childWidth = RawTransformValue(transform, "chExt", "cx") ?? rawWidth;
            var childHeight = RawTransformValue(transform, "chExt", "cy") ?? rawHeight;
            return GroupTransformContext.FromRaw(
                rawX,
                rawY,
                rawWidth,
                rawHeight,
                childX,
                childY,
                childWidth,
                childHeight,
                ToBool(RawAttributeValue(transform, "flipH")) == true,
                ToBool(RawAttributeValue(transform, "flipV")) == true,
                parent);
        }

        private static long? RawTransformValue(XElement transform, string childName, string attributeName)
        {
            var value = transform.Elements()
                .FirstOrDefault(child => LocalNameEquals(child, childName))
                ?.Attributes()
                .FirstOrDefault(attribute => LocalNameEquals(attribute, attributeName))
                ?.Value;
            return ToLong(value);
        }

        private static string? RawAttributeValue(XElement element, string attributeName)
        {
            return element.Attributes()
                .FirstOrDefault(attribute => LocalNameEquals(attribute, attributeName))
                ?.Value;
        }

        private static bool LocalNameEquals(XObject value, string localName)
        {
            return value switch
            {
                XElement element => string.Equals(element.Name.LocalName, localName, StringComparison.Ordinal),
                XAttribute attribute => string.Equals(attribute.Name.LocalName, localName, StringComparison.Ordinal),
                _ => false,
            };
        }

        private static bool? ToBool(string? value)
        {
            return value switch
            {
                "1" or "true" => true,
                "0" or "false" => false,
                _ => null,
            };
        }

        private static int? ToInt32(string? value)
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
        }
    }

    private sealed record ColorValue(
        int Type,
        string Value,
        string? LastColor,
        int? Tint,
        int? Shade,
        int? LuminanceModulation,
        int? LuminanceOffset,
        int? SaturationModulation,
        int? Alpha)
    {
        public bool HasTransform =>
            Tint is not null ||
            Shade is not null ||
            LuminanceModulation is not null ||
            LuminanceOffset is not null ||
            SaturationModulation is not null ||
            Alpha is not null;
    }

}
