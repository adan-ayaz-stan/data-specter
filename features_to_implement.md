# Core Forensic & Algorithmic Features

### Automatic Signature Discovery (Longest Repeated Substring)
- Detects the longest repeating byte sequence in a binary file without prior knowledge.
- Ideal for uncovering embedded malware payloads, obfuscated signatures, or repeated exploit patterns.
- Powered by suffix array + LCP analysis in linear time.

### Binary-Level File Similarity Detection (Longest Common Substring)
- Identifies the largest shared byte sequence between two files.
- Proves code injection, cloning, or tampering even when files are renamed or padded.
- Visual side-by-side highlighting of shared regions.

### Instant Pattern Occurrence Counting on Massive Files
- Counts how many times a byte or hex pattern appears in multi-gigabyte files instantly.
- Uses suffix array binary search instead of naive scanning.
- Returns both count and exact offsets.

### Hex + ASCII Pattern Search (Unified)
- Accepts both ASCII strings and raw hex byte patterns in the same search box.
- Highlights every match with offset-accurate navigation.

# Visualization & Analyst Experience Features

### High-Performance Virtualized Hex Viewer
- Smooth scrolling even on multi-GB files.
- Classic forensic layout: Offset | Hex Bytes | ASCII.
- Zero-lag jump-to-offset and synchronized highlighting.

### Entropy Visualization for Obfuscation Detection
- Displays byte-level entropy to visually spot encrypted or compressed regions.
- Helps differentiate normal data from packed malware payloads.

### Linked Results & Visual Evidence Flow
- Clicking any result auto-scrolls and highlights the corresponding bytes.
- All views stay synchronized (search results, LRS, LCS).

### Diff View with Visual Proof of Similarity
- Side-by-side binary comparison with shared blocks color-linked.
- Dimmed irrelevant data, spotlighting forensic evidence.

# Performance & Systems-Level Features

### Algorithm Selector (Educational + Practical)
- Toggle between naive and optimized suffix array builds.
- Displays build time, memory usage, and performance stats.
- Demonstrates deep algorithmic understanding.

### Index-Once, Query-Forever Architecture
- Builds a suffix index once and enables near-instant queries afterward.
- Supports repeated searches, comparisons, and analyses without recomputation.

### Large-File Safe Design (GB-Scale)
- Handles files far larger than RAM using memory-mapped access.
- Designed for real forensic workloads, not toy examples.

### Background Indexing with Live Progress
- Non-blocking indexing with progress indicators.
- UI remains fully responsive during heavy computation.

# Persistence & Evidence Handling

### Persistent Forensic Indexes
- Indexes survive app restarts.
- No need to rebuild suffix arrays each session.

### Forensic Integrity Metadata
- Tracks file hashes, index version, build algorithm, and timestamps.
- Supports reproducibility and chain-of-custody narratives.

### Exportable Evidence Reports
- Offsets, signatures, and matches exportable to CSV/TXT.
- Designed for legal reports and academic submission.
- Professional UX & Tooling Features

### Dark-Mode Cybersecurity UI
- Purpose-built interface aligned with professional forensic tools.
- High contrast highlighting for evidence visibility.

### Drag-and-Drop Ingestion
- Zero-friction entry point for analysts.

### Clean, Modular Architecture
- Algorithms, UI, persistence, and services cleanly separated.
- Easy to extend (more algorithms, formats, or visualizations).
