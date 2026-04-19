using System.Text;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;
using ZXing.QrCode.Internal;

namespace VKFoodArea.Web.Helpers;

public static class QrSvgBuilder
{
    public static string BuildSvg(string content, int moduleSize = 8, int quietZoneModules = 4)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("QR content is required.", nameof(content));

        if (moduleSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(moduleSize));

        if (quietZoneModules < 0)
            throw new ArgumentOutOfRangeException(nameof(quietZoneModules));

        var writer = new QRCodeWriter();
        var hints = new Dictionary<EncodeHintType, object>
        {
            [EncodeHintType.CHARACTER_SET] = "UTF-8",
            [EncodeHintType.ERROR_CORRECTION] = ErrorCorrectionLevel.M,
            [EncodeHintType.MARGIN] = quietZoneModules
        };

        var matrix = writer.encode(content, BarcodeFormat.QR_CODE, 1, 1, hints);
        return RenderSvg(matrix, moduleSize);
    }

    private static string RenderSvg(BitMatrix matrix, int moduleSize)
    {
        var canvasSize = matrix.Width * moduleSize;
        var pathBuilder = new StringBuilder(matrix.Width * matrix.Height * 16);

        for (var y = 0; y < matrix.Height; y++)
        {
            var yPos = y * moduleSize;

            for (var x = 0; x < matrix.Width; x++)
            {
                if (!matrix[x, y])
                    continue;

                var xPos = x * moduleSize;
                pathBuilder
                    .Append('M').Append(xPos).Append(' ').Append(yPos)
                    .Append('h').Append(moduleSize)
                    .Append('v').Append(moduleSize)
                    .Append('H').Append(xPos)
                    .Append("z ");
            }
        }

        var svgBuilder = new StringBuilder(pathBuilder.Length + 256);
        svgBuilder
            .Append("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 ")
            .Append(canvasSize)
            .Append(' ')
            .Append(canvasSize)
            .Append("\" role=\"img\" aria-label=\"QR code\" shape-rendering=\"crispEdges\">")
            .Append("<rect width=\"100%\" height=\"100%\" fill=\"#ffffff\"/>")
            .Append("<path fill=\"#111827\" d=\"")
            .Append(pathBuilder)
            .Append("\"/></svg>");

        return svgBuilder.ToString();
    }
}
