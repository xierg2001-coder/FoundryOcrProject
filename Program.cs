using System;
using System.IO;
using System.Threading.Tasks;

namespace FoundryOcr.Cli;

public static class Program {
    [STAThread]
    public static async Task<int> Main() {
        try {
            using var inputStream = Console.OpenStandardInput();
            using var memoryStream = new MemoryStream();
            await inputStream.CopyToAsync(memoryStream);
            var imageBytes = memoryStream.ToArray();

            if (imageBytes.Length == 0) {
                Console.Error.WriteLine("Error: No image data was received via standard input.");
                return 1;
            }
            
            var jsonResult = await OcrService.RecognizeAsJsonFromBytesAsync(imageBytes, indented: true);
            Console.WriteLine(jsonResult);
            return 0;
        }
        catch (Exception ex) {
            Console.Error.WriteLine($"An unexpected error occurred: {ex.Message}");
            return 1;
        }
    }
}
