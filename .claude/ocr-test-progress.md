# OCR File Testing Checklist

## Main OCR Files (requiring test classes):
- [ ] `Buffer.cs` → `BufferTests.cs`
- [ ] `CharacterDictionary.cs` → `CharacterDictionaryTests.cs`
- [ ] `DBNet.cs` → `DBNetPreprocessTests.cs` + `DBNetPostprocessTests.cs`
- [ ] `ModelRunner.cs` → `ModelRunnerTests.cs`
- [ ] `SVTRv2.cs` → `SVTRv2PreprocessTests.cs` + `SVTRv2PostprocessTests.cs`

## Algorithm Files (already have tests):
- [x] `Thresholding.cs` (was Binarization) → `BinarizationTests.cs` ✓
- [x] `ConnectedComponents.cs` → `ConnectedComponentsTests.cs` ✓ **COMPLETED**
- [x] `ConvexHull.cs` → `ConvexHullTests.cs` ✓ **COMPLETED**
- [x] `TensorOps.cs` (was TensorConversion) → `TensorOpsTests.cs` ✓ **COMPLETED**
- [ ] `Dilation.cs` (was PolygonDilation) → `PolygonDilationTests.cs` - needs documentation & test improvements
- [x] `Resampling.cs` (was Resize) → `ResizeCalculationTests.cs` ✓ **COMPLETED**
- [ ] `CTC.cs` → May need dedicated `CTCTests.cs` (currently only in end-to-end tests)

## Data Files (no tests needed):
- `CharacterDictionary.Data.txt` (data file)

## Progress Notes:
- **ConvexHull.cs**: ✅ Complete
  - Added comprehensive XML documentation for all methods
  - Added YouTube video reference to Graham scan tutorial
  - Fixed implementation to return empty for < 3 points
  - Combined three edge case tests into single theory
  - Added pathological test cases with mixed coordinates
  - Fixed counter-clockwise ordering verification test