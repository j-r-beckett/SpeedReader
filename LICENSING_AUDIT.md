# SpeedReader Licensing Audit Report

**Date:** 2025-12-28
**SpeedReader License:** Apache License 2.0

---

## Executive Summary

SpeedReader is licensed under Apache 2.0 and uses dependencies with primarily permissive licenses (MIT, Apache 2.0, BSD). The project is **generally compatible** with commercial distribution, but there are **important considerations** regarding:

1. **SixLabors libraries** - May require commercial licensing for companies with ≥$1M annual revenue
2. **ONNX Runtime** - Must distribute ThirdPartyNotices.txt file
3. **Attribution requirements** - Several dependencies require copyright/license notices

---

## 1. Runtime Dependencies (Distributed with SpeedReader Binary)

### 1.1 NuGet Packages

#### MIT License (10 packages)
| Package | Version | Notes |
|---------|---------|-------|
| BenchmarkDotNet | 0.15.8 | Permissive, no restrictions |
| CLIwrap | 3.8.2 | Permissive, no restrictions |
| CommunityToolkit.HighPerformance | 8.4.0 | Permissive, no restrictions |
| coverlet.msbuild | 6.0.4 | Test coverage tool |
| Microsoft.Extensions.DependencyInjection | 9.0.10 | Permissive, no restrictions |
| Microsoft.NET.Test.Sdk | 17.12.0 | Test framework |
| System.CommandLine | 2.0.0-beta4 | Permissive, no restrictions |
| System.Numerics.Tensors | 10.0.0-preview.4 | Permissive, no restrictions |

**License Requirements:**
- Include copyright notice and MIT license text
- No patent clauses
- Commercial use permitted

#### Apache-2.0 License (7 packages)
| Package | Version | Notes |
|---------|---------|-------|
| OpenTelemetry.Exporter.OpenTelemetryProtocol | 1.14.0 | Telemetry |
| OpenTelemetry.Extensions.Hosting | 1.13.1 | Telemetry |
| OpenTelemetry.Instrumentation.AspNetCore | 1.14.0 | Telemetry |
| OpenTelemetry.Instrumentation.Process | 1.14.0-beta.2 | Telemetry |
| OpenTelemetry.Instrumentation.Runtime | 1.14.0 | Telemetry |
| xunit | 2.9.3 | Test framework |
| xunit.runner.visualstudio | 3.1.3 | Test framework |

**License Requirements:**
- Include copyright notice and Apache 2.0 license text
- Include patent grant
- State modifications if made
- Patent retaliation clause (if you sue for patent infringement, license terminates)

#### Six Labors Split License, Version 1.0 (3 packages) ⚠️
| Package | Version | Notes |
|---------|---------|-------|
| SixLabors.Fonts | 2.1.3 | Font rendering |
| SixLabors.ImageSharp | 3.1.11 | Image processing |
| SixLabors.ImageSharp.Drawing | 2.1.6 | Image drawing |

**License Requirements - CRITICAL:**

**Free/Apache-2.0 applies if:**
- SpeedReader is licensed under an OSI-approved open source license ✅ (Apache 2.0)
- Used as a transitive dependency ✅
- Direct dependency by company with <$1M USD annual gross revenue ✅ (if applicable)
- Direct dependency by non-profit organization ✅ (if applicable)

**Commercial License Required if:**
- For-profit company with ≥$1M USD annual gross revenue using SpeedReader in closed-source software
- SpeedReader itself is open source (Apache 2.0), so this should qualify for free use

**Implications:**
- ✅ SpeedReader project itself can use these libraries for free under Apache 2.0
- ⚠️ Users who fork SpeedReader and create closed-source derivatives with revenue ≥$1M may need commercial licenses
- ✅ License is non-viral (doesn't affect downstream consumers of SpeedReader)

#### Boost Software License 1.0 (1 package)
| Package | Version | Notes |
|---------|---------|-------|
| Clipper2 | 1.5.3 | Polygon clipping |

**License Requirements:**
- Include copyright notice and license text
- Very permissive, similar to MIT

#### Microsoft Public License (1 package)
| Package | Version | Notes |
|---------|---------|-------|
| Xunit.Combinatorial | 1.6.24 | Test framework extension |

**License Requirements:**
- Include copyright notice and license text
- OSI-approved, permissive

---

### 1.2 Native Dependencies

#### ONNX Runtime 1.15.0 - MIT License ⚠️

**Source:** https://github.com/microsoft/onnxruntime
**Copyright:** Microsoft Corporation
**Linking:** Statically linked into SpeedReader binary

**License Requirements:**
- ✅ Fully compatible with Apache 2.0
- ⚠️ **MUST distribute ThirdPartyNotices.txt** (6,023 lines) with binary
- ⚠️ **MUST include MIT copyright notice** in distribution

**Third-Party Components:**
- **Intel MKL** (Intel Simplified Software License) - Limited patent grant that excludes combinations
- **Intel DNNL** (Apache 2.0) - Standard patent grant with retaliation clause
- Various BSD, zlib, and other permissive licenses

**Patent Considerations:**
- MIT license has no explicit patent grant
- Intel MKL patent grant: "shall not apply to any combinations" (only covers MKL itself, not combined work)
- Intel DNNL includes Apache 2.0 patent grant with standard retaliation clause

**Static Linking:**
- ✅ Legally permitted
- ✅ Technically supported (with API usage restrictions)
- ⚠️ Must preserve all copyright notices and ThirdPartyNotices.txt

---

### 1.3 ML Model Weights

#### DbNet (Text Detection) - Apache License 2.0

**Source:** https://github.com/open-mmlab/mmocr
**Model:** dbnet_resnet18_fpnc_1200e_icdar2015 (fp32 and int8)
**Copyright:** MMOCR Authors (2021)

**License Requirements:**
- ✅ Commercial use permitted
- ✅ Redistribution permitted
- ✅ Compatible with SpeedReader's Apache 2.0 license
- Recommended: Include citation to original paper

**Citation:**
```bibtex
@article{Liao_Wan_Yao_Chen_Bai_2020,
    title={Real-Time Scene Text Detection with Differentiable Binarization},
    journal={Proceedings of the AAAI Conference on Artificial Intelligence},
    author={Liao, Minghui and Wan, Zhaoyi and Yao, Cong and Chen, Kai and Bai, Xiang},
    year={2020},
    pages={11474-11481}}
```

#### SVTRv2 (Text Recognition) - Apache License 2.0

**Source:** https://github.com/Topdu/OpenOCR
**Model:** SVTRv2 base CTC (fp32)
**Copyright:** OCR team, FVL Lab, Fudan University

**License Requirements:**
- ✅ Commercial use permitted
- ✅ Redistribution permitted
- ✅ Compatible with SpeedReader's Apache 2.0 license
- Recommended: Include citation to original paper

**Citation:**
```bibtex
@inproceedings{Du2025SVTRv2,
    title={SVTRv2: CTC Beats Encoder-Decoder Models in Scene Text Recognition},
    author={Yongkun Du and Zhineng Chen and Hongtao Xie and Caiyan Jia and Yu-Gang Jiang},
    booktitle={ICCV},
    year={2025},
    pages={20147-20156}
}
```

---

## 2. Build-Time Dependencies (NOT Distributed)

### 2.1 Python Packages

These packages are used only in build scripts and development tooling and are **NOT distributed** with SpeedReader binaries.

#### Permissive Licenses (No Restrictions)
| Package | License | Usage |
|---------|---------|-------|
| click | BSD-3-Clause | CLI framework |
| rich | MIT | Terminal formatting |
| psutil | BSD-3-Clause | Process utilities |
| onnx | Apache-2.0 | ONNX model tools |
| onnxruntime | MIT | ONNX inference (build-time) |
| onnxsim | Apache-2.0 | ONNX simplification |
| pillow | HPND (MIT-CMU) | Image processing |
| numpy | BSD-3-Clause | Numerical computing |
| torch | BSD-style | PyTorch (model building) |
| torchvision | BSD | PyTorch vision (model building) |
| tqdm | MPL-2.0 / MIT (dual) | Progress bars |
| pixelmatch | ISC | Image comparison |

#### Copyleft/Restrictive Licenses ⚠️
| Package | License | Usage | Impact |
|---------|---------|-------|--------|
| Polygon3 | LGPL + Proprietary (gpc) | Benchmarking | ⚠️ Build-time only, not distributed |

**Note:** LGPL requires modifications to Polygon3 itself be released under LGPL, but since this is build-time only and not distributed with SpeedReader, it has no impact on SpeedReader's licensing.

---

## 3. License Compatibility Analysis

### 3.1 SpeedReader (Apache 2.0) Compatibility Matrix

| Dependency License | Compatible? | Notes |
|-------------------|-------------|-------|
| MIT | ✅ Yes | Fully compatible, very permissive |
| Apache-2.0 | ✅ Yes | Same license, perfect compatibility |
| BSD-3-Clause | ✅ Yes | Fully compatible |
| BSL-1.0 (Boost) | ✅ Yes | Fully compatible |
| MS-PL | ✅ Yes | Compatible with Apache 2.0 |
| Six Labors Split | ⚠️ Conditional | Free for open source projects |
| HPND | ✅ Yes | Permissive |
| ISC | ✅ Yes | Permissive |
| LGPL (build-time) | ✅ Yes | Not distributed, no impact |

**Conclusion:** All runtime dependencies are compatible with Apache 2.0 licensing.

---

## 4. Implications for SpeedReader

### 4.1 Distribution Requirements

When distributing SpeedReader binaries, you **MUST** include:

1. **SpeedReader's Apache 2.0 LICENSE** (already present)

2. **Third-Party Notices** including:
   - ONNX Runtime's ThirdPartyNotices.txt
   - Copyright notices for all NuGet packages
   - Apache 2.0 license text (for ONNX packages, ML models)
   - MIT license text (for MIT packages, ONNX Runtime)
   - BSD license text (for BSD packages)
   - Boost license text (for Clipper2)
   - MS-PL license text (for Xunit.Combinatorial)
   - Six Labors Split License text

3. **Optional but Recommended:**
   - Citations for DbNet and SVTRv2 models
   - Acknowledgments section in documentation

### 4.2 Commercial Use

**✅ Commercial use is permitted** for SpeedReader with the following considerations:

1. **SixLabors Libraries:**
   - SpeedReader itself qualifies for free Apache-2.0 licensing (open source)
   - Users who fork and create closed-source derivatives with ≥$1M revenue need commercial licenses
   - This is a concern for **downstream users**, not SpeedReader itself

2. **Patent Considerations:**
   - Apache 2.0 dependencies include patent grants
   - MIT dependencies (ONNX Runtime) have no explicit patent grant
   - Intel MKL's patent grant excludes combinations (standard limitation)

3. **No Copyleft Contamination:**
   - All runtime dependencies use permissive licenses
   - No GPL/LGPL in distributed code

### 4.3 Recommended Actions

**High Priority:**

1. ✅ Create a `THIRD_PARTY_NOTICES.txt` file containing:
   - All required copyright notices
   - All license texts
   - ONNX Runtime's ThirdPartyNotices.txt

2. ✅ Update README.md to include:
   - License section
   - Attribution to ML models (DbNet, SVTRv2)
   - Link to THIRD_PARTY_NOTICES.txt

3. ⚠️ Verify SixLabors licensing applicability:
   - Confirm SpeedReader qualifies as open source
   - Document that closed-source forks may need commercial licenses

**Medium Priority:**

4. ✅ Add model citations to documentation:
   - DbNet paper citation
   - SVTRv2 paper citation

5. ✅ Document licensing in build artifacts:
   - Include LICENSE and THIRD_PARTY_NOTICES.txt in release packages
   - Include in Docker images if applicable

**Low Priority:**

6. Consider automated license scanning in CI:
   - Use tools like `dotnet-project-licenses` for NuGet packages
   - Validate license compliance on each release

---

## 5. Risk Assessment

| Risk | Severity | Likelihood | Mitigation |
|------|----------|------------|------------|
| Missing third-party notices | Medium | High | Create THIRD_PARTY_NOTICES.txt |
| SixLabors commercial license violation | Medium | Low | Document open-source status |
| Patent litigation from Intel MKL combinations | Low | Very Low | Standard industry practice |
| LGPL contamination from Polygon3 | Low | None | Build-time only, not distributed |
| Attribution violations | Low | Medium | Automated license scanning |

---

## 6. Summary

### License Breakdown by Type

| License Type | Count | Percentage |
|--------------|-------|------------|
| MIT | 11 | 37% |
| Apache-2.0 | 10 | 33% |
| BSD/ISC | 5 | 17% |
| Six Labors Split | 3 | 10% |
| BSL-1.0 | 1 | 3% |
| MS-PL | 1 | 3% |

### Overall Status

**✅ SpeedReader is license-compliant** for commercial and open-source distribution under Apache 2.0, provided:

1. Third-party notices are properly included
2. Copyright attributions are preserved
3. SixLabors open-source status is documented

**No blocking issues identified.** All runtime dependencies use permissive licenses compatible with Apache 2.0.

---

## Appendix: License Texts Reference

For full license texts, see:
- Apache 2.0: https://www.apache.org/licenses/LICENSE-2.0.txt
- MIT: https://opensource.org/licenses/MIT
- BSD-3-Clause: https://opensource.org/licenses/BSD-3-Clause
- Six Labors Split: https://github.com/SixLabors/ImageSharp/blob/main/LICENSE
- BSL-1.0: https://www.boost.org/LICENSE_1_0.txt
- MS-PL: https://opensource.org/licenses/MS-PL
