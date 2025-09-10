using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Ocr;
using Ocr.Blocks;

namespace Video.Test;

public class DeduplicatorBlockTests
{
    [Fact]
    public async Task ProcessAsync_FirstResult_ReturnsUnchangedWithOriginalIds()
    {
        await using var bridge = CreateBridge(maxLevDist: 2);

        var words = new List<Word>
        {
            CreateWord("word_0", "hello", 10, 10),
            CreateWord("word_1", "world", 50, 10)
        };
        var ocrResult = new OcrResult { Words = words };

        var result = await await bridge.ProcessAsync(ocrResult, CancellationToken.None, CancellationToken.None);

        Assert.Equal(2, result.Words.Count);
        Assert.Equal("word_0", result.Words[0].Id);
        Assert.Equal("word_1", result.Words[1].Id);
        Assert.Equal("hello", result.Words[0].Text);
        Assert.Equal("world", result.Words[1].Text);
    }

    [Fact]
    public async Task ProcessAsync_ExactMatch_ReusesPreviousIds()
    {
        await using var bridge = CreateBridge(maxLevDist: 2);

        // First result
        var firstWords = new List<Word>
        {
            CreateWord("word_0", "hello", 10, 10),
            CreateWord("word_1", "world", 50, 10)
        };
        var firstResult = new OcrResult { Words = firstWords };
        await (await bridge.ProcessAsync(firstResult, CancellationToken.None, CancellationToken.None));

        // Second result with exact matches
        var secondWords = new List<Word>
        {
            CreateWord("newid1", "hello", 10, 10),
            CreateWord("newid2", "world", 50, 10)
        };
        var secondResult = new OcrResult { Words = secondWords };

        var result = await await bridge.ProcessAsync(secondResult, CancellationToken.None, CancellationToken.None);

        Assert.Equal(2, result.Words.Count);
        Assert.Equal("word_0", result.Words[0].Id); // Should reuse original ID
        Assert.Equal("word_1", result.Words[1].Id); // Should reuse original ID
        Assert.Equal("hello", result.Words[0].Text);
        Assert.Equal("world", result.Words[1].Text);
    }

    [Fact]
    public async Task ProcessAsync_NoMatch_AssignsNewIds()
    {
        await using var bridge = CreateBridge(maxLevDist: 2);

        // First result
        var firstWords = new List<Word>
        {
            CreateWord("word_0", "hello", 10, 10),
            CreateWord("word_1", "world", 50, 10)
        };
        var firstResult = new OcrResult { Words = firstWords };
        await (await bridge.ProcessAsync(firstResult, CancellationToken.None, CancellationToken.None));

        // Second result with completely different words
        var secondWords = new List<Word>
        {
            CreateWord("newid1", "goodbye", 100, 100),
            CreateWord("newid2", "universe", 150, 100)
        };
        var secondResult = new OcrResult { Words = secondWords };

        var result = await await bridge.ProcessAsync(secondResult, CancellationToken.None, CancellationToken.None);

        Assert.Equal(2, result.Words.Count);
        Assert.Equal("word_2", result.Words[0].Id); // New ID, counter starts at 2
        Assert.Equal("word_3", result.Words[1].Id); // New ID, counter increments
        Assert.Equal("goodbye", result.Words[0].Text);
        Assert.Equal("universe", result.Words[1].Text);
    }

    [Fact]
    public async Task ProcessAsync_AllNewWords_AssignsSequentialIds()
    {
        await using var bridge = CreateBridge(maxLevDist: 2);

        // First result with 3 words
        var firstWords = new List<Word>
        {
            CreateWord("word_0", "hello", 10, 10),
            CreateWord("word_1", "world", 50, 10),
            CreateWord("word_2", "test", 90, 10)
        };
        var firstResult = new OcrResult { Words = firstWords };
        await (await bridge.ProcessAsync(firstResult, CancellationToken.None, CancellationToken.None));

        // Second result with all new words
        var secondWords = new List<Word>
        {
            CreateWord("newid1", "alpha", 200, 200),
            CreateWord("newid2", "beta", 250, 200),
            CreateWord("newid3", "gamma", 300, 200),
            CreateWord("newid4", "delta", 350, 200)
        };
        var secondResult = new OcrResult { Words = secondWords };

        var result = await await bridge.ProcessAsync(secondResult, CancellationToken.None, CancellationToken.None);

        Assert.Equal(4, result.Words.Count);
        Assert.Equal("word_3", result.Words[0].Id); // Counter starts at 3
        Assert.Equal("word_4", result.Words[1].Id);
        Assert.Equal("word_5", result.Words[2].Id);
        Assert.Equal("word_6", result.Words[3].Id);
        Assert.Equal("alpha", result.Words[0].Text);
        Assert.Equal("beta", result.Words[1].Text);
        Assert.Equal("gamma", result.Words[2].Text);
        Assert.Equal("delta", result.Words[3].Text);
    }

    [Fact]
    public async Task ProcessAsync_MixedMatches_ReusesMatchingIdsAssignsNewToOthers()
    {
        await using var bridge = CreateBridge(maxLevDist: 2);

        // First result
        var firstWords = new List<Word>
        {
            CreateWord("word_0", "hello", 10, 10),
            CreateWord("word_1", "world", 50, 10)
        };
        var firstResult = new OcrResult { Words = firstWords };
        await bridge.ProcessAsync(firstResult, CancellationToken.None, CancellationToken.None);

        // Second result with mix: one match, one new
        var secondWords = new List<Word>
        {
            CreateWord("newid1", "hello", 10, 10),    // Should match word_0
            CreateWord("newid2", "goodbye", 100, 100) // Should get word_2
        };
        var secondResult = new OcrResult { Words = secondWords };

        var result = await await bridge.ProcessAsync(secondResult, CancellationToken.None, CancellationToken.None);

        Assert.Equal(2, result.Words.Count);
        Assert.Equal("word_0", result.Words[0].Id); // Reused from match
        Assert.Equal("word_2", result.Words[1].Id); // New ID, counter at 2
        Assert.Equal("hello", result.Words[0].Text);
        Assert.Equal("goodbye", result.Words[1].Text);
    }

    [Fact]
    public async Task ProcessAsync_EmptyWords_ReturnsUnchanged()
    {
        await using var bridge = CreateBridge(maxLevDist: 2);

        // First result with words
        var firstWords = new List<Word>
        {
            CreateWord("word_0", "hello", 10, 10)
        };
        var firstResult = new OcrResult { Words = firstWords };
        await await bridge.ProcessAsync(firstResult, CancellationToken.None, CancellationToken.None);

        // Second result with no words
        var emptyResult = new OcrResult { Words = new List<Word>() };

        var result = await await bridge.ProcessAsync(emptyResult, CancellationToken.None, CancellationToken.None);

        Assert.Empty(result.Words);
    }

    [Fact]
    public async Task ProcessAsync_LevenshteinBoundary_RespectsThreshold()
    {
        await using var bridge = CreateBridge(maxLevDist: 2);

        // First result
        var firstWords = new List<Word>
        {
            CreateWord("word_0", "hello", 10, 10)
        };
        var firstResult = new OcrResult { Words = firstWords };
        await await bridge.ProcessAsync(firstResult, CancellationToken.None, CancellationToken.None);

        // Test word at distance 1 - should match
        var withinThresholdWords = new List<Word>
        {
            CreateWord("id1", "hell", 10, 10) // Distance 1 - should match
        };
        var withinThresholdResult = new OcrResult { Words = withinThresholdWords };

        var result1 = await await bridge.ProcessAsync(withinThresholdResult, CancellationToken.None, CancellationToken.None);

        Assert.Single(result1.Words);
        Assert.Equal("word_0", result1.Words[0].Id); // Should reuse ID

        // Test word at distance 3 - should NOT match
        var exceedsThresholdWords = new List<Word>
        {
            CreateWord("id2", "world", 50, 50) // Distance 5 - should NOT match
        };
        var exceedsThresholdResult = new OcrResult { Words = exceedsThresholdWords };

        var result2 = await await bridge.ProcessAsync(exceedsThresholdResult, CancellationToken.None, CancellationToken.None);

        Assert.Single(result2.Words);
        Assert.Equal("word_1", result2.Words[0].Id); // Should get new ID
    }

    [Fact]
    public async Task ProcessAsync_SpatialDistance_InfluencesMatching()
    {
        await using var bridge = CreateBridge(maxLevDist: 2);

        // First result with two identical words at different positions
        var firstWords = new List<Word>
        {
            CreateWord("word_0", "test", 10, 10),   // Close position
            CreateWord("word_1", "test", 1000, 1000) // Far position
        };
        var firstResult = new OcrResult { Words = firstWords };
        await await bridge.ProcessAsync(firstResult, CancellationToken.None, CancellationToken.None);

        // Second result with word close to first position
        var secondWords = new List<Word>
        {
            CreateWord("newid", "test", 12, 12) // Very close to first word (10,10)
        };
        var secondResult = new OcrResult { Words = secondWords };

        var result = await await bridge.ProcessAsync(secondResult, CancellationToken.None, CancellationToken.None);

        Assert.Single(result.Words);
        Assert.Equal("word_0", result.Words[0].Id); // Should match closest spatial word
        Assert.Equal("test", result.Words[0].Text);
    }

    [Fact]
    public async Task ProcessAsync_MultipleOldWordsMatchOneNew_ClosestLevDistanceWins()
    {
        await using var bridge = CreateBridge(maxLevDist: 5);

        // First result with multiple similar words
        var firstWords = new List<Word>
        {
            CreateWord("word_0", "hello", 10, 10),   // Distance 1 from "hell"
            CreateWord("word_1", "help", 50, 10),    // Distance 2 from "hell"
            CreateWord("word_2", "held", 90, 10)     // Distance 2 from "hell"
        };
        var firstResult = new OcrResult { Words = firstWords };
        await await bridge.ProcessAsync(firstResult, CancellationToken.None, CancellationToken.None);

        // Second result with word that could match multiple previous words
        var secondWords = new List<Word>
        {
            CreateWord("newid", "hell", 30, 30) // Closest to "hello" (distance 1)
        };
        var secondResult = new OcrResult { Words = secondWords };

        var result = await await bridge.ProcessAsync(secondResult, CancellationToken.None, CancellationToken.None);

        Assert.Single(result.Words);
        Assert.Equal("word_0", result.Words[0].Id); // Should match "hello" (closest Levenshtein distance)
        Assert.Equal("hell", result.Words[0].Text);
    }

    [Fact] 
    public async Task ProcessAsync_OneOldWordMatchesMultipleNew_FirstMatchWinsOthersGetNewIds()
    {
        await using var bridge = CreateBridge(maxLevDist: 5);

        // First result with one word
        var firstWords = new List<Word>
        {
            CreateWord("word_0", "hello", 10, 10)
        };
        var firstResult = new OcrResult { Words = firstWords };
        await await bridge.ProcessAsync(firstResult, CancellationToken.None, CancellationToken.None);

        // Second result with multiple words that could all match the previous word
        var secondWords = new List<Word>
        {
            CreateWord("id1", "hell", 50, 50),   // Distance 1 - could match
            CreateWord("id2", "help", 100, 100), // Distance 2 - could match
            CreateWord("id3", "held", 150, 150)  // Distance 2 - could match
        };
        var secondResult = new OcrResult { Words = secondWords };

        var result = await await bridge.ProcessAsync(secondResult, CancellationToken.None, CancellationToken.None);

        Assert.Equal(3, result.Words.Count);
        Assert.Equal("word_0", result.Words[0].Id); // First match gets reused ID
        Assert.Equal("word_1", result.Words[1].Id); // Second gets new ID
        Assert.Equal("word_2", result.Words[2].Id); // Third gets new ID
        Assert.Equal("hell", result.Words[0].Text);
        Assert.Equal("help", result.Words[1].Text);
        Assert.Equal("held", result.Words[2].Text);
    }

    private static DataflowBridge<OcrResult, OcrResult> CreateBridge(int maxLevDist)
    {
        var deduplicator = new DeduplicatorBlock(maxLevDist);
        var block = DataflowBlock.Encapsulate(deduplicator.Target, deduplicator.Source);
        return new DataflowBridge<OcrResult, OcrResult>(block);
    }

    private static Word CreateWord(string id, string text, double x, double y)
    {
        var polygon = new List<JsonPoint>
        {
            new() { X = x, Y = y },
            new() { X = x + 30, Y = y },
            new() { X = x + 30, Y = y + 20 },
            new() { X = x, Y = y + 20 }
        };

        return new Word
        {
            Id = id,
            Text = text,
            Confidence = 0.95,
            BoundingBox = new BoundingBox
            {
                Polygon = polygon,
                AARectangle = new AARectangle
                {
                    X = x,
                    Y = y,
                    Width = 30,
                    Height = 20
                },
                ORectangle = polygon
            }
        };
    }
}
