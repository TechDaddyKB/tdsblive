using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace ExtensionSuite.Overlays;

public sealed record ValidatedAsset(byte[] Bytes, bool Sanitized);
public static class AssetValidation
{
    public const int MaximumBytes = 20 * 1024 * 1024;
    private static readonly HashSet<string> Elements = ["svg", "g", "path", "rect", "circle", "ellipse", "line", "polyline", "polygon", "title", "desc"];
    private static readonly HashSet<string> Attributes = ["width", "height", "viewBox", "d", "x", "y", "x1", "y1", "x2", "y2", "cx", "cy", "r", "rx", "ry", "points", "fill", "stroke", "stroke-width", "opacity", "fill-opacity", "stroke-opacity", "transform", "fill-rule", "stroke-linecap", "stroke-linejoin"];
    public static string NormalizeMime(string mime) => mime.Split(';')[0].Trim().ToLowerInvariant() switch
    {
        "image/jpg" => "image/jpeg", "audio/x-wav" => "audio/wav", "audio/mp3" => "audio/mpeg",
        "application/font-woff" => "font/woff", "application/x-font-ttf" => "font/ttf", "application/x-font-otf" or "application/vnd.ms-opentype" => "font/otf",
        var value => value
    };
    public static ValidatedAsset Validate(byte[] bytes, string mime)
    {
        if (bytes.Length is < 8 or > MaximumBytes) throw new ArgumentException("Invalid asset size.");
        if (mime == "image/svg+xml") return SanitizeSvg(bytes);
        var valid = mime switch
        {
            "image/png" => bytes.Length >= 33 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) && Ascii(bytes, 12, 4) == "IHDR",
            "image/jpeg" => bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255 && bytes[^2] == 255 && bytes[^1] == 217,
            "image/gif" => Ascii(bytes, 0, 6) is "GIF87a" or "GIF89a",
            "image/webp" => Riff(bytes, "WEBP"),
            "audio/wav" => Riff(bytes, "WAVE"),
            "audio/mpeg" => Ascii(bytes, 0, 3) == "ID3" || bytes[0] == 255 && (bytes[1] & 0xe0) == 0xe0,
            "audio/ogg" => Ascii(bytes, 0, 4) == "OggS",
            "video/webm" => bytes[0] == 0x1a && bytes[1] == 0x45 && bytes[2] == 0xdf && bytes[3] == 0xa3,
            "video/mp4" => bytes.Length >= 16 && Ascii(bytes, 4, 4) == "ftyp",
            "font/woff" => Ascii(bytes, 0, 4) == "wOFF",
            "font/woff2" => Ascii(bytes, 0, 4) == "wOF2",
            "font/ttf" => bytes[0] == 0 && bytes[1] == 1 && bytes[2] == 0 && bytes[3] == 0,
            "font/otf" => Ascii(bytes, 0, 4) == "OTTO",
            _ => false
        };
        if (!valid) throw new ArgumentException("Unsupported or mismatched asset MIME.");
        return new(bytes, false);
    }
    private static bool Riff(byte[] bytes, string format) => bytes.Length >= 16 && Ascii(bytes, 0, 4) == "RIFF" && Ascii(bytes, 8, 4) == format;
    private static string Ascii(byte[] bytes, int start, int count) => Encoding.ASCII.GetString(bytes, start, count);
    private static ValidatedAsset SanitizeSvg(byte[] bytes)
    {
        if (bytes.Length > 1024 * 1024) throw new ArgumentException("SVG exceeds size limit.");
        using var reader = XmlReader.Create(new MemoryStream(bytes), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 1024 * 1024 });
        XDocument document;
        try { document = XDocument.Load(reader); }
        catch (XmlException) { throw new ArgumentException("Invalid SVG."); }
        var root = document.Root;
        if (root?.Name != XName.Get("svg", "http://www.w3.org/2000/svg")) throw new ArgumentException("Invalid SVG root.");
        if (root.Descendants().Take(10001).Count() > 10000 || root.Descendants().Any(e => e.Ancestors().Take(33).Count() > 32)) throw new ArgumentException("SVG complexity limit exceeded.");
        foreach (var element in root.Descendants().ToArray())
            if (element.Name.Namespace != root.Name.Namespace || !Elements.Contains(element.Name.LocalName)) element.Remove();
        foreach (var element in root.DescendantsAndSelf())
        {
            foreach (var attribute in element.Attributes().ToArray())
                if (!attribute.IsNamespaceDeclaration && (attribute.Name.NamespaceName.Length != 0 || !Attributes.Contains(attribute.Name.LocalName) ||
                    attribute.Value.Contains("url", StringComparison.OrdinalIgnoreCase) || attribute.Value.Contains(':') || attribute.Value.Length > 32768)) attribute.Remove();
            foreach (var node in element.Nodes().Where(n => n is not XElement && !(n is XText && element.Name.LocalName is "title" or "desc")).ToArray()) node.Remove();
        }
        return new(Encoding.UTF8.GetBytes(root.ToString(SaveOptions.DisableFormatting)), true);
    }
}
