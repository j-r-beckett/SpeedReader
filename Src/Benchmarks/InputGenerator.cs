// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

using System.Runtime.CompilerServices;
using Resources;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Benchmarks;

public static class InputGenerator
{
    private static readonly Random _random = new(0);
    private static readonly Font _font = Fonts.GetFont(fontSize: 24f);

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    public static async IAsyncEnumerable<Image<Rgb24>> GenerateImages(int inputWidth, int inputHeight, int density, [EnumeratorCancellation] CancellationToken cancellationToken)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var image = new Image<Rgb24>(inputWidth, inputHeight, Color.White);
            for (var i = 0; i < density; i++)
            {
                var text = GetRandomWord();
                var x = _random.Next(0, inputWidth);
                var y = _random.Next(0, inputHeight);
                var angleDegrees = 0;
                image.Mutate(ctx => ctx
                    .SetDrawingTransform(Matrix3x2Extensions.CreateRotationDegrees(-angleDegrees, new PointF(x, y)))
                    .DrawText(text, _font, Color.Black, new PointF(x, y)));
            }

            yield return image;
        }
    }

    public static string GetRandomWord()
    {
        return Words()[_random.Next(0, Words().Count)];

        static List<string> Words() =>
        [
            "apple", "mountain", "bridge", "keyboard", "planet", "window", "forest", "ocean", "guitar", "pencil",
            "coffee", "dragon", "market", "thunder", "laptop", "garden", "mirror", "bottle", "fabric", "castle",
            "doctor", "engine", "flower", "hammer", "island", "jacket", "kitchen", "ladder", "monkey", "needle",
            "orange", "pillow", "rabbit", "silver", "temple", "umbrella", "violin", "wallet", "yellow", "zipper",
            "artist", "balloon", "camera", "danger", "energy", "friend", "ground", "hunger", "insect", "jungle",
            "kangaroo", "letter", "magnet", "nature", "office", "player", "reason", "season", "travel", "unique",
            "valley", "winter", "zigzag", "answer", "basket", "circle", "damage", "eleven", "finger", "global",
            "helmet", "impact", "jasper", "kernel", "legacy", "mobile", "normal", "oxygen", "pepper", "quarry",
            "record", "socket", "timber", "upload", "vector", "whisper", "xenon", "yogurt", "zebra", "anchor",
            "bronze", "copper", "desert", "empire", "falcon", "glacier", "harbor", "invent", "jigsaw", "lotus"
        ];
    }
}
