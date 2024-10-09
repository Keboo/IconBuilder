using System.CommandLine;

using BluwolfIcons;

using SkiaSharp;

using SKSvg = SkiaSharp.Extended.Svg.SKSvg;

namespace IconBuilder;

public sealed class Program
{
    private static Task<int> Main(string[] args)
    {
        CliConfiguration configuration = GetConfiguration();
        return configuration.InvokeAsync(args);
    }

    public static CliConfiguration GetConfiguration()
    {
        CliOption<FileInfo> svgFile = new("--svg", "-i")
        {
            Required = true,
            Description = "The SVG file to convert to an icon"
        };
        svgFile.AcceptExistingOnly();

        CliArgument<FileInfo> outputFile = new("icon")
        {
            Description = "The output .ico file to write the icon to"
        };
        outputFile.AcceptLegalFileNamesOnly();

        CliRootCommand rootCommand = new("A simple application for generating .ico files from SVGs")
        {
            svgFile,
            outputFile
        };
        rootCommand.SetAction((ParseResult parseResult) =>
        {
            //Check for windows-only
            if (!OperatingSystem.IsWindows())
            {
                Console.Error.WriteLine("This tool currently only works on Windows");
                return;
            }
            FileInfo inputFile = parseResult.CommandResult.GetValue(svgFile)!;
            FileInfo output = parseResult.CommandResult.GetValue(outputFile)!;

            using var outputStream = output.OpenWrite();
            CreateIco(inputFile, outputStream);
        });
        return new CliConfiguration(rootCommand);
    }

    private static void CreateIco(FileInfo svgFile, Stream outputStream)
    {
        var icon = new Icon();
        SKSvg svg = LoadSvg(svgFile);

        //TODO: Make this size list configurable
        foreach (var size in new[] { 256, 128, 64, 48, 32, 24, 16 })
        {
            IIconImage image = RasterizeImage(svg, size);
            icon.Images.Add(image);
        }

        icon.Save(outputStream);
    }

    private static SKSvg LoadSvg(FileInfo svgFile)
    {
        var svg = new SKSvg();
        using var stream = svgFile.OpenRead();
        svg.Load(stream);
        return svg;
    }

    private static RawIconImage RasterizeImage(SKSvg svg, int size)
    {
        //Based on: https://gist.github.com/punker76/67bd048ff403c1c73737905183f819a9
        var imageInfo = new SKImageInfo(size, size);
        using var surface = SKSurface.Create(imageInfo);
        using var canvas = surface.Canvas;
        // calculate the scaling need to fit to screen
        float scaleX = size / svg.Picture.CullRect.Width;
        float scaleY = size / svg.Picture.CullRect.Height;
        var matrix = SKMatrix.MakeScale(scaleX, scaleY);

        canvas.Clear(SKColors.Transparent);
        canvas.DrawPicture(svg.Picture, ref matrix);
        canvas.Flush();

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return new RawIconImage(size, imageInfo.BitsPerPixel, data.ToArray());
    }

    private class RawIconImage(int size, int bitsPerPixel, byte[] date) : IIconImage
    {
        public int Width { get; } = size;
        public int Height { get; } = size;
        public int BitsPerPixel { get; } = bitsPerPixel;
        public byte[] GetData() => date;
    }
}