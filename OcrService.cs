using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AI.Vision.Ocr;
using Microsoft.Windows.AI;
using Microsoft.Graphics.Imaging;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using System.Runtime.InteropServices.WindowsRuntime;

namespace FoundryOcr.Cli;

public sealed class OcrWordDto {
    public string Text { get; set; } = "";
    public double Confidence { get; set; }
    public (double X, double Y) TopLeft { get; set; }
    public (double X, double Y) TopRight { get; set; }
    public (double X, double Y) BottomRight { get; set; }
    public (double X, double Y) BottomLeft { get; set; }
}

public sealed class OcrLineDto {
    public string Text { get; set; } = "";
    public List<OcrWordDto> Words { get; set; } = new();
}

public sealed class OcrResultDto {
    public string FullText { get; set; } = "";
    public List<OcrLineDto> Lines { get; set; } = new();
}

public static class OcrService {
    public static async Task<OcrResultDto> RecognizeAsync(string imagePath) {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            throw new FileNotFoundException("Image not found.", imagePath);

        var recognizer = await EnsureRecognizerReadyAsync();
        using IRandomAccessStream ras = await (await StorageFile.GetFileFromPathAsync(imagePath)).OpenAsync(FileAccessMode.Read);
        var decoder = await BitmapDecoder.CreateAsync(ras);
        var softwareBitmap = await decoder.GetSoftwareBitmapAsync();
        if (softwareBitmap is null)
            throw new InvalidOperationException("Failed to decode image into SoftwareBitmap.");
        return RecognizeFromSoftwareBitmap(recognizer, softwareBitmap);
    }

    public static async Task<string> RecognizeAsJsonAsync(string imagePath, bool indented = false)
        => JsonSerialize(await RecognizeAsync(imagePath), indented);

    public static async Task<OcrResultDto> RecognizeFromBytesAsync(byte[] bytes) {
        if (bytes is null || bytes.Length == 0)
            throw new ArgumentException("No image bytes provided.", nameof(bytes));

        var recognizer = await EnsureRecognizerReadyAsync();
        using var ras = new InMemoryRandomAccessStream();
        await ras.WriteAsync(bytes.AsBuffer());
        ras.Seek(0);
        var decoder = await BitmapDecoder.CreateAsync(ras);
        var softwareBitmap = await decoder.GetSoftwareBitmapAsync();
        if (softwareBitmap is null)
            throw new InvalidOperationException("Invalid image bytes.");
        return RecognizeFromSoftwareBitmap(recognizer, softwareBitmap);
    }

    public static async Task<string> RecognizeAsJsonFromBytesAsync(byte[] bytes, bool indented = false)
        => JsonSerialize(await RecognizeFromBytesAsync(bytes), indented);

    private static async Task<TextRecognizer> EnsureRecognizerReadyAsync() {
        if (TextRecognizer.GetReadyState() == AIFeatureReadyState.EnsureNeeded) {
            var load = await TextRecognizer.EnsureReadyAsync();
            if (load.Status != PackageDeploymentStatus.CompletedSuccess)
                throw new Exception(load.ExtendedError().Message);
        }
        return await TextRecognizer.CreateAsync();
    }

    private static OcrResultDto RecognizeFromSoftwareBitmap(TextRecognizer recognizer, SoftwareBitmap bitmap) {
        ImageBuffer buffer = ImageBuffer.CreateBufferAttachedToBitmap(bitmap);
        RecognizedText recognized = recognizer.RecognizeTextFromImage(buffer);
        var sb = new StringBuilder();
        var result = new OcrResultDto();

        foreach (var line in recognized.Lines) {
            var lineDto = new OcrLineDto { Text = line.Text };
            sb.AppendLine(line.Text);
            foreach (var word in line.Words) {
                var b = word.BoundingBox;
                lineDto.Words.Add(new OcrWordDto {
                    Text = word.Text,
                    Confidence = word.Confidence,
                    TopLeft = (b.TopLeft.X, b.TopLeft.Y),
                    TopRight = (b.TopRight.X, b.TopRight.Y),
                    BottomRight = (b.BottomRight.X, b.BottomRight.Y),
                    BottomLeft = (b.BottomLeft.X, b.BottomLeft.Y)
                });
            }
            result.Lines.Add(lineDto);
        }
        result.FullText = sb.ToString().TrimEnd();
        return result;
    }

    private static string JsonSerialize(OcrResultDto dto, bool indented)
        => JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = indented });
}
