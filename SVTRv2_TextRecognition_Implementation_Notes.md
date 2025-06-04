# SVTRv2 Text Recognition Implementation Notes

## Overview

SVTRv2 is a CTC-based text recognition model designed for high accuracy and fast inference. This document outlines the implementation approach for integrating SVTRv2 into the OpusFlow video processing pipeline, following the established patterns from the TextDetection project.

## Architecture Summary

**Model Type**: CTC-based text recognition (not encoder-decoder)
**Key Innovation**: Multi-Scale Resizing (MSR) + Feature Rearrangement Module (FRM)
**Performance**: Superior accuracy with faster inference than attention-based models
**Input**: RGB images of text regions
**Output**: Character sequences via CTC decoding (spatial sequence left-to-right)

**Core Technical Innovation**: SVTRv2 relocates complexity from geometric space to feature space, leveraging CTC's natural ability to handle variable sequences rather than fighting against irregular text shapes.

## Selected Model Configuration

**Model**: RepSVTR (mobile-optimized variant)
**Decoder**: CTCDecoder (simple linear layer for fastest inference ~4-5ms)
**Architecture**: Mobile-optimized SVTRv2 with reparameterizable components
**Character Set**: Multilingual (Chinese + English), supports both languages out of the box
**Max Sequence Length**: 25 characters
**Model Size**: 62.3MB (vs 118MB for full SVTRv2)
**Language Support**: Built-in Chinese and English recognition capability
**Alternative Considered**: Full SVTRv2 Base (kept for future accuracy improvements if needed)

## Project Structure

Following the TextDetection project pattern, create a `TextRecognition` project with:

```
Src/TextRecognition/
├── TextRecognition.csproj         # Package references: SixLabors.ImageSharp, System.Numerics.Tensors, Microsoft.ML.OnnxRuntime
├── TextRecognizer.cs              # Main inference class (similar to TextDetector.cs)
├── Preprocessor.cs                # Image preprocessing and normalization
├── PostProcessor.cs               # CTC decoding and text extraction
└── CharacterDictionary.cs         # Character mapping and vocabulary management
```

## 1. Preprocessor Implementation

### Core Responsibilities
- Multi-Size Resizing (MSR) with aspect ratio preservation
- Normalization to model-specific ranges
- Tensor formatting (HWC → CHW conversion)
- Batch processing support

### Key Processing Steps

#### A. Multi-Scale Resizing (MSR)

**What MSR Really Is**: MSR is aspect-ratio preserving resize - a standard computer vision technique. The innovation wasn't inventing this preprocessing, but creating a model architecture that could handle variable input sizes that MSR produces.

**The Technical Challenge**: Previous text recognition models required fixed input dimensions due to:
- Batch processing constraints (all tensors must have identical shapes)
- Fixed convolutional layer expectations
- Sequence alignment requirements in encoder-decoder models

**SVTRv2's Solution**: Feature Rearrangement Module (FRM) that handles variable spatial dimensions and converts them to consistent sequential features that CTC can process.

**Practical Implementation**: While not truly novel, SVTRv2's approach is interesting - they essentially support ~4 standard aspect ratios that the model is trained on (e.g., 64×32, 96×32, 128×32, 160×32), rather than completely arbitrary sizes.

**Critical Batching Constraint**: When assembling batches for inference, images **must be grouped by their post-MSR size**. You cannot batch together a 64×32 image with a 128×32 image in the same tensor - they must be processed in separate batches.

```csharp
// MSR: Aspect-ratio preserving resize (similar to your existing DBNet preprocessing)
public static (int width, int height) CalculateOptimalSize(Image<Rgb24> image)
{
    const int targetHeight = 32;  // Standard height for text recognition
    const int maxWidth = 128;     // Configurable max width
    const int minWidth = 8;       // Minimum readable width
    
    double aspectRatio = (double)image.Width / image.Height;
    int optimalWidth = (int)Math.Round(targetHeight * aspectRatio);
    
    // Clamp to acceptable range
    optimalWidth = Math.Clamp(optimalWidth, minWidth, maxWidth);
    
    // Align to divisibility requirements if needed
    optimalWidth = (optimalWidth + 3) / 4 * 4;  // Align to 4-pixel boundaries
    
    return (optimalWidth, targetHeight);
}

// Group images by post-MSR dimensions for batching
public static Dictionary<(int width, int height), List<Image<Rgb24>>> GroupByMSRSize(Image<Rgb24>[] images)
{
    var groups = new Dictionary<(int, int), List<Image<Rgb24>>>();
    
    foreach (var image in images)
    {
        var size = CalculateOptimalSize(image);
        if (!groups.ContainsKey(size))
            groups[size] = new List<Image<Rgb24>>();
        groups[size].Add(image);
    }
    
    return groups;
}
```

#### B. Normalization Strategies
```csharp
// SVTRv2 Standard Normalization: [0,1] → [-1,1]
static readonly float[] SVTRv2_MEANS = [0.5f, 0.5f, 0.5f];
static readonly float[] SVTRv2_STDS = [0.5f, 0.5f, 0.5f];

// Alternative: ImageNet normalization (model-dependent)
static readonly float[] IMAGENET_MEANS = [0.485f, 0.456f, 0.406f];
static readonly float[] IMAGENET_STDS = [0.229f, 0.224f, 0.225f];
```

#### C. Tensor Processing
```csharp
public static Tensor<float> Preprocess(Image<Rgb24>[] batch)
{
    // 1. Calculate optimal dimensions for batch
    (int maxWidth, int height) = CalculateBatchDimensions(batch);
    
    // 2. Create NCHW tensor: [batch_size, 3, height, width]
    ReadOnlySpan<nint> shape = [(nint)batch.Length, 3, (nint)height, (nint)maxWidth];
    var tensor = Tensor.Create(new float[batch.Length * 3 * height * maxWidth], shape);
    
    // 3. Process each image with MSR and normalization
    // (Similar to TextDetection but with different resize strategy)
    
    return tensor;
}
```

### Comparison with RobustScanner
From your existing RobustScanner config:
- **RobustScanner**: Fixed height=48, width=160, padding to exact size
- **SVTRv2**: Adaptive MSR sizing, height=32, variable width with aspect preservation

## SVTRv2 Technical Architecture Deep Dive

### The Engineering Philosophy: Complexity Relocation

**Traditional Text Recognition Approach**:
```
Irregular Text → Geometric Warping → Standard Shape → Simple Feature Processing
```

**SVTRv2 Approach**:
```
Irregular Text → MSR (Preserve Shape) → Complex Feature Handling (FRM) → CTC Sequence
```

### Key Tradeoff: Variable Inputs → Complex Feature Handling

**The Problem MSR Creates**: Variable input sizes (64×32, 96×32, 128×32, 160×32) produce feature maps of different spatial dimensions. Most models expect consistent feature dimensions.

**How SVTRv2 Solved It**: Feature Rearrangement Module (FRM) handles this complexity:
- **Horizontal rearrangement**: Handles variable width dimensions
- **Vertical rearrangement**: Uses "selecting token" to collapse variable height dimension to consistent sequence
- **Result**: Variable spatial inputs → consistent sequential features for CTC

### Other Architectural Changes

**Doubled feature resolution**: From H/16 × W/4 to H/8 × W/4 to better preserve irregular text details
**Replaced local attention with grouped convolutions**: Better for capturing character-level features like strokes and textures  
**Removed rectification module**: SVTR used geometric transforms to "straighten" text, but this hurt long text recognition

### Why This Works with CTC

**CTC models are naturally good at handling variable sequences**, so SVTRv2 moves complexity to feature space where CTC excels rather than fighting irregular text in geometric space.

**Traditional paradigm**: "Make the data fit the model"
**SVTRv2 paradigm**: "Make the model handle natural data complexity"

## 2. TextRecognizer Implementation

### Core Structure
```csharp
public class TextRecognizer : IDisposable
{
    private readonly InferenceSession _session;
    private readonly CharacterDictionary _dictionary;
    private readonly ILogger<TextRecognizer> _logger;

    public string[] RecognizeText(Tensor<float> input)
    {
        // 1. Run ONNX inference (similar to TextDetector pattern)
        // 2. Get CTC output logits: [batch_size, sequence_length, num_classes]
        // 3. Apply CTC decoding via PostProcessor
        // 4. Return decoded text strings
    }
}
```

### ONNX Integration
```csharp
public Tensor<float> RunInference(Tensor<float> input)
{
    // Follow exact pattern from TextDetector.cs:18-41
    float[] inputBuffer = new float[input.FlattenedLength];
    input.FlattenTo(inputBuffer);
    long[] shape = Array.ConvertAll(input.Lengths.ToArray(), x => (long)x);
    using var inputOrtValue = OrtValue.CreateTensorValueFromMemory(inputBuffer, shape);

    var inputs = new Dictionary<string, OrtValue>
    {
        { "input", inputOrtValue }  // Verify input tensor name from model
    };

    using var runOptions = new RunOptions();
    using var ortOutputs = _session.Run(runOptions, inputs, _session.OutputNames);
    
    // Convert to Tensor<float> for CTC processing
    // Output shape: [batch_size, sequence_length, num_classes]
}
```

## 3. PostProcessor Implementation

### CTC Decoding Algorithm

**IMPORTANT**: The "sequence_length" dimension represents **spatial sequence** (left-to-right reading order), not temporal sequence. Each position corresponds to a spatial region of the text image from left to right. CTC terminology is borrowed from speech recognition but refers to spatial progression in text recognition.

```csharp
public class PostProcessor
{
    private readonly CharacterDictionary _dictionary;
    
    public string[] DecodeCTC(Tensor<float> logits)
    {
        // Input: [batch_size, sequence_length, num_classes]
        // sequence_length = spatial positions from left-to-right across text image
        // num_classes = vocabulary size + CTC blank token
        // Output: Decoded text strings
        
        var results = new List<string>();
        var batchSize = (int)logits.Lengths[0];
        var sequenceLength = (int)logits.Lengths[1];
        var numClasses = (int)logits.Lengths[2];
        
        for (int batch = 0; batch < batchSize; batch++)
        {
            string text = DecodeSingleSequence(logits, batch, sequenceLength, numClasses);
            results.Add(text);
        }
        
        return results.ToArray();
    }
    
    private string DecodeSingleSequence(Tensor<float> logits, int batchIndex, int seqLen, int numClasses)
    {
        var decoded = new List<char>();
        int prevIndex = -1;
        const int blankTokenIndex = 0;  // CTC blank token (usually index 0)
        
        for (int t = 0; t < seqLen; t++)
        {
            // Find argmax at spatial position t (left-to-right across text image)
            int maxIndex = 0;
            float maxValue = float.MinValue;
            
            for (int c = 0; c < numClasses; c++)
            {
                ReadOnlySpan<nint> indices = [batchIndex, t, c];
                float value = logits.AsTensorSpan()[indices];
                if (value > maxValue)
                {
                    maxValue = value;
                    maxIndex = c;
                }
            }
            
            // CTC rule: only add if different from previous and not blank
            if (maxIndex != prevIndex && maxIndex != blankTokenIndex)
            {
                char character = _dictionary.IndexToChar(maxIndex);
                decoded.Add(character);
            }
            
            prevIndex = maxIndex;
        }
        
        return new string(decoded.ToArray());
    }
}
```

## 4. CharacterDictionary Implementation

### Standard English Dictionary (94 characters)
```csharp
public class CharacterDictionary
{
    // SVTRv2 standard character set
    private static readonly string CHARACTERS = 
        "0123456789" +
        "abcdefghijklmnopqrstuvwxyz" +
        "ABCDEFGHIJKLMNOPQRSTUVWXYZ" +
        "!\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~";
    
    private readonly Dictionary<int, char> _indexToChar;
    private readonly Dictionary<char, int> _charToIndex;
    
    public CharacterDictionary()
    {
        _indexToChar = new Dictionary<int, char>();
        _charToIndex = new Dictionary<char, int>();
        
        // Index 0 reserved for CTC blank token
        for (int i = 0; i < CHARACTERS.Length; i++)
        {
            char c = CHARACTERS[i];
            int index = i + 1;  // Offset by 1 for blank token
            _indexToChar[index] = c;
            _charToIndex[c] = index;
        }
    }
    
    public char IndexToChar(int index) => _indexToChar.TryGetValue(index, out char c) ? c : '?';
    public int CharToIndex(char c) => _charToIndex.TryGetValue(c, out int index) ? index : 0;
    public int VocabularySize => CHARACTERS.Length + 1;  // +1 for blank token (93 + 1 = 94 total)
}
```

### Alternative: Load from File (like RobustScanner)
```csharp
// For models that include dict_file.txt
public static CharacterDictionary LoadFromFile(string dictFilePath)
{
    var lines = File.ReadAllLines(dictFilePath);
    return new CharacterDictionary(lines);
}
```

## 5. ModelZoo Integration

### Add SVTRv2 Model Enum
```csharp
// In Models/ModelZoo.cs
public enum Model
{
    DbNet18,
    Robust31,
    RepSVTR,         // Add RepSVTR mobile model
}

private static string GetModelDirectory(Model model) => model switch
{
    Model.DbNet18 => "dbnet_resnet18_fpnc_1200e_icdar2015",
    Model.Robust31 => "robustscanner_resnet31_5e_st-sub_mj-sub_sa_real",
    Model.RepSVTR => "repsvtr_multilingual",  // Model directory name
    _ => throw new ArgumentException($"Unknown model: {model}")
};
```

## 6. ONNX Model Conversion

### Docker Build Process Integration
Add to `modelBuilder/Dockerfile`:
```dockerfile
# SVTRv2 Base CTC
RUN git clone https://github.com/PaddlePaddle/PaddleOCR && \
    cd PaddleOCR && \
    pip3 install -r requirements.txt

# Convert SVTRv2 model using PyTorch to ONNX
COPY convert_svtrv2.py /models/
RUN python3 /models/convert_svtrv2.py \
    --output-dir /models/svtrv2_base_ctc \
    --model-variant base \
    --decoder ctc
```

### Approach 1: Use OpenOCR's Built-in ONNX Export (Recommended)
```bash
# Download OpenOCR and RepSVTR model
git clone https://github.com/Topdu/OpenOCR.git
cd OpenOCR
pip install -r requirements.txt
wget https://github.com/Topdu/OpenOCR/releases/download/develop0.0.1/openocr_repsvtr_ch.pth

# Convert to ONNX using OpenOCR's tools
pip install onnx
python tools/toonnx.py --c configs/rec/svtrv2/repsvtr_ch.yml --o Global.device=cpu Global.pretrained_model=./openocr_repsvtr_ch.pth

# Model will be saved in ./output/rec/repsvtr_ch/export_rec/rec_model.onnx
```

### Approach 2: Custom PyTorch to ONNX Conversion Script
```python
# Alternative approach if custom conversion needed
import torch
from openrec.modeling.encoders.svtrv2 import SVTRv2LNConvTwo33
from openrec.modeling.decoders.ctc_decoder import CTCDecoder

class RepSVTRModel(torch.nn.Module):
    def __init__(self):
        super().__init__()
        # Load pretrained RepSVTR architecture
        # (exact architecture details from repsvtr_ch.yml config)
        self.encoder = SVTRv2LNConvTwo33(
            # RepSVTR-specific parameters
            out_channels=256
        )
        self.decoder = CTCDecoder(
            in_channels=256,
            out_channels=6625  # Multilingual vocabulary size
        )
    
    def forward(self, x):
        features = self.encoder(x)
        # RepSVTR-specific feature processing
        logits = self.decoder(features)
        return logits

# Export with dynamic width for MSR
input_tensor = torch.rand((1, 3, 32, 128), dtype=torch.float32)
model = RepSVTRModel()

torch.onnx.export(
    model,
    (input_tensor,),
    "repsvtr.onnx",
    input_names=["input"],
    output_names=["output"],
    dynamic_axes={
        "input": {0: "batch_size", 3: "width"},    # Dynamic batch + width
        "output": {0: "batch_size", 1: "sequence"}  # Dynamic batch + sequence
    },
    dynamo=True
)
```

### Model Download and Fine-tuning
```bash
# Download multilingual RepSVTR model (Chinese + English support)
wget https://github.com/Topdu/OpenOCR/releases/download/develop0.0.1/openocr_repsvtr_ch.pth

# Optional: Fine-tune for improved English performance
# See docs/finetune_rec.md in OpenOCR repository for detailed instructions
python tools/train_rec.py --c configs/rec/svtrv2/repsvtr_ch.yml --o Global.pretrained_model=./openocr_repsvtr_ch.pth
```

### Model Export Details
- **Input shape**: `[batch_size, 3, 32, width]` with dynamic width dimension
- **Output shape**: `[batch_size, sequence_length, vocab_size]` CTC logits (multilingual vocabulary)
- **Dynamic axes**: Both batch size and width/sequence dimensions are dynamic for MSR support
- **Preprocessing**: External (not included in ONNX model for flexibility)
- **Postprocessing**: CTC decoding in C# PostProcessor class with multilingual character dictionary

## Implementation Strategy

### Phase 1: Direct Integration (Recommended Start)
1. **Download RepSVTR multilingual model** from OpenOCR release
2. **Convert to ONNX** using OpenOCR's built-in tools
3. **Test on English video content** to evaluate out-of-box performance
4. **If accuracy sufficient** → ship with multilingual capability as bonus

### Phase 2: Optional Fine-tuning (If Needed)
1. **Fine-tune on English datasets** following `docs/finetune_rec.md`
2. **Improve English-specific accuracy** while maintaining multilingual support
3. **Expected improvement**: 5-15% accuracy boost on English text

### Key Resources
- **Fine-tuning documentation**: `docs/finetune_rec.md` in OpenOCR repository
- **Pretrained model**: https://github.com/Topdu/OpenOCR/releases/download/develop0.0.1/openocr_repsvtr_ch.pth
- **Model capabilities**: Supports Chinese and English text recognition out of the box

## 7. Performance Considerations

### Memory Optimization
- **Batch processing**: Support variable batch sizes efficiently, but **group by post-MSR dimensions**
- **Tensor reuse**: Pool tensors for repeated inference (separate pools per common size)
- **Dictionary caching**: Pre-compute character mappings

### Model Variants
- **SVTRv2-T (Tiny)**: 5.13M params, 5.0ms latency - mobile/edge deployment
- **SVTRv2-S (Small)**: 11.25M params, 5.3ms latency - balanced performance
- **SVTRv2-B (Base)**: 19.76M params, 7.0ms latency - highest accuracy

### Inference Optimization
```csharp
// Optimize for video processing pipeline
public class BatchTextRecognizer
{
    private readonly TextRecognizer _recognizer;
    private readonly int _maxBatchSize;
    
    public async Task<string[]> RecognizeBatchAsync(Image<Rgb24>[] images)
    {
        // Split into optimal batch sizes
        // Process concurrently when possible
        // Return ordered results
    }
}
```

## 7. Integration with Video Pipeline

### Text Detection → Recognition Pipeline
```csharp
// Usage example in video processing
public async Task<(List<(int X, int Y)> Polygon, string Text)[]> ProcessFrameAsync(Image<Rgb24> frame)
{
    // 1. Text Detection (existing)
    var detectionResults = await _textDetector.DetectTextAsync(frame);
    
    // 2. Extract text regions
    var textRegions = ExtractTextRegions(frame, detectionResults);
    
    // 3. Text Recognition (new)
    var recognitionResults = await _textRecognizer.RecognizeTextAsync(textRegions);
    
    // 4. Combine results
    return CombineResults(detectionResults, recognitionResults);
}
```

### Input Image Requirements
- **Source**: Cropped text regions from detection stage
- **Quality**: Higher resolution preferred for recognition accuracy
- **Preprocessing**: Detection coordinates → cropped images → recognition preprocessing

## 8. Testing Strategy

### Unit Tests
```csharp
// TextRecognition.Test project
[Fact]
public void Preprocessor_HandlesVariableWidthImages()
{
    // Test MSR with different aspect ratios
}

[Fact]
public void CTC_DecodesSimpleText()
{
    // Test CTC decoding with known outputs
}

[Fact]
public void EndToEnd_RecognizesTestImages()
{
    // Full pipeline with sample text images
}
```

### Performance Benchmarks
- **Latency**: Single image recognition time
- **Throughput**: Batch processing performance
- **Memory**: Peak memory usage during inference
- **Accuracy**: Character/word accuracy on test sets

## 9. Configuration Options

### Model Configuration
```csharp
public class SVTRv2Config
{
    public int TargetHeight { get; set; } = 32;
    public int MaxWidth { get; set; } = 128;
    public int MinWidth { get; set; } = 8;
    public NormalizationMode Normalization { get; set; } = NormalizationMode.SVTRv2Standard;
    public bool PreserveAspectRatio { get; set; } = true;
}

public enum NormalizationMode
{
    SVTRv2Standard,  // [-1,1] range
    ImageNet,        // ImageNet statistics
    Custom           // User-defined
}
```

This implementation approach leverages the proven patterns from your TextDetection project while adapting to the specific requirements of CTC-based text recognition with SVTRv2's MSR preprocessing strategy.