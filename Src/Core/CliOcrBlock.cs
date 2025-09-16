// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

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
    public class Config
    {
        public VizMode VizMode { get; init; } = VizMode.None;
        public bool JsonOutput { get; init; } = false;
        public Meter Meter { get; init; } = new Meter("SpeedReader.Ocr");
    }

    public readonly ITargetBlock<string> Target;
    public readonly Task Completion;

    private readonly Config _config;

    public CliOcrBlock(Config config)
    {
        _config = config;

        // Create the pipeline components
        var fileReaderBlock = CreateFileReaderBlock();
        var vizData = _config.VizMode is VizMode.None or VizMode.Basic ? null : new VizData();
        var splitBlock = new SplitBlock<(Image<Rgb24> Image, string Filename), (Image<Rgb24> Image, VizData? VizData), string>(
            input => ((input.Image, vizData), input.Filename)
        );

        // Create OCR block
        var modelProvider = new ModelProvider();
        var dbnetSession = modelProvider.GetSession(Model.DbNet18, ModelPrecision.INT8);
        var svtrSession = modelProvider.GetSession(Model.SVTRv2);
        var ocrBlock = new OcrBlock(dbnetSession, svtrSession, new OcrConfiguration(), _config.Meter);

        var mergeBlock = new MergeBlock<(Image<Rgb24> Image, OcrResult Result, VizData? VizData), string, (Image<Rgb24> Image, OcrResult Result, VizData? VizData, string Filename)>(
            (ocrResult, filename) => (ocrResult.Image, ocrResult.Result, ocrResult.VizData, filename)
        );

        var outputEmitterBlock = CreateOutputEmitterBlock();

        // Link the pipeline
        fileReaderBlock.LinkTo(splitBlock.Target, new DataflowLinkOptions { PropagateCompletion = true });
        splitBlock.LeftSource.LinkTo(ocrBlock.Block, new DataflowLinkOptions { PropagateCompletion = true });
        splitBlock.RightSource.LinkTo(mergeBlock.RightTarget, new DataflowLinkOptions { PropagateCompletion = true });
        ocrBlock.Block.LinkTo(mergeBlock.LeftTarget, new DataflowLinkOptions { PropagateCompletion = true });
        mergeBlock.Source.LinkTo(outputEmitterBlock, new DataflowLinkOptions { PropagateCompletion = true });

        Target = fileReaderBlock;
        Completion = outputEmitterBlock.Completion.ContinueWith(_ =>
        {
            if (_config.JsonOutput)
            {
                Console.WriteLine("\n]");
            }
            modelProvider.Dispose();
        });
    }

    private TransformBlock<string, (Image<Rgb24> Image, string Filename)> CreateFileReaderBlock() => new TransformBlock<string, (Image<Rgb24> Image, string Filename)>(async filename =>
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

    private ActionBlock<(Image<Rgb24> Image, OcrResult Result, VizData? VizData, string Filename)> CreateOutputEmitterBlock()
    {
        int pageNumber = 0;

        return new ActionBlock<(Image<Rgb24> Image, OcrResult Result, VizData? VizData, string Filename)>(async output =>
        {
            var (image, ocrResult, vizData, filename) = output;

            try
            {
                // Update page number
                var resultWithPageNumber = ocrResult with { PageNumber = pageNumber };

                // Generate viz file path if applicable
                string? vizFilePath = null;
                if (_config.VizMode != VizMode.None)
                {
                    var inputDir = Path.GetDirectoryName(filename) ?? ".";
                    var inputName = Path.GetFileNameWithoutExtension(filename);
                    vizFilePath = Path.Combine(inputDir, $"{inputName}_viz.svg");
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

                if (_config.JsonOutput)
                {
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
                }
                else
                {
                    // Pipe-separated output: filename|line1|line2|line3...
                    var lines = ocrResult.Lines
                        .Select(line => line.Text.Replace('|', ' ').Trim())
                        .Where(line => line != string.Empty);
                    var pipeOutput = string.Join(" | ", new[] { filename.Replace('|', ' ') }.Concat(lines));
                    Console.WriteLine(pipeOutput);
                }

                pageNumber++;

                // Generate visualization if configured
                if (vizFilePath != null)
                {
                    var svg = SvgRenderer.Render(image, ocrResult, vizData);
                    await svg.SaveAsync(vizFilePath);
                }
            }
            finally
            {
                image.Dispose();
            }
        }, new ExecutionDataflowBlockOptions { BoundedCapacity = 1 });
    }
}
