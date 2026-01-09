# DataSpecter

A high-performance binary forensic analysis tool for cybersecurity professionals and malware researchers. DataSpecter enables deep inspection of binary files, pattern detection, and file comparison using advanced algorithms optimized for large-scale analysis.

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=.net)
![WPF](https://img.shields.io/badge/UI-WPF-blue)
![License](https://img.shields.io/badge/license-MIT-green)

## Core Capabilities

### Binary Analysis Engine

- **Suffix Array Indexing**: O(M log N) pattern search using optimized suffix array construction with LCP arrays
- **Memory-Mapped I/O**: Efficient handling of multi-gigabyte files without loading entire content into RAM
- **Parallel Processing**: Multi-threaded index generation with real-time progress tracking
- **Persistent Caching**: Index serialization and versioned storage for instant reuse

### Pattern Detection

- **Unified Search Interface**: Supports both ASCII text and hexadecimal byte pattern searches
- **Automatic Signature Discovery**: LCP-based detection of longest repeated substrings within files
- **Cross-File Analysis**: Identify longest common substrings between files for similarity assessment
- **Fallback Mechanisms**: Graceful degradation to naive search for non-indexed files

### Visualization & Forensics

- **Virtualized Hex Viewer**: Smooth rendering of large binaries with configurable chunk loading (16KB default)
- **Entropy Analysis**: Shannon entropy visualization to identify encrypted, compressed, or obfuscated regions
- **Structure Parsing**: Automatic format detection and parsing for PE executables and PDF documents
- **Dual-Pane Comparison**: Side-by-side hex display with synchronized highlighting of matched regions

### Fuzzy Hashing

- **ssdeep Integration**: Context-triggered piecewise hashing for similarity detection
- **Comparison Scoring**: Numerical similarity metrics for forensic correlation analysis

## Architecture

```
DataSpecter/
├── DataSpecter.Core/           # Domain models, interfaces, business logic
│   ├── Interfaces/             # Service abstractions (ISuffixArrayService, IEntropyService, etc.)
│   └── Models/                 # Core entities (BinaryDataSource, StructureItem)
├── DataSpecter.Infrastructure/ # Algorithm implementations, parsers, services
│   ├── Services/               # Suffix array, entropy, fuzzy hash, LCS implementations
│   └── Parsers/                # PE, PDF structure parsers
├── DataSpecter.UI/             # WPF application
│   ├── Views/                  # XAML views (Analysis, Comparison, Entropy)
│   ├── ViewModels/             # MVVM ViewModels with ICommand bindings
│   └── Controls/               # Custom controls (HexViewer, ComparisonVisualizer)
└── DataSpecter.Tests/          # Unit and performance tests
```

**Design Patterns**: MVVM architecture with dependency injection, repository pattern for data access, strategy pattern for algorithm selection.

## Technical Stack

- **.NET 8.0** - Cross-platform runtime with modern C# features
- **WPF** - Rich desktop UI framework with XAML databinding
- **ssdeep** - Fuzzy hashing library for context-triggered piecewise hashing
- **NuGet Packages**:
  - `CommunityToolkit.Mvvm` - Modern MVVM helpers (RelayCommand, ObservableProperty)
  - `Ookii.Dialogs.Wpf` - Native Windows file/folder dialogs

## Getting Started

### Prerequisites

- .NET 8.0 SDK or later
- Windows 10/11 (WPF requirement)
- Visual Studio 2022 or Rider (recommended)

### Build & Run

```bash
# Clone the repository
git clone https://github.com/adan-ayaz-stan/data-specter.git
cd data-specter

# Restore dependencies
dotnet restore

# Build solution
dotnet build

# Run the application
dotnet run --project DataSpecter.UI
```

### Running Tests

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

## Key Algorithms

### Suffix Array Construction

Implements optimized SA-IS (Suffix Array Induced Sorting) algorithm with parallel processing for O(n) construction time. LCP array computed using Kasai's algorithm in linear time.

### Pattern Search

Binary search on suffix array provides O(M log N) search complexity where M is pattern length and N is file size. Significantly outperforms naive O(MN) search for indexed files.

### Longest Common Substring

Uses merged suffix arrays with LCP-based sliding window to find maximal shared sequences between files in O(n+m) time complexity.

### Entropy Calculation

Shannon entropy computed over configurable sliding windows (default 1024 bytes) to identify data randomness. Values approaching 8.0 indicate high entropy (encryption/compression).

## Use Cases

- **Malware Analysis**: Identify packed sections, detect code injection, find embedded payloads
- **Digital Forensics**: Compare file versions, detect tampering, extract hidden data
- **Incident Response**: Rapid pattern matching across large binary datasets
- **Threat Intelligence**: Extract and catalog malware signatures for IOC generation
- **Reverse Engineering**: Analyze file structure, identify common libraries, detect code reuse

## Performance

- Indexes 100MB files in ~2-5 seconds (varies by hardware)
- Searches indexed files in <100ms for typical patterns
- Supports files up to 4GB with virtualized rendering
- Entropy analysis: ~50-100 MB/s throughput

## Contributing

Contributions are welcome! Please follow these guidelines:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/your-feature`)
3. Follow C# coding conventions and MVVM patterns
4. Add unit tests for new functionality
5. Ensure all tests pass (`dotnet test`)
6. Submit a pull request with clear description

## Roadmap

- [ ] GPU-accelerated suffix array construction via CUDA/OpenCL
- [ ] Support for ELF and Mach-O binary formats
- [ ] Machine learning-based anomaly detection
- [ ] Export functionality for analysis reports (JSON, CSV, HTML)
- [ ] Multi-file batch processing pipeline
- [ ] YARA rule integration for signature matching

## License

MIT License - see [LICENSE](LICENSE) file for details.

## Author

**Adan Ayaz** - Binary analysis and forensic tooling specialist

---

_Built for the cybersecurity community. For questions or collaboration, open an issue or submit a PR._
