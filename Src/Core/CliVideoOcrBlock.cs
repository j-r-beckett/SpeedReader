// // Copyright (c) 2025 j-r-beckett
// // Licensed under the Apache License, Version 2.0
//
// using System.Diagnostics.Metrics;
// using System.IO.Pipelines;
// using System.Threading.Tasks.Dataflow;
// using Ocr;
// using Ocr.Blocks;
// using Ocr.Visualization;
// using Resources;
// using SixLabors.ImageSharp;
// using SixLabors.ImageSharp.PixelFormats;
// using Video;
//
// namespace Core;
//
// public class CliVideoOcrBlock
// {
//     public ISourceBlock<JsonOcrResult> ResultsBlock
//     {
//         get; init;
//     }
//     public Task Completion
//     {
//         get; init;
//     }
//
//     public CliVideoOcrBlock(string videoFilePath, int sampleRate)
//     {
//         var modelProvider = new ModelProvider();
//         var dbnetSession = modelProvider.GetSession(Model.DbNet18, ModelPrecision.INT8);
//         var svtrSession = modelProvider.GetSession(Model.SVTRv2);
//         var ocrBlock = new OcrBlock(dbnetSession, svtrSession, new OcrConfiguration(), new Meter("SpeedReader.Ocr"));
//
//         var fileStream = new FileStream(videoFilePath, FileMode.Open, FileAccess.Read);
//         var videoOcrBlock = new VideoOcrBlock(ocrBlock, fileStream, sampleRate);
//
//         ITargetBlock<Image<Rgb24>>? ffmpegEncoderBlock = null;
//         PipeReader? encodedOutput = null;
//         Task<Stream>? videoOutputTask = null;
//
//         var counter = 0;
//         var transformer = new TransformBlock<(Image<Rgb24> Img, JsonOcrResult Result, VizData? VizData), JsonOcrResult>(async item =>
//         {
//             // Render the visualization with OCR results
//             var visualizedImage = PngRenderer.Render(item.Img, item.Result, item.VizData);
//
//             if (ffmpegEncoderBlock == null)
//             {
//                 ffmpegEncoderBlock = new FfmpegEncoderBlockCreator().CreateFfmpegEncoderBlock(visualizedImage.Width,
//                     visualizedImage.Height, 30, out encodedOutput, CancellationToken.None);
//
//                 // Start consuming the encoded output concurrently
//                 videoOutputTask = ReadEncodedOutputAsync(encodedOutput);
//             }
//
//             await visualizedImage.SaveAsPngAsync($"img_{counter++}.png");
//
//             await ffmpegEncoderBlock.SendAsync(visualizedImage);
//
//             return item.Result;
//         });
//
//         videoOcrBlock.Source.LinkTo(transformer, new DataflowLinkOptions { PropagateCompletion = true });
//
//         ResultsBlock = transformer;
//         Completion = transformer.Completion.ContinueWith(async _ =>
//         {
//             if (ffmpegEncoderBlock != null)
//             {
//                 ffmpegEncoderBlock.Complete();
//                 await ffmpegEncoderBlock.Completion;
//
//                 // Wait for video output and save to file
//                 if (videoOutputTask != null)
//                 {
//                     var videoStream = await videoOutputTask;
//                     await using var outFile = File.Create("video_viz.webm");
//                     videoStream.Position = 0;
//                     await videoStream.CopyToAsync(outFile);
//                     videoStream.Dispose();
//                 }
//             }
//
//             fileStream.Dispose();
//             modelProvider.Dispose();
//         }).Unwrap();
//     }
//
//     private static async Task<Stream> ReadEncodedOutputAsync(PipeReader reader)
//     {
//         var outputStream = new MemoryStream();
//
//         try
//         {
//             while (true)
//             {
//                 var result = await reader.ReadAsync();
//                 var buffer = result.Buffer;
//
//                 if (buffer.Length > 0)
//                 {
//                     // Copy buffer to memory stream
//                     foreach (var segment in buffer)
//                     {
//                         await outputStream.WriteAsync(segment);
//                     }
//                 }
//
//                 reader.AdvanceTo(buffer.End);
//
//                 if (result.IsCompleted)
//                 {
//                     break;
//                 }
//             }
//         }
//         finally
//         {
//             await reader.CompleteAsync();
//         }
//
//         return outputStream;
//     }
// }
