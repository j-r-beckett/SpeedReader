# Model Conversion Overhaul Guide

## Context & Goals

We're replacing the current model building pipeline (Docker-based, uses mmdeploy/mmocr ecosystem) with a simpler approach using PaddleOCR's PP-OCRv5 models.

**Key Goals:**
- Eliminate Docker dependency
- Use modern, actively maintained models (PP-OCRv5 released 2025)
- Keep conversion process in scripts/ directory using uv environment
- Models output to target/models/ following structure.md
- One model at a time: DBNet first, then SVTRv2
- Delete old models before building new ones (prevent false positives)
- 100% test pass rate required at each step

## Why PaddleOCR PP-OCRv5?

### Detection (replacing current DBNet)
- **PP-OCRv5_mobile_det** (4.6MB) or **PP-OCRv5_server_det** (84MB)
- Uses same DBNet algorithm, better backbone (PPHGNetV2/PPLCNetV3 vs ResNet18)
- **Preprocessing is identical**: ImageNet normalization [123.675, 116.28, 103.53] / [58.395, 57.12, 57.375]
- **Same postprocessing**: unclip_ratio=1.5 (matches TextDetector.cs:133)
- **Same output**: probability map (HxW)
- **Drop-in compatible** - no code changes needed in TextDetector.cs

### Recognition (replacing current SVTRv2)
- **PP-OCRv5_mobile_rec** (16MB) or **PP-OCRv5_server_rec** (81MB)
- **Preprocessing is identical**: mean=127.5, std=127.5 ([-1,1] normalization)
- **Same input dimensions**: [3, 48, 320]
- **Same output**: CTC logits (NRTR head only used during training, not inference)
- **Character dictionary change**: 6,625 → 18,383 characters (need to update CharacterDictionary.cs)
- Better multilingual support (Chinese, Traditional Chinese, English, Japanese, Pinyin)

## Conversion Approach

### Phase 1: Setup
PaddleOCR distributes **pre-exported inference models** (not just pretrained weights):
- Download: `https://paddle-model-ecology.bj.bcebos.com/paddlex/official_inference_model/paddle3.0.0/PP-OCRv5_mobile_det_infer.tar`
- Contains: `inference.json`, `inference.pdiparams`, `inference.yml`
- Note: PaddlePaddle 3.x uses `.json` instead of `.pdmodel`

### Phase 2: Conversion
Use `paddle2onnx` (Python package with CLI) to convert:
```bash
paddle2onnx --model_dir <paddle_dir> \
            --model_filename inference.json \
            --params_filename inference.pdiparams \
            --save_file <output.onnx> \
            --opset_version 11 \
            --enable_onnx_checker True
```

**Critical: Python version compatibility**
- paddle2onnx requires `onnxoptimizer==0.3.13`
- onnxoptimizer only has pre-built wheels for Python ≤3.11
- **Use Python 3.11** in scripts/pyproject.toml (not 3.13)

### Phase 3: Integration
- Update ModelLoader.cs to reference new model names
- Update Resources.csproj to embed new models, remove old ones (one model at a time)
- For recognition: update CharacterDictionary.cs and CharacterDictionary.Data.txt with PP-OCRv5 dict
  - Source: `https://raw.githubusercontent.com/PaddlePaddle/PaddleOCR/main/ppocr/utils/dict/ppocrv5_dict.txt`
  - 18,383 characters vs current 6,622

## Available Resources

### Local References
- **~/library/PaddleOCR** - Complete PaddleOCR codebase for reference
  - Check conversion examples: `~/library/PaddleOCR/deploy/paddle2onnx/`
  - Test scripts: `~/library/PaddleOCR/test_tipc/`
  - Model configs: `~/library/PaddleOCR/configs/`

### Current Implementation
- **TextDetector.cs** - Preprocessing (lines 70-71), postprocessing (line 133 dilation)
- **TextRecognizer.cs** - Preprocessing (lines 63-64), CTC decode (line 79)
- **ModelLoader.cs** - Model loading from embedded resources
- **CharacterDictionary.cs** - Character index mapping

### Documentation
- PP-OCRv5 models: https://paddle-model-ecology.bj.bcebos.com/paddlex/official_inference_model/paddle3.0.0/
- Paddle2ONNX: https://github.com/PaddlePaddle/Paddle2ONNX
- Third-party pre-converted models (reference only): https://github.com/MeKo-Christian/paddleocr-onnx

## Key Findings

### NRTR is Transparent
PP-OCRv5 recognition uses dual CTC+NRTR heads, but during inference only CTC output is returned:
```python
# From rec_multi_head.py
if not self.training:
    return ctc_out  # Only CTC during inference
```
No special handling needed - our GreedyCTCDecode() will work as-is.

### Model Format Evolution
- PaddlePaddle 2.x: `.pdmodel` + `.pdiparams`
- PaddlePaddle 3.x: `inference.json` + `.pdiparams`
- paddle2onnx handles both formats automatically

### Quantization
Not adding new quantization options in this phase. Keep current:
- DBNet: FP32 + INT8
- SVTRv2/PP-OCRv5_rec: FP32 only

## Reproducibility Requirement

**Must be able to rebuild everything from scratch:**
```bash
# Delete all models and character dictionary
rm -rf target/models/
rm Src/Resources/CharacterDictionary.Data.txt

# Regenerate everything
uv run scripts/setup_models.py

# Everything should work
dotnet test
```

The `scripts/setup_models.py` script must:
1. Download PP-OCRv5 inference models (tar files)
2. Extract them to target/models/paddle/
3. Convert to ONNX → target/models/onnx/
4. Download and save PP-OCRv5 character dictionary to Src/Resources/CharacterDictionary.Data.txt
5. Be idempotent (safe to run multiple times)

## Process

1. **Update scripts/pyproject.toml**: Python >=3.11, add dependencies (paddle2onnx, onnx, onnxsim)
2. **Create scripts/setup_models.py** - Single reproducible entry point for all model setup
3. **Start with DBNet FP32**:
   - Implement download + conversion in setup_models.py
   - Update ModelLoader.cs with new model name
   - Update Resources.csproj: add new model, remove old dbnet FP32
   - Delete old model files
   - Build and run tests - must achieve 100% pass
4. **Then DBNet INT8** (if needed - may require quantization tooling)
5. **Repeat for recognition model** (including character dictionary download)
6. **Delete Src/Resources/modelBuilder/** when complete

## Anti-Patterns to Avoid

- Don't test against old models accidentally (delete before building)
- Don't disable tests to get to 100% pass rate
- Don't make changes outside the scope of model conversion
- Don't assume solutions will work - verify at each step
- Don't proceed to next model until current one fully working

## Model Selection

**Using mobile variants for both models:**
- **Detection**: PP-OCRv5_mobile_det (4.6MB)
- **Recognition**: PP-OCRv5_mobile_rec (16MB)

These are optimized for CPU deployment and smaller binary size.

## Open Questions (to be resolved during implementation)

- INT8 quantization tooling for new models?
- Do we need onnxsim optimization step?
- Exact naming convention for models in target/models/onnx/?
