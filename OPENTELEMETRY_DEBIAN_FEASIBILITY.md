# OpenTelemetry .NET Debian Packaging Feasibility Assessment

**Date:** 2025-12-28
**SpeedReader Version:** Uses .NET 10.0
**OpenTelemetry Version Range:** 1.13.1 - 1.14.0

---

## Executive Summary

**Verdict: PACKAGING IS FEASIBLE BUT RESOURCE-INTENSIVE**

Packaging OpenTelemetry .NET for Debian is technically feasible but would require creating **15-20 new packages** with all transitive dependencies. All licenses are DFSG-compliant (MIT/Apache-2.0). However, **Debian currently has NO .NET runtime packages** beyond basic Mono support, which presents a fundamental infrastructure challenge.

**Recommendation:** Consider **dropping OpenTelemetry** or using a **minimal subset** (OTLP exporter only) to reduce packaging burden from ~20 packages to ~5 packages.

---

## 1. Debian Availability Status

### 1.1 Current OpenTelemetry Packages in Debian

**No .NET/C# OpenTelemetry packages exist in Debian.** The available packages are:

| Language | Package Count | Status |
|----------|--------------|--------|
| **Go** | 3 packages | ✅ Available (bookworm, trixie, sid) |
| **C++** | 3 packages | ✅ Available (bookworm-backports, trixie, sid) |
| **Python** | 14 packages | ✅ Available (forky, sid) |
| **C#/.NET** | **0 packages** | ❌ NOT AVAILABLE |

**Source:** [Debian Packages Search](https://packages.debian.org/search?keywords=opentelemetry)

### 1.2 .NET Runtime Availability in Debian

**Critical Infrastructure Gap:** Debian has **extremely limited .NET support**:

- ❌ No `dotnet-sdk` packages in official Debian repositories
- ❌ No `dotnet-runtime` packages in official Debian repositories
- ✅ Only 2 .NET packages exist: `libgtk-dotnet3.0-cil` (GTK bindings for Mono)
- ⚠️ Microsoft provides .NET packages via their own repository (not in Debian proper)

**Source:** [Debian Package Search for .NET](https://packages.debian.org/search?keywords=dotnet)

**Implications:**
1. Any .NET library packaging would require .NET runtime infrastructure to be available
2. Debian's NuGet packaging policy is **incomplete** (dependency management unresolved)
3. Microsoft's .NET packages for Debian use their own repository, not Debian's
4. Recent compatibility issues: Debian Trixie's libicu76 conflicts with .NET packages

**Sources:**
- [Teams/DebianMonoGroup/NuGet - Debian Wiki](https://wiki.debian.org/Teams/DebianMonoGroup/NuGet)
- [Install .NET on Debian](https://learn.microsoft.com/en-us/dotnet/core/install/linux-debian)
- [GitHub Issue: dotnet libicu76 incompatibility](https://github.com/dotnet/sdk/issues/48973)

---

## 2. Complete Dependency Analysis

### 2.1 SpeedReader's OpenTelemetry Package Usage

SpeedReader uses 5 OpenTelemetry NuGet packages:

```xml
<PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.14.0" />
<PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="1.13.1" />
<PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.14.0" />
<PackageReference Include="OpenTelemetry.Instrumentation.Process" Version="1.14.0-beta.2" />
<PackageReference Include="OpenTelemetry.Instrumentation.Runtime" Version="1.14.0" />
```

**Target Framework:** `net10.0` (SpeedReader uses .NET 10.0)

### 2.2 Full Dependency Tree

#### Tier 1: Direct Dependencies (5 packages)

1. **OpenTelemetry.Exporter.OpenTelemetryProtocol** 1.14.0
2. **OpenTelemetry.Extensions.Hosting** 1.13.1
3. **OpenTelemetry.Instrumentation.AspNetCore** 1.14.0
4. **OpenTelemetry.Instrumentation.Process** 1.14.0-beta.2
5. **OpenTelemetry.Instrumentation.Runtime** 1.14.0

#### Tier 2: OpenTelemetry Core Dependencies (3 packages)

6. **OpenTelemetry** ≥ 1.14.0
   - Depends on: Microsoft.Extensions.Diagnostics.Abstractions ≥ 10.0.0
   - Depends on: Microsoft.Extensions.Logging.Configuration ≥ 10.0.0
   - Depends on: OpenTelemetry.Api.ProviderBuilderExtensions ≥ 1.14.0

7. **OpenTelemetry.Api** ≥ 1.14.0
   - **NO dependencies on .NET 10.0** ✅

8. **OpenTelemetry.Api.ProviderBuilderExtensions** ≥ 1.14.0
   - Depends on: Microsoft.Extensions.DependencyInjection.Abstractions ≥ 10.0.0
   - Depends on: OpenTelemetry.Api ≥ 1.14.0

#### Tier 3: Microsoft.Extensions Dependencies (~12 packages)

**Key insight:** Since SpeedReader targets .NET 10.0, many Microsoft.Extensions packages have **reduced or zero dependencies**.

9. **Microsoft.Extensions.Configuration.Binder** ≥ 8.0.2
   - Depends on: Microsoft.Extensions.Configuration.Abstractions ≥ 8.0.0

10. **Microsoft.Extensions.Hosting.Abstractions** ≥ 9.0.0
    - Depends on: Microsoft.Extensions.Configuration.Abstractions ≥ 9.0.0
    - Depends on: Microsoft.Extensions.DependencyInjection.Abstractions ≥ 9.0.0
    - Depends on: Microsoft.Extensions.Diagnostics.Abstractions ≥ 9.0.0
    - Depends on: Microsoft.Extensions.FileProviders.Abstractions ≥ 9.0.0
    - Depends on: Microsoft.Extensions.Logging.Abstractions ≥ 9.0.0

11. **Microsoft.Extensions.DependencyInjection.Abstractions** ≥ 10.0.0
    - **NO dependencies on .NET 10.0** ✅

12. **Microsoft.Extensions.Diagnostics.Abstractions** ≥ 10.0.0
    - Depends on: Microsoft.Extensions.DependencyInjection.Abstractions ≥ 10.0.0
    - Depends on: Microsoft.Extensions.Options ≥ 10.0.0

13. **Microsoft.Extensions.Logging.Configuration** ≥ 10.0.0
    - Depends on: Microsoft.Extensions.Configuration
    - Depends on: Microsoft.Extensions.Configuration.Abstractions
    - Depends on: Microsoft.Extensions.Configuration.Binder
    - Depends on: Microsoft.Extensions.DependencyInjection.Abstractions
    - Depends on: Microsoft.Extensions.Logging
    - Depends on: Microsoft.Extensions.Logging.Abstractions
    - Depends on: Microsoft.Extensions.Options
    - Depends on: Microsoft.Extensions.Options.ConfigurationExtensions

14. **Microsoft.Extensions.Options** ≥ 10.0.0
    - Depends on: Microsoft.Extensions.DependencyInjection.Abstractions ≥ 10.0.0
    - Depends on: Microsoft.Extensions.Primitives ≥ 10.0.0

15. **Microsoft.Extensions.Configuration** ≥ 10.0.0
16. **Microsoft.Extensions.Configuration.Abstractions** ≥ 10.0.0
17. **Microsoft.Extensions.FileProviders.Abstractions** ≥ 9.0.0
18. **Microsoft.Extensions.Logging** ≥ 10.0.0
19. **Microsoft.Extensions.Logging.Abstractions** ≥ 10.0.0
20. **Microsoft.Extensions.Options.ConfigurationExtensions** ≥ 10.0.0
21. **Microsoft.Extensions.Primitives** ≥ 10.0.0

#### Tier 4: System Libraries (Minimal on .NET 10.0)

For .NET Framework 4.6.2 / .NET Standard 2.0, additional dependencies would include:
- System.Diagnostics.DiagnosticSource
- System.Memory
- System.Buffers
- System.Runtime.CompilerServices.Unsafe
- System.Numerics.Vectors

**However, on .NET 10.0, these are built into the runtime** ✅

### 2.3 ASP.NET Core Dependencies

SpeedReader uses `<FrameworkReference Include="Microsoft.AspNetCore.App" />` which is part of the .NET runtime, not a separate NuGet package.

For **OpenTelemetry.Instrumentation.AspNetCore** on .NET Standard 2.0, it would need:
- Microsoft.AspNetCore.Http.Abstractions
- Microsoft.AspNetCore.Http.Features
- System.Text.Encodings.Web

**On .NET 10.0, these are part of the framework reference** ✅

### 2.4 Total Package Count

**Minimum packages to create for full OpenTelemetry support:**

| Category | Package Count |
|----------|--------------|
| OpenTelemetry core packages | 8 |
| Microsoft.Extensions packages | ~12 |
| **Total (net10.0 target)** | **~20 packages** |

**Note:** This assumes .NET 10.0 is available. For .NET Standard 2.0, add ~5 more System.* packages.

---

## 3. Licensing Analysis

### 3.1 OpenTelemetry Packages

**License:** Apache-2.0 ✅
**DFSG-Compliant:** Yes
**Source:** [OpenTelemetry .NET GitHub](https://github.com/open-telemetry/opentelemetry-dotnet)

All OpenTelemetry packages consistently use Apache-2.0 license with SPDX headers:
```csharp
// SPDX-License-Identifier: Apache-2.0
```

**Project Status:**
- ✅ Very active development (3,654 commits, 3.6k stars)
- ✅ Stable 1.x releases across all signals (Logs, Metrics, Traces)
- ✅ Regular release cadence (weekly maintainer meetings)
- ✅ Production-ready (signed releases with Sigstore attestation)

### 3.2 Microsoft.Extensions Packages

**License:** MIT ✅
**DFSG-Compliant:** Yes

All Microsoft.Extensions packages use the MIT license:
- Microsoft.Extensions.Configuration
- Microsoft.Extensions.DependencyInjection.Abstractions
- Microsoft.Extensions.Hosting.Abstractions
- Microsoft.Extensions.Logging.*
- Microsoft.Extensions.Options

**Sources:**
- [Microsoft.Extensions.Configuration 9.0.9](https://www.nuget.org/packages/Microsoft.Extensions.Configuration/)
- [Microsoft.Extensions.Options 10.0.1](https://www.nuget.org/packages/microsoft.extensions.options/)

### 3.3 System.* Packages (Runtime Dependencies)

**License:** MIT ✅
**DFSG-Compliant:** Yes

- System.Diagnostics.DiagnosticSource - MIT
- System.Memory - MIT
- System.Buffers - MIT
- System.Runtime.CompilerServices.Unsafe - MIT
- System.Numerics.Vectors - MIT

### 3.4 ASP.NET Core

**License:** MIT (modern .NET) / Apache-2.0 (some components) ✅
**DFSG-Compliant:** Yes

**Source:** [ASP.NET Core License](https://github.com/dotnet/aspnetcore/blob/main/LICENSE.txt)

Modern ASP.NET Core uses MIT license. Some historical components used Apache-2.0.

### 3.5 License Compatibility Summary

| License Type | Package Count | DFSG-Compliant | Compatible with Apache-2.0 |
|--------------|--------------|----------------|---------------------------|
| Apache-2.0 | 8 | ✅ Yes | ✅ Yes (same license) |
| MIT | 12+ | ✅ Yes | ✅ Yes (permissive) |

**Result: ALL DEPENDENCIES ARE DFSG-COMPLIANT** ✅

**No licensing blockers for Debian packaging.**

---

## 4. Packaging Complexity Assessment

### 4.1 Number of Packages Required

**For .NET 10.0 target (SpeedReader's case):**
- Minimum: ~20 packages (OpenTelemetry + Microsoft.Extensions)
- Additional infrastructure: .NET 10.0 runtime (not in Debian)

**For .NET Standard 2.0 target (broader compatibility):**
- Minimum: ~25 packages (includes System.* packages)
- Additional infrastructure: .NET runtime or Mono

### 4.2 Packages Already in Debian

**None of the required packages exist in Debian:**

| Package Category | In Debian? | Notes |
|-----------------|------------|-------|
| OpenTelemetry.* | ❌ No | 0/8 packages |
| Microsoft.Extensions.* | ❌ No | 0/12 packages |
| System.* (.NET libs) | ❌ No | Built into .NET runtime |
| .NET runtime | ❌ No | Only via Microsoft's repo |

### 4.3 Problematic Dependencies

#### Native Code Dependencies

**None.** All OpenTelemetry .NET packages are pure managed code. HOWEVER:
- SpeedReader's OTHER dependencies include ONNX Runtime (native code)
- ONNX Runtime is already being handled separately in SpeedReader

#### Non-DFSG Licenses

**None identified.** All packages use MIT or Apache-2.0.

#### Beta/Preview Packages

**One beta package:**
- OpenTelemetry.Instrumentation.Process (1.14.0-beta.2)

**Impact:** Debian typically prefers stable releases. This package could be:
1. Dropped from SpeedReader (non-critical instrumentation)
2. Included with documentation noting beta status
3. Wait for stable release

#### Circular Dependencies

**Minimal circular dependencies within Microsoft.Extensions.*** These are well-structured and resolvable.

### 4.4 Build System Complexity

Debian's NuGet packaging approach:
- Uses local file repositories at `/usr/share/nupkg` (platform-agnostic) and `/usr/lib/nupkg` (arch-specific)
- Offline builds (no internet access during builds)
- Dependency tracking **unresolved** according to Debian Wiki

**Challenge:** Debian's NuGet packaging policy is incomplete. From the Debian Wiki:
> "Should nupkgs have dependencies? They're just zip files. Is it a nupkg package's responsibility to track the external deps of its own contents?"

**Source:** [Debian NuGet Wiki](https://wiki.debian.org/Teams/DebianMonoGroup/NuGet)

**This suggests Debian doesn't have mature infrastructure for .NET library packaging.**

### 4.5 Packaging Effort Estimate

| Task | Complexity | Estimated Effort |
|------|-----------|-----------------|
| Create .nupkg packages (20) | Medium | 2-4 weeks |
| Write Debian packaging metadata | High | 2-3 weeks |
| Resolve dependency graph | Medium | 1 week |
| Test builds offline | High | 2 weeks |
| Navigate Debian policy for .NET | High | 1-2 weeks |
| **TOTAL** | **High** | **8-12 weeks** |

**This assumes:**
- One maintainer working part-time
- Debian infrastructure accepts .NET packages
- No policy roadblocks

---

## 5. Upstream Activity Assessment

### 5.1 OpenTelemetry .NET Project Status

**Repository:** https://github.com/open-telemetry/opentelemetry-dotnet

**Activity Metrics:**
- ✅ **Very Active:** 3,654 commits on main branch
- ✅ **Strong Community:** 3.6k stars, 863 forks
- ✅ **Maintained:** Regular commits, active issue triage
- ✅ **Professional Support:** Weekly meetings, dedicated maintainers

**Release Cadence:**
- ✅ **Stable Releases:** 1.x releases are production-ready
- ✅ **Signals Status:** Stable across Logs, Metrics, and Traces
- ✅ **Security:** Digitally signed via Sigstore, GitHub attestations
- ✅ **Versioning:** Follows semantic versioning

**Latest Releases:**
- 1.14.0 (2024) - Current stable
- Regular minor/patch releases
- Beta releases for experimental features (e.g., Process instrumentation)

### 5.2 Long-Term Viability

**Outlook: EXCELLENT** ✅

- Part of CNCF (Cloud Native Computing Foundation) OpenTelemetry project
- Cross-language standard (Go, Python, C++, .NET, Java, etc.)
- Industry adoption by major cloud providers
- Active standardization process

**No concerns about upstream abandonment or instability.**

---

## 6. Alternative: Minimal OpenTelemetry Subset

### 6.1 Current SpeedReader Usage (5 packages)

```
1. OpenTelemetry.Exporter.OpenTelemetryProtocol  (OTLP export - CRITICAL)
2. OpenTelemetry.Extensions.Hosting              (ASP.NET Core integration - IMPORTANT)
3. OpenTelemetry.Instrumentation.AspNetCore      (HTTP instrumentation - NICE-TO-HAVE)
4. OpenTelemetry.Instrumentation.Process         (Process metrics - OPTIONAL)
5. OpenTelemetry.Instrumentation.Runtime         (Runtime metrics - OPTIONAL)
```

### 6.2 Minimal Configuration: OTLP Export Only

**Reduced Package Set (2-3 packages):**

```
1. OpenTelemetry.Exporter.OpenTelemetryProtocol  (OTLP export)
2. OpenTelemetry.Extensions.Hosting              (Integration with .NET hosting)
```

**Dependency Reduction:**

| Configuration | Package Count | Debian Packages Needed |
|--------------|--------------|----------------------|
| Full (current) | 5 packages | ~20 packages |
| Minimal OTLP | 2 packages | ~10 packages |
| **Savings** | **-60%** | **~50% reduction** |

**What You Lose:**
- ❌ Automatic ASP.NET Core HTTP tracing (can implement manually)
- ❌ Automatic process metrics (can add later)
- ❌ Automatic runtime metrics (can add later)

**What You Keep:**
- ✅ OTLP export (core functionality)
- ✅ Manual instrumentation capability
- ✅ Metrics and traces export to collectors

### 6.3 Alternative: Drop OpenTelemetry Entirely

**Impact on SpeedReader:**

| Feature | Impact | Workaround |
|---------|--------|-----------|
| Telemetry export | Lost | Use logging, custom metrics |
| Distributed tracing | Lost | Manual trace headers |
| Metrics | Lost | Use built-in ASP.NET Core metrics |
| OTLP protocol | Lost | Implement custom exporters |

**Packaging Burden Reduction:**
- From ~20 packages → 0 packages ✅

**User Impact:**
- SpeedReader would lose built-in observability
- Users would need to add telemetry separately
- Reduces out-of-box monitoring capabilities

**Recommendation:**
- **If OpenTelemetry is core to SpeedReader's value proposition:** Keep it
- **If telemetry is nice-to-have:** Consider dropping for Debian packaging

### 6.4 Hybrid Approach: Optional OpenTelemetry

**Strategy:**
1. Create a base `speedreader` package **without** OpenTelemetry
2. Create an optional `speedreader-telemetry` package **with** OpenTelemetry
3. Let users decide if they want the extra dependencies

**Debian Package Structure:**
```
speedreader                    (base, no telemetry)
speedreader-telemetry          (optional, includes OpenTelemetry)
  ↓ Depends on:
  - opentelemetry-dotnet-exporter-otlp
  - opentelemetry-dotnet-extensions-hosting
  - ... (all OpenTelemetry deps)
```

**Benefits:**
- ✅ Reduces barrier to entry for Debian packaging
- ✅ Allows users to opt-in to telemetry
- ✅ Separates concerns (core vs observability)

**Drawbacks:**
- ⚠️ Requires conditional compilation or runtime feature flags
- ⚠️ Increases maintenance burden (two configurations)

---

## 7. Debian-Specific Challenges

### 7.1 Lack of .NET Runtime Infrastructure

**Critical Blocker:**
- Debian has **no official .NET SDK/runtime packages**
- Users must use Microsoft's repository (outside Debian ecosystem)
- This violates Debian's principle of self-contained package management

**Implications:**
- Even if OpenTelemetry packages are created, they can't be used without .NET runtime
- .NET runtime itself would need to be packaged first (MASSIVE effort)
- Alternative: Document dependency on Microsoft's .NET repository

### 7.2 NuGet Packaging Policy Gaps

From Debian Wiki:
> "Should nupkgs have dependencies? ... This question is marked as needing discussion."

**Problem:** Debian hasn't finalized how to handle NuGet dependency resolution.

**Impact:**
- Unclear how to properly package NuGet libraries
- Potential policy changes could invalidate packaging work
- May require engaging with Debian Mono Group to clarify policy

### 7.3 Mono vs .NET Core/10

**Debian's .NET support is Mono-focused:**
- Mono is legacy, .NET 5+ is the modern stack
- OpenTelemetry .NET targets modern .NET, not Mono
- Targeting Mono would require significant compatibility work

**SpeedReader uses .NET 10.0**, which is cutting-edge and has NO Mono compatibility.

---

## 8. Recommendations

### 8.1 For Debian Packaging Feasibility

**Tier 1: Keep Full OpenTelemetry (Status Quo)**
- **Effort:** High (~8-12 weeks packaging effort)
- **Packages:** ~20 packages
- **Blockers:** .NET runtime infrastructure missing
- **Recommendation:** ⚠️ **NOT RECOMMENDED** unless Debian .NET infrastructure is established

**Tier 2: Minimal OpenTelemetry Subset**
- **Effort:** Medium (~4-6 weeks)
- **Packages:** ~10 packages (OTLP exporter + hosting extensions only)
- **Trade-offs:** Lose auto-instrumentation, keep core export
- **Recommendation:** ✅ **RECOMMENDED** if OpenTelemetry is important

**Tier 3: Drop OpenTelemetry Entirely**
- **Effort:** Zero packaging effort
- **Packages:** 0 packages
- **Trade-offs:** Lose built-in observability, simpler packaging
- **Recommendation:** ✅ **RECOMMENDED** if simplicity is prioritized

**Tier 4: Hybrid (Base + Optional Telemetry)**
- **Effort:** Medium-High (packaging + code changes)
- **Packages:** ~20 packages (but optional)
- **Trade-offs:** More complexity, better user choice
- **Recommendation:** ⚠️ Consider if targeting multiple package systems (apt + others)

### 8.2 Immediate Actions

**If keeping OpenTelemetry:**

1. ✅ **Reduce to minimal subset:**
   - Keep: `OpenTelemetry.Exporter.OpenTelemetryProtocol`
   - Keep: `OpenTelemetry.Extensions.Hosting`
   - Drop: `OpenTelemetry.Instrumentation.AspNetCore` (optional)
   - Drop: `OpenTelemetry.Instrumentation.Process` (beta, optional)
   - Drop: `OpenTelemetry.Instrumentation.Runtime` (optional)

2. ✅ **Engage with Debian Mono Group:**
   - Clarify NuGet packaging policy
   - Understand .NET runtime packaging plans
   - Determine if .NET 10.0 can be packaged or if fallback to .NET Standard 2.0 needed

3. ⚠️ **Document dependency on Microsoft's .NET repository:**
   - Accept that users will need to install .NET from Microsoft's repo
   - Provide clear installation instructions

**If dropping OpenTelemetry:**

1. ✅ **Remove from `Directory.Packages.props`:**
   - Remove all 5 OpenTelemetry package references

2. ✅ **Update code to remove telemetry:**
   - Remove OpenTelemetry setup in `Program.cs`
   - Remove instrumentation registrations

3. ✅ **Document the change:**
   - Note that Debian builds lack telemetry (for now)
   - Provide guidance for adding telemetry post-install (via NuGet)

### 8.3 Long-Term Strategy

**For SpeedReader Maintainers:**

1. **Multi-distribution strategy:**
   - Create different build flavors:
     - `.deb` (Debian/Ubuntu) - Minimal/no OpenTelemetry
     - `.rpm` (Fedora/RHEL) - May have better .NET support
     - Standalone binary - Full OpenTelemetry support

2. **Feature flags:**
   - Use `#if` directives or runtime configuration to make OpenTelemetry optional
   - Allows same codebase to build with/without telemetry

3. **Contribute to Debian .NET infrastructure:**
   - Long-term: Help establish .NET packaging standards in Debian
   - Collaborate with Debian Mono Group on policy

**For Debian Ecosystem:**

Consider advocating for:
1. Official .NET SDK/runtime packages in Debian
2. Finalized NuGet packaging policy
3. Infrastructure for modern .NET library packaging

---

## 9. Conclusion

### 9.1 Summary of Findings

| Aspect | Finding | Status |
|--------|---------|--------|
| **Debian Availability** | No .NET OpenTelemetry packages | ❌ |
| **Dependencies** | ~20 packages needed (full), ~10 (minimal) | ⚠️ High |
| **Licensing** | All MIT/Apache-2.0 (DFSG-compliant) | ✅ |
| **Complexity** | High (8-12 weeks effort) | ⚠️ |
| **Upstream** | Active, stable, well-maintained | ✅ |
| **Infrastructure** | Debian lacks .NET runtime packages | ❌ Critical |

### 9.2 Final Recommendation

**For SpeedReader Debian packaging:**

✅ **RECOMMENDED: Drop OpenTelemetry for Debian builds**

**Rationale:**
1. Debian lacks .NET runtime infrastructure (critical blocker)
2. Packaging ~20 packages is resource-intensive for minimal benefit
3. OpenTelemetry can be added post-install via NuGet by users who want it
4. SpeedReader's core OCR functionality doesn't depend on telemetry
5. Reduces Debian packaging scope significantly

**Implementation:**
- Use build configurations to exclude OpenTelemetry from Debian builds
- Document that telemetry is available in other distributions
- Provide instructions for manual OpenTelemetry installation via NuGet

**Alternative (if telemetry is critical):**
- Package minimal subset (OTLP exporter + hosting extensions only)
- Accept dependency on Microsoft's .NET repository
- Document ~10 packages need to be created
- Estimate 4-6 weeks of packaging effort

### 9.3 Next Steps

**Immediate (If dropping OpenTelemetry):**
1. Create Debian build configuration in `Directory.Build.props`
2. Add preprocessor directives to exclude OpenTelemetry code
3. Update Debian packaging scripts to use this configuration
4. Document the limitation in Debian package description

**Immediate (If keeping minimal OpenTelemetry):**
1. Reduce dependencies to OTLP exporter + hosting extensions only
2. Begin packaging Microsoft.Extensions.* libraries
3. Engage with Debian Mono Group on NuGet policy
4. Plan for 4-6 weeks of packaging work

**Long-term:**
1. Monitor Debian's .NET packaging infrastructure development
2. Consider contributing to Debian .NET ecosystem
3. Re-evaluate OpenTelemetry inclusion when infrastructure matures

---

## References

### Debian Package Sources
- [Debian Package Search - OpenTelemetry](https://packages.debian.org/search?keywords=opentelemetry)
- [Debian Package Search - .NET/dotnet](https://packages.debian.org/search?keywords=dotnet)
- [Debian Wiki - NuGet Packaging](https://wiki.debian.org/Teams/DebianMonoGroup/NuGet)

### NuGet Package Sources
- [OpenTelemetry.Exporter.OpenTelemetryProtocol 1.14.0](https://www.nuget.org/packages/OpenTelemetry.Exporter.OpenTelemetryProtocol/1.14.0)
- [OpenTelemetry.Extensions.Hosting 1.13.1](https://www.nuget.org/packages/OpenTelemetry.Extensions.Hosting/1.13.1)
- [OpenTelemetry.Instrumentation.AspNetCore 1.14.0](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.AspNetCore/1.14.0)
- [Microsoft.Extensions.* packages on NuGet](https://www.nuget.org/packages?q=Microsoft.Extensions)

### Upstream Sources
- [OpenTelemetry .NET GitHub](https://github.com/open-telemetry/opentelemetry-dotnet)
- [OpenTelemetry OTLP Exporter Documentation](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry.Exporter.OpenTelemetryProtocol/README.md)
- [.NET Observability with OpenTelemetry](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/observability-with-otel)

### .NET on Debian
- [Install .NET on Debian](https://learn.microsoft.com/en-us/dotnet/core/install/linux-debian)
- [dotnet SDK issue #48973 - libicu76 incompatibility](https://github.com/dotnet/sdk/issues/48973)
- [ASP.NET Core License](https://github.com/dotnet/aspnetcore/blob/main/LICENSE.txt)

### Licensing References
- [Apache-2.0 License](https://www.apache.org/licenses/LICENSE-2.0.txt)
- [MIT License](https://opensource.org/licenses/MIT)
- [DFSG - Debian Free Software Guidelines](https://www.debian.org/social_contract#guidelines)

---

**End of Assessment**
