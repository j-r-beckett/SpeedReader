using System.Diagnostics.Metrics;
using System.Text.Json;
using System.Threading.Tasks.Dataflow;
using Ocr;
using Ocr.Blocks;
using Ocr.Visualization;
using Resources;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Core;

public class CliOcrBlock
{
    public readonly ITargetBlock<string> Target;
    public readonly Task Completion;

    public CliOcrBlock(VizMode vizMode, Meter meter)
    {
        // Create the pipeline components
        var fileReaderBlock = CreateFileReaderBlock();
        var splitBlock = new SplitBlock<(Image<Rgb24> Image, string Filename), (Image<Rgb24> Image, VizBuilder VizBuilder), string>(
            input => ((input.Image, VizBuilder.Create(vizMode, input.Image)), input.Filename)
        );
        
        // Create OCR block
        var modelProvider = new ModelProvider();
        var dbnetSession = modelProvider.GetSession(Model.DbNet18, ModelPrecision.INT8);
        var svtrSession = modelProvider.GetSession(Model.SVTRv2);
        var ocrBlock = OcrBlock.Create(dbnetSession, svtrSession, new OcrConfiguration(), meter);
        
        var mergeBlock = new MergeBlock<(Image<Rgb24> Image, OcrResult Result, VizBuilder VizBuilder), string, (Image<Rgb24> Image, OcrResult Result, VizBuilder VizBuilder, string Filename)>(
            (ocrResult, filename) => (ocrResult.Image, ocrResult.Result, ocrResult.VizBuilder, filename)
        );
        
        var outputEmitterBlock = CreateOutputEmitterBlock(vizMode);

        // Link the pipeline
        fileReaderBlock.LinkTo(splitBlock.Target, new DataflowLinkOptions { PropagateCompletion = true });
        splitBlock.LeftSource.LinkTo(ocrBlock, new DataflowLinkOptions { PropagateCompletion = true });
        splitBlock.RightSource.LinkTo(mergeBlock.RightTarget, new DataflowLinkOptions { PropagateCompletion = true });
        ocrBlock.LinkTo(mergeBlock.LeftTarget, new DataflowLinkOptions { PropagateCompletion = true });
        mergeBlock.Source.LinkTo(outputEmitterBlock, new DataflowLinkOptions { PropagateCompletion = true });

        Target = fileReaderBlock;
        Completion = outputEmitterBlock.Completion.ContinueWith(_ => 
        {
            Console.WriteLine("\n]");
            modelProvider.Dispose();
        });
    }

    private static TransformBlock<string, (Image<Rgb24> Image, string Filename)> CreateFileReaderBlock()
    {
        return new TransformBlock<string, (Image<Rgb24> Image, string Filename)>(async filename =>
        {
            try
            {
                var fileInfo = new FileInfo(filename);
                if (!fileInfo.Exists)
                {
                    Console.Error.WriteLine($"Error: Input file '{filename}' not found.");
                    Environment.Exit(1);
                }

                var image = await Image.LoadAsync<Rgb24>(filename);
                return (image, filename);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error loading '{filename}': {ex.Message}");
                Environment.Exit(1);
                throw; // Never reached
            }
        }, new ExecutionDataflowBlockOptions { BoundedCapacity = 1 });
    }

    private static ActionBlock<(Image<Rgb24> Image, OcrResult Result, VizBuilder VizBuilder, string Filename)> CreateOutputEmitterBlock(VizMode vizMode)
    {
        int pageNumber = 0;
        
        return new ActionBlock<(Image<Rgb24> Image, OcrResult Result, VizBuilder VizBuilder, string Filename)>(async output =>
        {
            var (image, ocrResult, vizBuilder, filename) = output;
            
            try
            {
                // Update page number
                var resultWithPageNumber = ocrResult with { PageNumber = pageNumber };

                // Generate viz file path if applicable
                string? vizFilePath = null;
                if (vizMode != VizMode.None)
                {
                    var inputDir = Path.GetDirectoryName(filename) ?? ".";
                    var inputName = Path.GetFileNameWithoutExtension(filename);
                    var inputExt = Path.GetExtension(filename);
                    vizFilePath = Path.Combine(inputDir, $"{inputName}_viz{inputExt}");
                }

                // Create enriched result with additional CLI metadata
                var jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                
                // Serialize OcrResult to JsonElement
                var ocrElement = JsonSerializer.SerializeToElement(resultWithPageNumber, jsonOptions);
                var ocrDict = ocrElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
                
                // Add CLI-specific fields
                ocrDict["sourceFile"] = JsonSerializer.SerializeToElement(filename, jsonOptions);
                if (vizFilePath != null)
                {
                    ocrDict["vizFile"] = JsonSerializer.SerializeToElement(vizFilePath, jsonOptions);
                }
                
                var json = JsonSerializer.Serialize(ocrDict, jsonOptions);
                
                // Add proper indentation and comma formatting for array
                var indentedJson = string.Join('\n', json.Split('\n').Select(line => "  " + line));
                
                if (pageNumber == 0)
                {
                    Console.WriteLine("[");
                    Console.Write(indentedJson);
                }
                else
                {
                    Console.WriteLine(",");
                    Console.Write(indentedJson);
                }
                
                pageNumber++;

                // Generate visualization if configured
                if (vizFilePath != null)
                {
                    var outputImage = vizBuilder.Render();
                    await outputImage.SaveAsync(vizFilePath);
                }
            }
            finally
            {
                // Dispose image after processing
                image.Dispose();
            }
        }, new ExecutionDataflowBlockOptions { BoundedCapacity = 1 });
    }
}