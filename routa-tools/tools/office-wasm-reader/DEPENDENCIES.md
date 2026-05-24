# Office WASM Reader Dependency Lock

The reader intentionally matches the dependency families observed in the extracted Codex/Walnut webview bundle.

| Dependency | Pinned Version | Why |
| --- | --- | --- |
| `.NET browser-wasm runtime` | `9.0.14` | `dotnet.native.wfd2lrj4w6.wasm` contains `Microsoft.NETCore.App.Runtime.Mono.browser-wasm/9.0.14`. |
| `ClosedXML` | `0.105.0` | Used as a lazy XLSX formula semantics adapter when formula cells have no cached `<v>` value. The OpenXML reader remains the protocol source of truth. |
| `ClosedXML.Parser` | `2.0.0` | Transitive dependency required by `ClosedXML 0.105.0` for formula parsing. |
| `DocumentFormat.OpenXml` | `3.4.1` | Updated from `3.3.0` (Walnut bundle baseline) to `3.4.1` for Q3 2025 Office schema support, MP4 media type, and WASM JIT/AOT size reductions. Walnut parity tests guard against behavioral regressions. |
| `DocumentFormat.OpenXml.Framework` | `3.4.1` | Updated alongside `DocumentFormat.OpenXml` to the same `3.4.1` release line. |
| `ExcelNumberFormat` | `1.1.0` | Transitive `ClosedXML` dependency for Excel number format semantics. |
| `RBush.Signed` | `4.0.0` | Transitive `ClosedXML` dependency. |
| `SixLabors.Fonts` | `1.0.0` | Transitive `ClosedXML` dependency used by ClosedXML workbook/style handling. |
| `System.IO.Packaging` | `8.0.1` | Extracted bundle contains `System.IO.Packaging.wasm`; `DocumentFormat.OpenXml.Framework 3.3.0` depends on `System.IO.Packaging >= 8.0.1`. |
| `Google.Protobuf` | `3.31.0` | Extracted `Google.Protobuf.wasm` contains assembly version evidence `3.31.0.0`. |
| `System.*` runtime assemblies | `9.0.14 runtime pack` | Extracted `System.*.wasm` assemblies are BCL/runtime assemblies from the browser-wasm runtime pack, not separate NuGet package references. |

Do not replace these with JS ZIP/XML parser packages when validating compatibility. The point of this POC is to exercise the same OpenXML SDK and .NET browser-wasm runtime surface as the extracted bundle while implementing Routa-owned reader code. ClosedXML is intentionally scoped to formula-value backfill; it should not replace the low-level OpenXML protocol reader without a separate parity review.

Version declarations live in `Directory.Packages.props`; the project enables NuGet lock-file restore so `dotnet restore` generates `packages.lock.json` once a .NET 9 SDK is available locally.
