using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

var icoPath = Path.GetFullPath(args.Length > 0
    ? args[0]
    : Path.Combine("src", "CoffeeManiaVPN", "Assets", "app.ico"));
var pngPath = Path.GetFullPath(args.Length > 1
    ? args[1]
    : Path.Combine("src", "CoffeeManiaVPN", "Assets", "logo-black.png"));

Directory.CreateDirectory(Path.GetDirectoryName(icoPath)!);

var drawing = CreateLogoDrawing();
var sizes = new[] { 16, 32, 48, 64, 128, 256 };
var pngImages = sizes.Select(size => RenderPng(drawing, size)).ToArray();
var iconImages = sizes.Select(size => RenderPng(drawing, size, rotationDegrees: 45)).ToArray();
File.WriteAllBytes(pngPath, pngImages[^1]);

using var stream = File.Create(icoPath);
using var writer = new BinaryWriter(stream);

writer.Write((ushort)0);
writer.Write((ushort)1);
writer.Write((ushort)sizes.Length);

var offset = 6 + sizes.Length * 16;
foreach (var (size, png) in sizes.Zip(iconImages))
{
    writer.Write(size == 256 ? (byte)0 : (byte)size);
    writer.Write(size == 256 ? (byte)0 : (byte)size);
    writer.Write((byte)0);
    writer.Write((byte)0);
    writer.Write((ushort)1);
    writer.Write((ushort)32);
    writer.Write((uint)png.Length);
    writer.Write((uint)offset);
    offset += png.Length;
}

foreach (var png in iconImages)
    writer.Write(png);

Console.WriteLine($"Created {icoPath}");
Console.WriteLine($"Created {pngPath}");
return 0;

static DrawingGroup CreateLogoDrawing()
{
    var transform = new TransformGroup();
    transform.Children.Add(new ScaleTransform(1.35, 1.35));
    transform.Children.Add(new TranslateTransform(-930, -420));

    var bean1 = new GeometryDrawing(
        Brushes.Black,
        null,
        Geometry.Parse("M737.673,328.231C738.494,328.056 739.334,328.427 739.757,329.152C739.955,329.463 740.106,329.722 740.106,329.722C740.106,329.722 745.206,338.581 739.429,352.782C737.079,358.559 736.492,366.083 738.435,371.679C738.697,372.426 738.482,373.258 737.89,373.784C737.298,374.31 736.447,374.426 735.735,374.077C730.192,371.375 722.028,365.058 722.021,352C722.015,340.226 728.812,330.279 737.673,328.231Z"));

    var bean2Geometry = new GeometryGroup
    {
        Transform = new MatrixTransform(-1, 0, 0, -1, 1483.03, 703.293),
        Children = { Geometry.Parse("M737.609,328.246C738.465,328.06 739.344,328.446 739.785,329.203C739.97,329.49 740.106,329.722 740.106,329.722C740.106,329.722 745.206,338.581 739.429,352.782C737.1,358.507 736.503,365.948 738.383,371.527C738.646,372.304 738.415,373.164 737.796,373.703C737.177,374.243 736.294,374.356 735.56,373.989C730.02,371.241 722.028,364.92 722.021,352C722.016,340.255 728.779,330.328 737.609,328.246Z") }
    };

    var bean2 = new GeometryDrawing(Brushes.Black, null, bean2Geometry);

    var group = new DrawingGroup { Transform = transform };
    group.Children.Add(bean1);
    group.Children.Add(bean2);
    group.Freeze();
    return group;
}

static byte[] RenderPng(DrawingGroup drawing, int size, double rotationDegrees = 0)
{
    var visual = new DrawingVisual();
    using (var context = visual.RenderOpen())
    {
        var bounds = drawing.Bounds;
        var scale = Math.Min(size / bounds.Width, size / bounds.Height);
        var offsetX = (size - bounds.Width * scale) / 2 - bounds.X * scale;
        var offsetY = (size - bounds.Height * scale) / 2 - bounds.Y * scale;
        context.PushTransform(new TranslateTransform(offsetX, offsetY));
        context.PushTransform(new ScaleTransform(scale, scale));

        if (rotationDegrees != 0)
        {
            var centerX = bounds.X + bounds.Width / 2;
            var centerY = bounds.Y + bounds.Height / 2;
            context.PushTransform(new RotateTransform(rotationDegrees, centerX, centerY));
        }

        context.DrawDrawing(drawing);

        if (rotationDegrees != 0)
            context.Pop();

        context.Pop();
        context.Pop();
    }

    var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
    bitmap.Render(visual);

    var encoder = new PngBitmapEncoder();
    encoder.Frames.Add(BitmapFrame.Create(bitmap));
    using var memory = new MemoryStream();
    encoder.Save(memory);
    return memory.ToArray();
}
