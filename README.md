# DataSpecter

A high-performance binary forensic analysis tool for cybersecurity professionals and malware researchers. DataSpecter enables deep inspection of binary files, pattern detection, and file comparison using advanced algorithms optimized for large-scale analysis.

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=.net)
![WPF](https://img.shields.io/badge/UI-WPF-blue)
![License](https://img.shields.io/badge/license-MIT-green)

## Core Capabilities

### Binary Analysis Engine

- **Suffix Array Indexing**: O(M log N) pattern search using parallel radix sort construction with LCP arrays (100MB file limit for in-memory indexing)
- **Memory-Mapped I/O**: Efficient handling of files without loading entire content into RAM using `MemoryMappedFile` and view accessors
- **Parallel Processing**: Multi-threaded doubling algorithm for suffix array generation with real-time progress tracking
- **Persistent Caching**: In-memory LRU cache for recently generated suffix arrays (5-entry limit)

### Pattern Detection

- **Unified Search Interface**: Supports both ASCII text and hexadecimal byte pattern searches via binary search on suffix arrays
- **Cross-File Analysis**: Identify longest common substrings between files using naive sliding window comparison (4KB search window, 1MB file limit for performance)
- **Fallback Mechanisms**: Graceful degradation to sequential search when files aren't indexed

### Visualization & Forensics

- **Virtualized Hex Viewer**: WPF virtualized ListBox rendering with configurable chunk loading (16KB default page size)
- **Entropy Analysis**: Shannon entropy calculation over configurable sliding windows (1024-byte default) for identifying randomness patterns
- **Structure Parsing**: Automatic format detection and parsing for PE executables (DOS/NT headers, sections) and PDF documents (header, objects, trailer)
- **Dual-Pane Comparison**: Side-by-side hex display with offset-based highlighting for matched regions

### Fuzzy Hashing

- **Custom SSDEEP Implementation**: Context-triggered piecewise hashing using rolling hash and block-based signatures
- **Comparison Scoring**: Edit distance-based similarity scoring (0-100 scale) for hash comparison

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

**Design Patterns**: MVVM architecture with Microsoft DI container, service layer abstraction via interfaces, observable collections for real-time UI updates.

## Technical Stack

- **.NET 8.0** - Cross-platform runtime with modern C# features
- **WPF** - Rich desktop UI framework with XAML databinding and virtualization
- **ILGPU** - GPU compute library (v1.5.1) with CUDA/CPU accelerator support for future optimization
- **NuGet Packages**:
  - `CommunityToolkit.Mvvm` (v8.4.0) - Modern MVVM helpers (RelayCommand, ObservableProperty)
  - `Microsoft.Extensions.DependencyInjection` (v10.0.1) - Service container for DI
  - `WPF-UI` (v4.1.0) - Modern UI controls and styling
  - `dnYara` (v2.1.0) - YARA pattern matching library (integrated but not actively used)

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

Implements parallel doubling algorithm with radix sort optimization. Constructs suffix array in O(n log² n) time with multi-threaded sorting of (first, second) tuple pairs. LCP array computed using Kasai's algorithm in linear time with rank array preprocessing. Optimized for files up to 100MB; larger files require chunked processing.

### Pattern Search

Binary search on suffix array provides O(M log N) search complexity where M is pattern length and N is file size. Performs two binary searches (lower bound and upper bound) to find all occurrences. Uses memory-mapped file reads during comparison to avoid loading full file.

### Longest Common Substring

Simple naive sliding window comparison: iterates through first 4KB of each file (limited to 1MB total per file) using nested loop pattern matching. O(n×m) complexity—suitable for prototype but requires optimization for production (recommend suffix tree or enhanced suffix array merging).

### Entropy Calculation

Shannon entropy computed per chunk using byte frequency distribution: `H = -Σ(p_i × log₂(p_i))` where p_i is probability of byte value i. Processes file sequentially in 1024-byte chunks, returns entropy array for visualization.

### Fuzzy Hashing (SSDEEP-like)

Custom implementation using rolling hash (FNV-based) with context-triggered piecewise hashing. Generates two signatures at different block sizes. Comparison uses edit distance algorithm to compute similarity score (0-100).

## Use Cases

- **Malware Analysis**: Identify packed sections, detect code injection, find embedded payloads
- **Digital Forensics**: Compare file versions, detect tampering, extract hidden data
- **Incident Response**: Rapid pattern matching across large binary datasets
- **Threat Intelligence**: Extract and catalog malware signatures for IOC generation
- **Reverse Engineering**: Analyze file structure, identify common libraries, detect code reuse

## Performance

- Indexes files up to 100MB (hard limit for in-memory indexing)
- Indexing speed: ~20-30 MB/s for suffix array + LCP construction (single-threaded equivalent)
- Searches indexed files in <100ms for typical patterns via O(M log N) binary search
- Hex viewer: Virtualized rendering with 16KB page size enables smooth navigation of large files
- Entropy analysis: Sequential chunk processing, configurable 1024-byte windows
- LCS comparison: Limited to 4KB search space within 1MB file segments (naive algorithm)

## Contributing

Contributions are welcome! Please follow these guidelines:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/your-feature`)
3. Follow C# coding conventions and MVVM patterns
4. Add unit tests for new functionality
5. Ensure all tests pass (`dotnet test`)
6. Submit a pull request with clear description

## RoaActivate GPU-accelerated suffix array construction via ILGPU (infrastructure in place with `GpuSuffixArrayService`)

- [ ] Remove 100MB indexing limit: implement disk-based suffix array construction (SA-IS or DC3)
- [ ] Optimize LCS algorithm: replace naive O(n×m) with generalized suffix tree or enhanced suffix array merging
- [ ] Support for ELF and Mach-O binary formats (expand parser infrastructure)
- [ ] Index persistence: serialize suffix arrays to disk for cross-session reuse
- [ ] Longest repeated substring detection: add LCP-based anomaly finder to UI
- [ ] YARA rule integration: activate dnYara package for signature matching workflows
- [ ] Export functionality for analysis reports (JSON, CSV, HTML)
- [ ] Multi-file batch processing pipeline with progress track (JSON, CSV, HTML)
- [ ] Multi-file batch processing pipeline
- [ ] YARA rule integration for signature matching

## License

MIT License - see [LICENSE](LICENSE) file for details.

## Author

**Adan Ayaz** - Binary analysis and forensic tooling specialist

---

_Built for the cybersecurity community. For questions or collaboration, open an issue or submit a PR._
