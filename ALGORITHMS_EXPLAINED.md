# DataSpecter: Algorithmic Foundation and Technical Deep Dive

## Executive Summary

DataSpecter is a forensic binary analysis tool built upon powerful string processing algorithms from computer science. At its core, the application uses **Suffix Arrays** and **LCP (Longest Common Prefix) Arrays** to enable lightning-fast pattern searching, anomaly detection, and file similarity analysis. This document explains the theory, implementation, and practical applications of these algorithms within the DataSpecter ecosystem.

---

## 1. Core Data Structures

### 1.1 Suffix Array (SA)

#### **What is a Suffix Array?**

A **Suffix Array** is a sorted array of all suffixes of a string (or in our case, a byte sequence). For a byte array of length `n`, there are `n` suffixes, each starting at position `i` (0 ≤ i < n) and extending to the end of the array.

The Suffix Array stores the **starting positions** of these suffixes in lexicographically sorted order.

#### **Example:**

Consider the byte string: `"banana"` (treating it as ASCII bytes)

| Index | Suffix |
| ----- | ------ |
| 0     | banana |
| 1     | anana  |
| 2     | nana   |
| 3     | ana    |
| 4     | na     |
| 5     | a      |

When sorted lexicographically:

| Sorted Rank | Suffix | Starting Position |
| ----------- | ------ | ----------------- |
| 0           | a      | 5                 |
| 1           | ana    | 3                 |
| 2           | anana  | 1                 |
| 3           | banana | 0                 |
| 4           | na     | 4                 |
| 5           | nana   | 2                 |

The **Suffix Array** would be: `[5, 3, 1, 0, 4, 2]`

#### **Why Use Suffix Arrays?**

Suffix Arrays enable:

- **Fast pattern searching**: O(M log N) where M is pattern length, N is file size
- **Space efficiency**: Only 4n bytes for typical 32-bit integers (vs. suffix trees at ~10n-20n)
- **Memory locality**: Better cache performance than pointer-based structures
- **Parallelization**: Construction can be optimized with parallel algorithms

### 1.2 LCP Array (Longest Common Prefix Array)

#### **What is the LCP Array?**

The **LCP Array** stores the length of the longest common prefix between consecutive suffixes in the sorted suffix array.

Formally: `LCP[i]` = length of the longest common prefix between suffix at `SA[i]` and suffix at `SA[i-1]`

By convention, `LCP[0] = 0` (no previous suffix to compare).

#### **Example (continuing from above):**

| SA Index | Starting Position (SA[i]) | Suffix | LCP[i] |
| -------- | ------------------------- | ------ | ------ |
| 0        | 5                         | a      | 0      |
| 1        | 3                         | ana    | 1      |
| 2        | 1                         | anana  | 3      |
| 3        | 0                         | banana | 0      |
| 4        | 4                         | na     | 0      |
| 5        | 2                         | nana   | 2      |

- LCP[0] = 0 (by convention)
- LCP[1] = 1 ("a" vs "ana" share "a")
- LCP[2] = 3 ("ana" vs "anana" share "ana")
- LCP[3] = 0 ("anana" vs "banana" share nothing)
- LCP[4] = 0 ("banana" vs "na" share nothing)
- LCP[5] = 2 ("na" vs "nana" share "na")

The **LCP Array** would be: `[0, 1, 3, 0, 0, 2]`

#### **Why Use the LCP Array?**

The LCP Array enables:

- **Linear-time LRS detection**: Find the maximum value in the LCP array
- **Efficient similarity queries**: Identify common substrings between files
- **Range queries**: Determine common prefixes within suffix ranges
- **Pattern matching optimization**: Skip redundant comparisons

---

## 2. Algorithm Implementations in DataSpecter

### 2.1 Suffix Array Construction

DataSpecter implements two versions of suffix array construction:

#### **2.1.1 CPU-Based Implementation** (`SuffixArrayService.cs`)

The implementation uses an **optimized doubling algorithm** with parallel radix sort:

**Algorithm: Doubling with Parallel Sorting**

```
Time Complexity: O(n log² n)
Space Complexity: O(n)

1. Initialize:
   - Create suffix array SA[i] = i (all suffix positions)
   - Create rank array rank[i] = data[i] (initial ranking by first byte)

2. Iterate with doubling (k = 1, 2, 4, 8, ...):
   For each position i:
     - Create pair: (rank[SA[i]], rank[SA[i] + k])
     - Use -1 if SA[i] + k >= n (no character beyond end)

3. Sort all pairs using comparison:
   - Primary key: rank[SA[i]]
   - Secondary key: rank[SA[i] + k]

4. Re-rank suffixes based on sorted order:
   - Consecutive identical pairs get same rank
   - Different pairs get incrementing ranks

5. Repeat until all ranks are unique or k >= n

6. Return final SA[]
```

**Code Highlights:**

```csharp
// Initial ranking based on byte values
Parallel.For(0, n, i =>
{
    sa[i] = i;
    rank[i] = data[i];
});

// Doubling: sort by first 2^k characters
for (int k = 1; k < n; k *= 2)
{
    // Create (first, second, index) tuples
    var pairs = new (int first, int second, int index)[n];
    Parallel.For(0, n, i =>
    {
        int first = rank[sa[i]];
        int second = (sa[i] + k < n) ? rank[sa[i] + k] : -1;
        pairs[i] = (first, second, sa[i]);
    });

    // Sort pairs
    Array.Sort(pairs, (a, b) =>
    {
        if (a.first != b.first)
            return a.first.CompareTo(b.first);
        return a.second.CompareTo(b.second);
    });

    // Re-rank based on sorted order
    tempRank[sa[0]] = 0;
    for (int i = 1; i < n; i++)
    {
        bool same = (rank[prev] == rank[curr]) &&
                    (prevSecond == currSecond);
        tempRank[curr] = same ? tempRank[prev] : tempRank[prev] + 1;
    }

    // Early termination if all unique
    if (rank[sa[n - 1]] == n - 1) break;
}
```

**Parallelization Strategy:**

- Initial ranking: Parallel
- Tuple creation: Parallel
- Sorting: Sequential (Array.Sort is highly optimized)
- Re-ranking: Sequential (dependency on previous rank)

#### **2.1.2 GPU-Based Implementation** (`GpuSuffixArrayService.cs`)

The GPU version attempts to leverage ILGPU for acceleration, though full GPU-based suffix array construction is complex:

```csharp
// Uses same doubling algorithm but prepared for GPU execution
// Current implementation uses CPU sorting with ILGPU infrastructure
// Future optimization: GPU radix sort for massive datasets
```

**Current Status:**

- Infrastructure set up with ILGPU Context and CUDA accelerator
- Falls back to CPU if GPU unavailable
- Uses optimized CPU sorting (Array.Sort) for correctness
- Future: Implement GPU parallel radix sort for 2^k-character comparisons

### 2.2 LCP Array Construction (Kasai's Algorithm)

#### **Algorithm: Kasai's Linear-Time LCP Construction**

Kasai's algorithm is a brilliant optimization that computes the LCP array in **O(n) time** after the suffix array is built.

**Key Insight:**
If we know `LCP[rank[i]]`, we can compute `LCP[rank[i+1]]` efficiently because:

- Removing the first character from suffix `i` gives suffix `i+1`
- Therefore, LCP decreases by at most 1

**Algorithm:**

```
Input: data[], SA[], rank[]
Output: LCP[]

1. Initialize:
   h = 0  // running LCP value
   LCP[0] = 0

2. For i = 0 to n-1 (in text order, not SA order):
   if rank[i] > 0:  // Not the lexicographically first suffix
     j = SA[rank[i] - 1]  // Previous suffix in sorted order

     // Extend common prefix from previous h
     while (i + h < n) AND (j + h < n) AND (data[i + h] == data[j + h]):
       h++

     LCP[rank[i]] = h

     if h > 0:
       h--  // Prepare for next iteration (at most -1 from removing first char)

3. Return LCP[]
```

**Time Complexity Proof:**

- The `while` loop increments `h` at most `n` times total (across all iterations)
- Each outer loop iteration decrements `h` by at most 1
- Total operations: O(2n) = O(n)

**Code Implementation:**

```csharp
private int[] BuildLCPArrayParallel(byte[] data, int[] sa, int[] rank, ...)
{
    int n = data.Length;
    int[] lcp = new int[n];
    int h = 0;

    for (int i = 0; i < n; i++)
    {
        if (rank[i] > 0)
        {
            int j = sa[rank[i] - 1];

            // Extend match
            while (i + h < n && j + h < n && data[i + h] == data[j + h])
            {
                h++;
            }

            lcp[rank[i]] = h;
            if (h > 0) h--;
        }
    }

    return lcp;
}
```

**Why Sequential?**
The `h` variable creates a dependency chain that prevents parallelization. However, the algorithm is already linear-time, making it extremely fast even sequentially.

---

## 3. Pattern Searching with Suffix Arrays

### 3.1 Binary Search Algorithm

Once the Suffix Array is built, pattern searching becomes a **binary search problem**.

#### **Algorithm: Suffix Array Pattern Search**

```
Input: pattern[], SA[], dataSource
Output: List of all occurrence positions

Time Complexity: O(M log N) where M = pattern length, N = file size

1. Binary Search for Lower Bound:
   Find the smallest index in SA where suffix >= pattern

2. Binary Search for Upper Bound:
   Find the largest index in SA where suffix <= pattern

3. All SA indices between [lower, upper] are matches
   Extract offsets from SA[lower] to SA[upper]

4. Return sorted list of offsets
```

#### **Detailed Implementation:**

```csharp
public async Task<long[]> SearchAsync(BinaryDataSource dataSource, int[] sa, byte[] pattern)
{
    int n = sa.Length;

    // STEP 1: Binary search for lower bound
    int l = 0, r = n - 1;
    int start = -1;

    while (l <= r)
    {
        int mid = l + (r - l) / 2;
        int suffixStart = sa[mid];

        int cmp = Compare(dataSource, suffixStart, pattern);

        if (cmp >= 0)  // suffix >= pattern
        {
            if (cmp == 0) start = mid;
            r = mid - 1;  // Search left for earlier match
        }
        else
        {
            l = mid + 1;
        }
    }

    if (start == -1) return Array.Empty<long>();

    // STEP 2: Binary search for upper bound
    l = start;
    r = n - 1;
    int end = -1;

    while (l <= r)
    {
        int mid = l + (r - l) / 2;
        int suffixStart = sa[mid];

        int cmp = Compare(dataSource, suffixStart, pattern);

        if (cmp <= 0)  // suffix <= pattern
        {
            if (cmp == 0) end = mid;
            l = mid + 1;  // Search right for later match
        }
        else
        {
            r = mid - 1;
        }
    }

    // STEP 3: Extract all matches
    int count = end - start + 1;
    long[] offsets = new long[count];
    for (int i = 0; i < count; i++)
    {
        offsets[i] = sa[start + i];
    }

    Array.Sort(offsets);  // Sort by file position for display
    return offsets;
}
```

#### **Comparison Function:**

```csharp
private int Compare(BinaryDataSource dataSource, int textOffset, byte[] pattern)
{
    long len = dataSource.Length;
    int limit = pattern.Length;

    // Safety check
    if (textOffset >= len) return -1;

    // Read chunk from file
    int availableBytes = (int)Math.Min(limit, len - textOffset);
    byte[] buffer = new byte[availableBytes];
    int bytesRead = dataSource.ReadRange(textOffset, buffer, 0, availableBytes);

    // Byte-by-byte comparison
    for (int i = 0; i < bytesRead; i++)
    {
        if (buffer[i] < pattern[i]) return -1;  // text < pattern
        if (buffer[i] > pattern[i]) return 1;   // text > pattern
    }

    // If we couldn't read enough, text is shorter
    if (bytesRead < limit) return -1;

    return 0;  // Exact match
}
```

**Why This is Fast:**

- Traditional naive search: O(N \* M) - check every position
- Suffix Array search: O(M log N) - binary search with M-length comparisons
- For a 100MB file searching 10-byte pattern:
  - Naive: ~1 billion comparisons
  - Suffix Array: ~270 comparisons (log₂(100M) ≈ 27, times 10 bytes)

---

## 4. Anomaly Detection: Finding the Longest Repeated Substring (LRS)

### 4.1 The LRS Problem

In forensic analysis, identifying the **longest repeated substring** is crucial for:

- **Malware detection**: Repeated payload signatures
- **Exploit analysis**: Obfuscated code patterns
- **Data carving**: File fragments embedded multiple times
- **Compression artifacts**: Repeated data structures

### 4.2 The LCP Array Solution

**Key Theorem:**
The longest repeated substring in a text corresponds to the **maximum value** in the LCP array.

**Why?**

- LCP[i] represents the longest common prefix between SA[i] and SA[i-1]
- If LCP[i] = k, it means suffixes at positions SA[i] and SA[i-1] share k bytes
- This implies a k-byte substring appears at least twice in the file
- The maximum LCP value gives the longest such repetition

### 4.3 Implementation

```csharp
private void IsolateAnomaly()
{
    if (SelectedFile.LcpArray == null || SelectedFile.SuffixArray == null)
        return;

    // STEP 1: Find maximum value in LCP array
    int maxLcp = 0;
    int maxIndex = 0;

    var lcp = SelectedFile.LcpArray;
    for (int i = 1; i < lcp.Length; i++)
    {
        if (lcp[i] > maxLcp)
        {
            maxLcp = lcp[i];
            maxIndex = i;
        }
    }

    // STEP 2: Extract location and length
    if (maxLcp > 0)
    {
        // The repeated substring starts at this offset
        long offset = SelectedFile.SuffixArray[maxIndex];
        long length = maxLcp;

        // STEP 3: Navigate to show user
        _navigateToAnomalyAction?.Invoke(SelectedFile, offset, length);
    }
}
```

**Time Complexity: O(n)** - single scan through LCP array

**Example:**

Given a binary file containing:

```
00 01 02 03 04 05 06 07 | FF FF AA BB CC | 08 09 | FF FF AA BB CC | 10 11
```

The repeated sequence `FF FF AA BB CC` (5 bytes) would produce:

- Two suffixes starting at positions 8 and 19
- These suffixes appear consecutively (or nearby) in the sorted SA
- LCP value of 5 at their comparison point
- If this is the longest repetition, LCP[i] = 5 is the maximum

---

## 5. File Similarity: Longest Common Substring (LCS)

### 5.1 The LCS Problem

Finding the **longest common substring** between two files enables:

- **Malware variant detection**: Shared code between samples
- **Binary diffing**: Common sections between versions
- **File relationship analysis**: Shared embedded data

### 5.2 Generalized Suffix Array Approach (Theory)

The optimal algorithm uses a **Generalized Suffix Array**:

1. Concatenate File1 + separator + File2
2. Build SA and LCP for combined text
3. Find maximum LCP where SA[i] is from File1 and SA[i-1] is from File2 (or vice versa)

**Time Complexity: O(n₁ + n₂)** after SA construction

### 5.3 Current Implementation

DataSpecter's current implementation uses a **simplified heuristic** for prototype purposes:

```csharp
public async Task<(long length, long offset1, long offset2)> FindLcsAsync(
    BinaryDataSource source1, BinaryDataSource source2)
{
    // Load first 1MB of each file (or full file if smaller)
    long len1 = Math.Min(source1.Length, 1024 * 1024);
    long len2 = Math.Min(source2.Length, 1024 * 1024);

    byte[] data1 = new byte[len1];
    byte[] data2 = new byte[len2];

    source1.ReadRange(0, data1, 0, (int)len1);
    source2.ReadRange(0, data2, 0, (int)len2);

    // Sliding window comparison
    long bestLen = 0;
    long bestOff1 = 0;
    long bestOff2 = 0;

    int limit = 4096;  // Search first 4KB for responsiveness
    for (int i = 0; i < Math.Min(len1, limit); i++)
    {
        for (int j = 0; j < Math.Min(len2, limit); j++)
        {
            if (data1[i] == data2[j])
            {
                // Extend match
                long currentLen = 0;
                while (i + currentLen < len1 &&
                       j + currentLen < len2 &&
                       data1[i + currentLen] == data2[j + currentLen])
                {
                    currentLen++;
                }

                if (currentLen > bestLen)
                {
                    bestLen = currentLen;
                    bestOff1 = i;
                    bestOff2 = j;
                }
            }
        }
    }

    return (bestLen, bestOff1, bestOff2);
}
```

**Current Time Complexity: O(N \* M)** for simplified version
**Future Optimization:** Implement full Generalized Suffix Array approach

---

## 6. Entropy Analysis for Anomaly Detection

### 6.1 Shannon Entropy

While not directly related to Suffix Arrays, **entropy analysis** complements pattern detection by identifying:

- **Encrypted sections**: High entropy (close to 8 bits)
- **Compressed data**: High entropy
- **Repeated patterns**: Low entropy
- **Plaintext**: Medium entropy

### 6.2 Algorithm

```csharp
private double CalculateShannonEntropy(byte[] buffer, int count)
{
    if (count == 0) return 0.0;

    // STEP 1: Count byte frequencies
    int[] frequencies = new int[256];
    for (int i = 0; i < count; i++)
    {
        frequencies[buffer[i]]++;
    }

    // STEP 2: Calculate entropy
    double entropy = 0.0;
    double log2 = Math.Log(2);

    for (int i = 0; i < 256; i++)
    {
        if (frequencies[i] > 0)
        {
            double p = (double)frequencies[i] / count;
            entropy -= p * (Math.Log(p) / log2);
        }
    }

    return entropy;  // Range: [0, 8] bits per byte
}
```

**Shannon Entropy Formula:**

$$
H(X) = -\sum_{i=0}^{255} p_i \log_2(p_i)
$$

Where $p_i$ is the probability of byte value $i$.

**Interpretation:**

- **H = 0**: All bytes identical (no information)
- **H ≈ 4**: Text or structured data
- **H ≈ 8**: Random, encrypted, or compressed data

### 6.3 Chunk-Based Analysis

```csharp
public async Task<double[]> CalculateEntropyAsync(BinaryDataSource dataSource, int chunkSize = 1024)
{
    long length = dataSource.Length;
    int numChunks = (int)((length + chunkSize - 1) / chunkSize);
    double[] entropyValues = new double[numChunks];

    return await Task.Run(() =>
    {
        byte[] buffer = new byte[chunkSize];

        for (int i = 0; i < numChunks; i++)
        {
            long offset = (long)i * chunkSize;
            int read = dataSource.ReadRange(offset, buffer, 0, chunkSize);

            if (read > 0)
            {
                entropyValues[i] = CalculateShannonEntropy(buffer, read);
            }
        }

        return entropyValues;
    });
}
```

**Use Case:**
Visualizing entropy across a file can reveal:

- Encrypted sections (spikes to ~8.0)
- Compressed regions (high entropy)
- Padding or null bytes (drops to 0)
- Transition points between sections

---

## 7. Performance Characteristics

### 7.1 Time Complexity Summary

| Operation                 | Naive Approach | Suffix Array Approach |
| ------------------------- | -------------- | --------------------- |
| **Construction**          | N/A            | O(n log² n)           |
| **Single Pattern Search** | O(N \* M)      | O(M log N)            |
| **Multiple Searches**     | O(K _ N _ M)   | O(K \* M log N)       |
| **LRS Detection**         | O(n²)          | O(n) with LCP         |
| **LCS Detection**         | O(n \* m)      | O((n+m) log(n+m))     |
| **Entropy Calculation**   | O(n)           | O(n)                  |

Where:

- n, m = file sizes
- M = pattern length
- K = number of searches

### 7.2 Space Complexity

- **Suffix Array**: 4n bytes (32-bit integers)
- **LCP Array**: 4n bytes (32-bit integers)
- **Rank Array** (temporary): 4n bytes
- **Total**: ~12n bytes during construction, 8n bytes after

**Example:**

- 100MB file → ~800MB RAM for SA + LCP
- Within limit specified in code (100MB max file size)

### 7.3 Cache Optimization

```csharp
private readonly ConcurrentDictionary<string, (int[] sa, int[] lcp, long size, DateTime modified)> _cache = new();
private const int MAX_CACHE_ENTRIES = 5;
```

**Benefits:**

- Avoid re-indexing frequently accessed files
- LRU-style eviction (FIFO in current implementation)
- Thread-safe with `ConcurrentDictionary`

---

## 8. Practical Applications in Forensics

### 8.1 Malware Analysis

**Scenario:** Analyzing a suspected malicious executable

1. **Index the file** → Build SA + LCP
2. **Entropy scan** → Find encrypted/packed regions (entropy > 7.5)
3. **Search for signatures** → Known malware patterns using SA search
4. **Anomaly detection** → Find repeated shellcode or payloads using LRS
5. **Compare variants** → Use LCS to find common code across samples

### 8.2 Data Carving

**Scenario:** Recovering deleted files from disk image

1. **Search for file headers** → JPEG (`FF D8 FF`), PNG (`89 50 4E 47`), etc.
2. **Pattern matching** → Use SA to locate all header occurrences instantly
3. **Repeated structures** → LRS helps identify file system metadata patterns
4. **Entropy profiles** → Distinguish file types by entropy signature

### 8.3 Exploit Detection

**Scenario:** Identifying buffer overflow attempts in network captures

1. **Search for shellcode patterns** → NOP sleds (`90 90 90 ...`), common exploit bytes
2. **Anomaly detection** → LRS finds repeated exploit payloads
3. **Entropy spikes** → Encoded/obfuscated shellcode shows unusual entropy

---

## 9. Algorithm Trade-offs and Limitations

### 9.1 Current Limitations

1. **File Size Cap**: 100MB limit due to in-memory construction

   - **Mitigation**: Disk-based algorithms (SA-IS, DC3) for larger files

2. **LCS Implementation**: Simplified O(n\*m) heuristic

   - **Mitigation**: Implement full Generalized Suffix Array

3. **GPU Utilization**: Minimal in current implementation
   - **Mitigation**: Implement GPU radix sort for SA construction

### 9.2 Alternative Algorithms

**SA-IS (Suffix Array Induced Sorting):**

- Time: O(n)
- Space: O(n)
- More complex to implement
- Better for very large files

**DC3 (Difference Cover 3):**

- Time: O(n)
- Divide-and-conquer approach
- Excellent for parallel execution

**Suffix Trees:**

- More powerful for certain queries
- Much higher space overhead (~20n bytes)
- Harder to implement correctly

---

## 10. Conclusion

DataSpecter leverages fundamental string processing algorithms to provide powerful forensic analysis capabilities:

- **Suffix Arrays** enable O(M log N) pattern searching vs. O(N\*M) naive search
- **LCP Arrays** provide O(n) anomaly detection vs. O(n²) naive approach
- **Entropy Analysis** complements pattern detection with statistical anomalies
- **Combined approach** offers comprehensive binary analysis in a single tool

The implementation balances:

- **Performance**: Parallel construction, caching, optimized algorithms
- **Usability**: Progress reporting, responsive UI, intuitive navigation
- **Correctness**: Well-tested algorithms from academic literature
- **Scalability**: Designed for expansion to larger files and GPU acceleration

### Key Algorithmic Innovations in DataSpecter:

1. **Hybrid SA Construction**: CPU-optimized doubling with parallel tuple creation
2. **Kasai's LCP Algorithm**: Linear-time construction with progress reporting
3. **Dual Binary Search**: Efficient range queries for pattern occurrences
4. **Max-LCP Anomaly Detection**: Single-pass O(n) repeated pattern discovery
5. **Chunked Entropy Analysis**: Memory-efficient statistical profiling

This algorithmic foundation makes DataSpecter a powerful tool for forensic investigators, malware analysts, and security researchers working with binary data.

---

## References

1. **Manber, U., & Myers, G. (1993).** "Suffix arrays: A new method for on-line string searches." _SIAM Journal on Computing_, 22(5), 935-948.

2. **Kasai, T., et al. (2001).** "Linear-Time Longest-Common-Prefix Computation in Suffix Arrays and Its Applications." _CPM 2001_.

3. **Nong, G., Zhang, S., & Chan, W. H. (2009).** "Linear Suffix Array Construction by Almost Pure Induced-Sorting." _DCC 2009_.

4. **Shannon, C. E. (1948).** "A Mathematical Theory of Communication." _Bell System Technical Journal_, 27(3), 379-423.

5. **Gusfield, D. (1997).** _Algorithms on Strings, Trees, and Sequences: Computer Science and Computational Biology._ Cambridge University Press.

---

_Document Version: 1.0_  
_Last Updated: December 30, 2025_  
_Author: DataSpecter Development Team_
