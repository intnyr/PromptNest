using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

string outPath = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0])
    ? args[0]
    : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "src", "PromptNest.App", "Assets", "AppIcon.ico"));

int[] sizes = { 16, 32, 48, 64, 128, 256 };
Color bg = ColorTranslator.FromHtml("#5865F2");
Color bgDark = ColorTranslator.FromHtml("#4550D8");
Color fg = Color.White;

byte[] RenderPng(int size)
{
    using var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
    using var g = Graphics.FromImage(bmp);
    g.SmoothingMode = SmoothingMode.AntiAlias;
    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
    g.Clear(Color.Transparent);

    int radius = Math.Max(2, (int)(size * 0.22));
    var rect = new RectangleF(0, 0, size, size);
    using var path = new GraphicsPath();
    int d = radius * 2;
    path.AddArc(rect.X, rect.Y, d, d, 180, 90);
    path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
    path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
    path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
    path.CloseFigure();

    using (var grad = new LinearGradientBrush(rect, bg, bgDark, LinearGradientMode.Vertical))
    {
        g.FillPath(grad, path);
    }

    if (size >= 48)
    {
        using var hiPath = new GraphicsPath();
        hiPath.AddArc(rect.X, rect.Y, d, d, 180, 90);
        hiPath.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        hiPath.AddLine(rect.Right, rect.Height * 0.5f, rect.X, rect.Height * 0.5f);
        hiPath.CloseFigure();
        using var hiBrush = new SolidBrush(Color.FromArgb(28, 255, 255, 255));
        g.FillPath(hiBrush, hiPath);
    }

    string glyph = "PN";
    float fontSize = size * 0.46f;
    FontFamily? family = null;
    foreach (var name in new[] { "Segoe UI Variable Display Semibold", "Segoe UI Semibold", "Segoe UI", "Arial" })
    {
        try { family = new FontFamily(name); break; } catch { }
    }
    family ??= FontFamily.GenericSansSerif;

    using var font = new Font(family, fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
    using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
    var textRect = new RectangleF(0, -size * 0.04f, size, size);
    using (var fgBrush = new SolidBrush(fg))
    {
        g.DrawString(glyph, font, fgBrush, textRect, sf);
    }

    using var ms = new MemoryStream();
    bmp.Save(ms, ImageFormat.Png);
    return ms.ToArray();
}

var pngs = sizes.ToDictionary(s => s, RenderPng);

using var ico = new MemoryStream();
using var bw = new BinaryWriter(ico);
bw.Write((ushort)0);
bw.Write((ushort)1);
bw.Write((ushort)sizes.Length);

int header = 6 + (16 * sizes.Length);
int offset = header;
foreach (var s in sizes)
{
    var bytes = pngs[s];
    bw.Write((byte)(s >= 256 ? 0 : s));
    bw.Write((byte)(s >= 256 ? 0 : s));
    bw.Write((byte)0);
    bw.Write((byte)0);
    bw.Write((ushort)1);
    bw.Write((ushort)32);
    bw.Write((uint)bytes.Length);
    bw.Write((uint)offset);
    offset += bytes.Length;
}
foreach (var s in sizes) bw.Write(pngs[s]);
bw.Flush();

Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
File.WriteAllBytes(outPath, ico.ToArray());
Console.WriteLine($"Wrote {outPath} ({new FileInfo(outPath).Length / 1024.0:F1} KB)");
