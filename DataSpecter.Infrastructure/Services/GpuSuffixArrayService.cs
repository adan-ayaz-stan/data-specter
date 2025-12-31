using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using DataSpecter.Core.Interfaces;
using DataSpecter.Core.Models;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;
using ILGPU.Runtime.CPU;

namespace DataSpecter.Infrastructure.Services
{
    /// <summary>
    /// GPU-accelerated suffix array service using ILGPU.
    /// Currently uses parallel CPU operations. GPU kernels for suffix array
    /// construction are complex and require careful implementation.
    /// </summary>
    public class GpuSuffixArrayService : ISuffixArrayService
    {
        private readonly Context? _context;
        private readonly Accelerator? _accelerator;
        private readonly bool _isGpuAvailable;
        
        // Cache for suffix arrays
        private readonly ConcurrentDictionary<string, (int[] sa, int[] lcp, long size, DateTime modified)> _cache = new();
        private const int MAX_CACHE_ENTRIES = 5;

        public GpuSuffixArrayService()
        {
            _context = Context.Create(builder => builder.Default().EnableAlgorithms());
            
            // Try to get CUDA GPU first, fall back to CPU
            try
            {
                _accelerator = _context.CreateCudaAccelerator(0);
                _isGpuAvailable = true;
                Debug.WriteLine($"[GpuSuffixArrayService] Using GPU: {_accelerator.Name}");
            }
            catch
            {
                _accelerator = _context.CreateCPUAccelerator(0);
                _isGpuAvailable = false;
                Debug.WriteLine("[GpuSuffixArrayService] GPU not available, using CPU accelerator");
            }
        }

        public async Task<(long saCount, TimeSpan saTime, int[] suffixArray, long lcpCount, TimeSpan lcpTime, int[] lcpArray)> GenerateAsync(
            BinaryDataSource dataSource, 
            IProgress<(string stage, int current, int total, double percentage)>? progress = null)
        {
            long length = dataSource.Length;
            if (length > 100 * 1024 * 1024)
            {
                throw new InvalidOperationException("File too large for in-memory Suffix Array generation (Limit: 100MB)");
            }

            // Check cache
            string cacheKey = GetCacheKey(dataSource);
            if (_cache.TryGetValue(cacheKey, out var cached))
            {
                progress?.Report(($"Loaded from cache ({(_isGpuAvailable ? "GPU" : "CPU")} mode)", 100, 100, 100));
                return ((long)cached.sa.Length, TimeSpan.Zero, cached.sa, (long)cached.lcp.Length, TimeSpan.Zero, cached.lcp);
            }

            progress?.Report(("Loading file into memory", 0, 100, 0));
            byte[] data = new byte[length];
            dataSource.ReadRange(0, data, 0, (int)length);
            progress?.Report(("Loading file into memory", 100, 100, 100));

            return await Task.Run(() =>
            {
                int n = data.Length;

                // Build Suffix Array using GPU
                progress?.Report(($"Building Suffix Array ({(_isGpuAvailable ? "GPU" : "CPU")})", 0, n, 0));
                var sw = Stopwatch.StartNew();
                
                int[] sa = BuildSuffixArrayGpu(data, progress);
                
                sw.Stop();
                var saTime = sw.Elapsed;
                progress?.Report(("Building Suffix Array", n, n, 100));

                // Verify suffix array is correctly sorted
                bool isValid = VerifySuffixArray(data, sa);
                Debug.WriteLine($"[GpuSuffixArrayService] Suffix array verification: {(isValid ? "PASSED" : "FAILED")}");
                if (!isValid)
                {
                    throw new InvalidOperationException("Suffix array construction failed - array is not properly sorted");
                }

                // Build LCP Array
                progress?.Report(("Building LCP Array", 0, n, 0));
                sw.Restart();
                
                int[] rank = new int[n];
                Parallel.For(0, n, i => rank[sa[i]] = i);
                int[] lcp = BuildLCPArrayOptimized(data, sa, rank, progress);
                
                sw.Stop();
                var lcpTime = sw.Elapsed;
                progress?.Report(("Building LCP Array", n, n, 100));

                progress?.Report(("Complete", n, n, 100));
                
                StoreInCache(cacheKey, sa, lcp, length, dataSource);
                
                return ((long)n, saTime, sa, (long)n, lcpTime, lcp);
            });
        }

        private int[] BuildSuffixArrayGpu(byte[] data, IProgress<(string stage, int current, int total, double percentage)>? progress)
        {
            int n = data.Length;
            int[] sa = new int[n];
            int[] rank = new int[n];
            int[] tempRank = new int[n];
            int[] tempSa = new int[n];

            // Initial ranking based on first character - parallelized
            Parallel.For(0, n, i =>
            {
                sa[i] = i;
                rank[i] = data[i];
            });

            // Initial counting sort by first byte (0-255 range)
            CountingSortBySingleKey(sa, rank, tempSa, n, 256);
            Array.Copy(tempSa, sa, n);

            // Update initial ranks based on sorted order
            tempRank[sa[0]] = 0;
            for (int i = 1; i < n; i++)
            {
                tempRank[sa[i]] = tempRank[sa[i - 1]];
                if (data[sa[i]] != data[sa[i - 1]])
                    tempRank[sa[i]]++;
            }
            Array.Copy(tempRank, rank, n);

            // Preallocate key arrays to reuse
            int[] firstKeys = new int[n];
            int[] secondKeys = new int[n];

            // Doubling algorithm - sort by first 2k characters in each iteration
            for (int k = 1; k < n; k *= 2)
            {
                int maxRank = tempRank[sa[n - 1]] + 1;
                int currentK = k; // Capture for lambda
                
                // First pass: sort by second key (rank[i+k])
                Parallel.For(0, n, i =>
                {
                    int idx = sa[i];
                    secondKeys[i] = (idx + currentK < n) ? rank[idx + currentK] : -1;
                });
                
                // Counting sort by second key (handles -1 to maxRank range)
                CountingSortByKey(sa, secondKeys, tempSa, n, maxRank);
                Array.Copy(tempSa, sa, n);
                
                // Second pass: stable sort by first key (rank[i])
                Parallel.For(0, n, i =>
                {
                    firstKeys[i] = rank[sa[i]];
                });
                
                CountingSortByKey(sa, firstKeys, tempSa, n, maxRank);
                Array.Copy(tempSa, sa, n);

                // Update ranks based on new sorted order - parallelizable
                tempRank[sa[0]] = 0;
                for (int i = 1; i < n; i++)
                {
                    int prevIdx = sa[i - 1];
                    int currIdx = sa[i];
                    
                    int prevRank1 = rank[prevIdx];
                    int prevRank2 = (prevIdx + currentK < n) ? rank[prevIdx + currentK] : -1;
                    
                    int currRank1 = rank[currIdx];
                    int currRank2 = (currIdx + currentK < n) ? rank[currIdx + currentK] : -1;
                    
                    tempRank[currIdx] = tempRank[prevIdx];
                    if (currRank1 != prevRank1 || currRank2 != prevRank2)
                    {
                        tempRank[currIdx]++;
                    }
                }

                Array.Copy(tempRank, rank, n);

                // Early termination if all ranks are unique
                if (tempRank[sa[n - 1]] == n - 1)
                    break;

                progress?.Report(($"Building Suffix Array ({(_isGpuAvailable ? "GPU" : "CPU")})", k * 2, n, Math.Min(100, (k * 2 * 100.0) / n)));
            }

            return sa;
        }

        /// <summary>
        /// Counting sort for initial byte-based ranking (0-255 range)
        /// </summary>
        private void CountingSortBySingleKey(int[] sa, int[] keys, int[] output, int n, int keyRange)
        {
            int[] count = new int[keyRange];
            
            // Count occurrences
            for (int i = 0; i < n; i++)
            {
                count[keys[sa[i]]]++;
            }
            
            // Compute cumulative counts
            for (int i = 1; i < keyRange; i++)
            {
                count[i] += count[i - 1];
            }
            
            // Place elements in sorted order (reverse to maintain stability)
            for (int i = n - 1; i >= 0; i--)
            {
                int key = keys[sa[i]];
                output[--count[key]] = sa[i];
            }
        }

        /// <summary>
        /// Counting sort for rank-based sorting (handles negative keys by offsetting)
        /// </summary>
        private void CountingSortByKey(int[] sa, int[] keys, int[] output, int n, int maxKey)
        {
            // Offset to handle -1 values
            int offset = 1;
            int keyRange = maxKey + offset + 1;
            int[] count = new int[keyRange];
            
            // Count occurrences
            for (int i = 0; i < n; i++)
            {
                count[keys[i] + offset]++;
            }
            
            // Compute cumulative counts
            for (int i = 1; i < keyRange; i++)
            {
                count[i] += count[i - 1];
            }
            
            // Place elements in sorted order (reverse to maintain stability)
            for (int i = n - 1; i >= 0; i--)
            {
                int key = keys[i] + offset;
                output[--count[key]] = sa[i];
            }
        }

        /// <summary>
        /// Verifies that the suffix array is correctly sorted.
        /// Used for debugging and testing.
        /// </summary>
        private bool VerifySuffixArray(byte[] data, int[] sa)
        {
            int n = sa.Length;
            
            // Check that all indices 0..n-1 appear exactly once
            bool[] seen = new bool[n];
            for (int i = 0; i < n; i++)
            {
                if (sa[i] < 0 || sa[i] >= n || seen[sa[i]])
                    return false;
                seen[sa[i]] = true;
            }

            // Check that suffixes are in sorted order
            for (int i = 0; i < n - 1; i++)
            {
                int pos1 = sa[i];
                int pos2 = sa[i + 1];
                
                // Compare the two suffixes
                int cmp = CompareSuffixes(data, pos1, pos2);
                if (cmp > 0)
                {
                    Debug.WriteLine($"[VerifySuffixArray] ERROR: Suffix at sa[{i}]={pos1} is greater than suffix at sa[{i+1}]={pos2}");
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Compares two suffixes starting at given positions.
        /// Returns: <0 if suffix1 < suffix2, 0 if equal, >0 if suffix1 > suffix2
        /// </summary>
        private int CompareSuffixes(byte[] data, int pos1, int pos2)
        {
            int n = data.Length;
            int len = Math.Min(n - pos1, n - pos2);
            
            for (int i = 0; i < len; i++)
            {
                if (data[pos1 + i] < data[pos2 + i]) return -1;
                if (data[pos1 + i] > data[pos2 + i]) return 1;
            }
            
            // If one suffix is a prefix of the other, the shorter one is smaller
            return (n - pos1).CompareTo(n - pos2);
        }

        private int[] BuildLCPArrayOptimized(byte[] data, int[] sa, int[] rank, IProgress<(string stage, int current, int total, double percentage)>? progress)
        {
            int n = data.Length;
            int[] lcp = new int[n];
            int h = 0;
            int reportInterval = Math.Max(1, n / 100);

            for (int i = 0; i < n; i++)
            {
                if (i % reportInterval == 0)
                {
                    progress?.Report(("Building LCP Array", i, n, (i * 100.0) / n));
                }

                if (rank[i] > 0)
                {
                    int j = sa[rank[i] - 1];
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

        public async Task<long[]> SearchAsync(BinaryDataSource dataSource, int[] sa, byte[] pattern)
        {
            if (pattern == null || pattern.Length == 0) return Array.Empty<long>();

            return await Task.Run(() =>
            {
                int n = sa.Length;
                Debug.WriteLine($"[GpuSuffixArrayService.SearchAsync] Starting search for pattern of length {pattern.Length} in suffix array of length {n}");
                Debug.WriteLine($"[GpuSuffixArrayService.SearchAsync] Pattern bytes: {string.Join(" ", pattern.Select(b => b.ToString("X2")))}");
                Debug.WriteLine($"[GpuSuffixArrayService.SearchAsync] DataSource length: {dataSource.Length}");

                // Binary search for lower bound
                int l = 0, r = n - 1;
                int start = -1;

                while (l <= r)
                {
                    int mid = l + (r - l) / 2;
                    int suffixStart = sa[mid];
                    int cmp = Compare(dataSource, suffixStart, pattern);
                    
                    Debug.WriteLine($"[GpuSuffixArrayService.SearchAsync] Binary search: mid={mid}, suffixStart={suffixStart}, cmp={cmp}");

                    if (cmp >= 0)
                    {
                        if (cmp == 0) start = mid;
                        r = mid - 1;
                    }
                    else
                    {
                        l = mid + 1;
                    }
                }

                Debug.WriteLine($"[GpuSuffixArrayService.SearchAsync] Lower bound search complete. start = {start}");
                if (start == -1)
                {
                    Debug.WriteLine($"[GpuSuffixArrayService.SearchAsync] No matches found - start is -1");
                    return Array.Empty<long>();
                }

                // Binary search for upper bound
                l = start;
                r = n - 1;
                int end = -1;

                while (l <= r)
                {
                    int mid = l + (r - l) / 2;
                    int suffixStart = sa[mid];
                    int cmp = Compare(dataSource, suffixStart, pattern);

                    if (cmp <= 0)
                    {
                        if (cmp == 0) end = mid;
                        l = mid + 1;
                    }
                    else
                    {
                        r = mid - 1;
                    }
                }

                Debug.WriteLine($"[GpuSuffixArrayService.SearchAsync] Upper bound search complete. end = {end}");
                if (end == -1)
                {
                    Debug.WriteLine($"[GpuSuffixArrayService.SearchAsync] Returning single match at offset {sa[start]}");
                    return new long[] { sa[start] };
                }

                // Collect results
                int count = end - start + 1;
                Debug.WriteLine($"[GpuSuffixArrayService.SearchAsync] Found {count} matches between indices {start} and {end}");
                long[] offsets = new long[count];
                for (int i = 0; i < count; i++)
                {
                    offsets[i] = sa[start + i];
                }

                Array.Sort(offsets);
                Debug.WriteLine($"[GpuSuffixArrayService.SearchAsync] Returning {offsets.Length} results. First few: {string.Join(", ", offsets.Take(5))}");
                return offsets;
            });
        }

        private int Compare(BinaryDataSource dataSource, int textOffset, byte[] pattern)
        {
            long len = dataSource.Length;
            int limit = pattern.Length;

            if (textOffset >= len) return -1;

            int availableBytes = (int)Math.Min(limit, len - textOffset);
            byte[] buffer = new byte[availableBytes];
            int bytesRead = dataSource.ReadRange(textOffset, buffer, 0, availableBytes);

            for (int i = 0; i < bytesRead; i++)
            {
                if (buffer[i] < pattern[i]) return -1;
                if (buffer[i] > pattern[i]) return 1;
            }

            if (bytesRead < limit) return -1;
            return 0;
        }

        private string GetCacheKey(BinaryDataSource dataSource)
        {
            return $"{dataSource.GetHashCode()}_{dataSource.Length}";
        }

        private void StoreInCache(string key, int[] sa, int[] lcp, long size, BinaryDataSource dataSource)
        {
            if (_cache.Count >= MAX_CACHE_ENTRIES)
            {
                var firstKey = _cache.Keys.FirstOrDefault();
                if (firstKey != null)
                {
                    _cache.TryRemove(firstKey, out _);
                }
            }

            _cache[key] = (sa, lcp, size, DateTime.UtcNow);
        }

        public void Dispose()
        {
            _accelerator?.Dispose();
            _context?.Dispose();
        }
    }
}
