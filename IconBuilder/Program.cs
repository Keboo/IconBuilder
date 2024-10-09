using System.CommandLine;
using System.Drawing;
using System.Drawing.Imaging;

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
            output.Delete();
            using var outputStream = output.OpenWrite();

            var svg = Svg.SvgDocument.Open(inputFile.FullName);
            
            //TODO: Make configurable
            int[] sizes = [256, 128, 64, 48, 32, 24, 16];

            var images = sizes.Select(size => svg.Draw(size, size)).ToArray();
            WriteIcon(outputStream, images);

        });
        return new CliConfiguration(rootCommand);
    }

    private static void WriteIcon(Stream stream, Bitmap[] images)
    {
        using BinaryWriter binaryWriter = new(stream);
        binaryWriter.Write((ushort)0);
        binaryWriter.Write((ushort)1);
        binaryWriter.Write((ushort)images.Length);
        Queue<byte[]> queue = new(images.Length);
        int num = 6 + 16 * images.Length;
        foreach (var image in images)
        {
            binaryWriter.Write((byte)image.Width);
            binaryWriter.Write((byte)image.Height);
            binaryWriter.Write((byte)0);
            binaryWriter.Write((byte)0);
            binaryWriter.Write((ushort)1);
            binaryWriter.Write((ushort)32);
            //TODO: Support bitmap with transparency mask
            using var ms = new MemoryStream();
            image.Save(ms, ImageFormat.Png);
            byte[] data = ms.ToArray();
            binaryWriter.Write((uint)data.Length);
            binaryWriter.Write((uint)num);
            num += data.Length;
            queue.Enqueue(data);
        }

        while (queue.Count > 0)
        {
            binaryWriter.Write(queue.Dequeue());
        }
    }

}