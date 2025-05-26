using System.IO;
using Engine;
using Engine.Benchmark;
using Microsoft.Extensions.Logging;

// Setup logger and URL publisher
using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger<FfmpegDecoderBenchmark>();
var outputDir = Path.GetTempPath();
var urlPublisher = new FileSystemUrlPublisher<FfmpegDecoderBenchmark>(outputDir, logger);

var benchmark = new FfmpegDecoderBenchmark(urlPublisher);
await benchmark.RunAsync();
