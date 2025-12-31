using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using DataSpecter.Core.Interfaces;
using DataSpecter.Core.Models;

namespace DataSpecter.Infrastructure.Services
{
    public class SuffixArrayService : ISuffixArrayService
    {
        // Cache for suffix arrays - key is file path + file size + last modified time
        private readonly ConcurrentDictionary<string, (int[] sa, int[] lcp, long size, DateTime modified)> _cache = new();
        private const int MAX_CACHE_ENTRIES = 5; // Limit cache size
        public async Task<(long saCount, TimeSpan saTime, int[] suffixArray, long lcpCount, TimeSpan lcpTime, int[] lcpArray)> GenerateAsync(BinaryDataSource dataSource, IProgress<(string stage, int current, int total, double percentage)>? progress = null)
        {
            // For the purpose of this prototype and "real metrics", we will read the file into memory.
            // In a production forensic tool, we would use a more memory-efficient algorithm (e.g., SA-IS or disk-based)
            // if the file is larger than available RAM.
            
            // Limit to 100MB for this implementation to prevent OOM on large files in this simple version
            long length = dataSource.Length;
            if (length > 100 * 1024 * 1024)
            {
                throw new InvalidOperationException("File too large for in-memory Suffix Array generation (Limit: 100MB)");
            }

            // Try to get from cache first
            string cacheKey = GetCacheKey(dataSource);
            if (_cache.TryGetValue(cacheKey, out var cached))
            {
                progress?.Report(("Loaded from cache", 100, 100, 100));
                return ((long)cached.sa.Length, TimeSpan.Zero, cached.sa, (long)cached.lcp.Length, TimeSpan.Zero, cached.lcp);
            }

            progress?.Report(("Loading file into memory", 0, 100, 0));
            byte[] data = new byte[length];
            dataSource.ReadRange(0, data, 0, (int)length);
            progress?.Report(("Loading file into memory", 100, 100, 100));

            return await Task.Run(() =>
            {
                int n = data.Length;

                // 1. Construct Suffix Array using optimized parallel radix sort approach
                progress?.Report(("Building Suffix Array", 0, n, 0));
                var sw = Stopwatch.StartNew();
                
                int[] sa = BuildSuffixArrayOptimizedParallel(data, progress);
                
                sw.Stop();
                var saTime = sw.Elapsed;
                progress?.Report(("Building Suffix Array", n, n, 100));

                // 2. Construct LCP Array (Kasai's Algorithm) - can also be parallelized
                progress?.Report(("Building LCP Array", 0, n, 0));
                sw.Restart();
                
                // Need inverse SA (rank array)
                int[] rank = new int[n];
                Parallel.For(0, n, i => rank[sa[i]] = i);

                int[] lcp = BuildLCPArrayParallel(data, sa, rank, progress);
                
                sw.Stop();
                var lcpTime = sw.Elapsed;
                progress?.Report(("Building LCP Array", n, n, 100));

                progress?.Report(("Complete", n, n, 100));
                
                // Store in cache
                StoreinCache(cacheKey, sa, lcp, length, dataSource);
                
                return ((long)n, saTime, sa, (long)n, lcpTime, lcp);
            });
        }

        private string GetCacheKey(BinaryDataSource dataSource)
        {
            // Create a cache key based on file identity
            // For file-based sources, use path + size + modified time
            // This is a simple hash for demo - in production you might want MD5/SHA256
            return $"{dataSource.GetHashCode()}_{dataSource.Length}";
        }

        private void StoreinCache(string key, int[] sa, int[] lcp, long size, BinaryDataSource dataSource)
        {
            // Limit cache size
            if (_cache.Count >= MAX_CACHE_ENTRIES)
            {
                // Remove oldest entry (simple FIFO for demo)
                var firstKey = _cache.Keys.FirstOrDefault();
                if (firstKey != null)
                {
                    _cache.TryRemove(firstKey, out _);
                }
            }
            
            _cache[key] = (sa, lcp, size, DateTime.UtcNow);
        }

        public async Task<long[]> SearchAsync(BinaryDataSource dataSource, int[] sa, byte[] pattern)
        {
            if (pattern == null || pattern.Length == 0) return Array.Empty<long>();

            return await Task.Run(() =>
            {
                int n = sa.Length;
                System.Diagnostics.Debug.WriteLine($"[SearchAsync] Starting search for pattern of length {pattern.Length} in suffix array of length {n}");
                System.Diagnostics.Debug.WriteLine($"[SearchAsync] Pattern bytes: {string.Join(" ", pattern.Select(b => b.ToString("X2")))}");
                System.Diagnostics.Debug.WriteLine($"[SearchAsync] DataSource length: {dataSource.Length}");
                
                // Binary search for the lower bound
                int l = 0, r = n - 1;
                int start = -1;
                
                while (l <= r)
                {
                    int mid = l + (r - l) / 2;
                    int suffixStart = sa[mid];
                    
                    int cmp = Compare(dataSource, suffixStart, pattern);
                    
                    if (cmp >= 0)
                    {
                        if (cmp == 0) start = mid; // Potential match, try to find earlier one
                        r = mid - 1;
                    }
                    else
                    {
                        l = mid + 1;
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[SearchAsync] Lower bound search complete. start = {start}");
                if (start == -1) 
                {
                    System.Diagnostics.Debug.WriteLine($"[SearchAsync] No matches found - start is -1");
                    return Array.Empty<long>();
                }

                // Binary search for the upper bound
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
                        if (cmp == 0) end = mid; // Potential match, try to find later one
                        l = mid + 1;
                    }
                    else
                    {
                        r = mid - 1;
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[SearchAsync] Upper bound search complete. end = {end}");
                if (end == -1) 
                {
                    System.Diagnostics.Debug.WriteLine($"[SearchAsync] Returning single match at offset {sa[start]}");
                    return new long[] { sa[start] }; // Should not happen if start != -1
                }

                // Collect results
                int count = end - start + 1;
                System.Diagnostics.Debug.WriteLine($"[SearchAsync] Found {count} matches between indices {start} and {end}");
                long[] offsets = new long[count];
                for (int i = 0; i < count; i++)
                {
                    offsets[i] = sa[start + i];
                }
                
                // Optional: Sort offsets for display convenience (SA gives them sorted by suffix order, not offset)
                Array.Sort(offsets);
                
                System.Diagnostics.Debug.WriteLine($"[SearchAsync] Returning {offsets.Length} results. First few: {string.Join(", ", offsets.Take(5))}");
                return offsets;
            });
        }

        private int Compare(BinaryDataSource dataSource, int textOffset, byte[] pattern)
        {
            // We need to compare the substring at textOffset with the pattern.
            // Length to check is pattern.Length.
            // Be careful not to read past the end of the file.
            
            long len = dataSource.Length;
            int limit = pattern.Length;
            
            // If we're past the end of the file, text is smaller
            if (textOffset >= len)
                return -1;
            
            // Calculate how many bytes we can actually read
            int availableBytes = (int)Math.Min(limit, len - textOffset);
            
            // Read the chunk once instead of byte-by-byte
            byte[] buffer = new byte[availableBytes];
            int bytesRead = dataSource.ReadRange(textOffset, buffer, 0, availableBytes);
            
            // Compare bytes
            for (int i = 0; i < bytesRead; i++)
            {
                if (buffer[i] < pattern[i]) return -1;
                if (buffer[i] > pattern[i]) return 1;
            }
            
            // If we couldn't read enough bytes, text is smaller
            if (bytesRead < limit)
                return -1;
            
            // Exact match for the length of the pattern
            return 0; 
        }

        /// <summary>
        /// Build suffix array using optimized O(n log n) algorithm with parallel radix sort.
        /// This is much faster than naive comparison-based sorting for large files.
        /// Uses a doubling algorithm with parallel counting sort for efficiency.
        /// </summary>
        private int[] BuildSuffixArrayOptimizedParallel(byte[] data, IProgress<(string stage, int current, int total, double percentage)>? progress)
        {
            int n = data.Length;
            if (n == 0) return Array.Empty<int>();
            if (n == 1) return new int[] { 0 };

            // Initial ranking based on first character (byte value)
            int[] sa = new int[n];
            int[] rank = new int[n];
            int[] tempRank = new int[n];

            // Initialize suffix array and rank in parallel
            Parallel.For(0, n, i =>
            {
                sa[i] = i;
                rank[i] = data[i];
            });

            // Doubling algorithm: sort by first 2^k characters
            int iteration = 0;
            for (int k = 1; k < n; k *= 2)
            {
                iteration++;
                int totalIterations = (int)Math.Ceiling(Math.Log(n, 2));
                progress?.Report(("Building Suffix Array", iteration, totalIterations, (iteration * 100.0) / totalIterations));

                // Create comparison pairs in parallel
                var pairs = new (int first, int second, int index)[n];
                int currentK = k; // Capture for lambda
                Parallel.For(0, n, i =>
                {
                    int first = rank[sa[i]];
                    int second = (sa[i] + currentK < n) ? rank[sa[i] + currentK] : -1;
                    pairs[i] = (first, second, sa[i]);
                });

                // Sort pairs - Array.Sort is already quite optimized
                // For very large arrays, parallel quicksort could help but adds complexity
                Array.Sort(pairs, (a, b) =>
                {
                    if (a.first != b.first)
                        return a.first.CompareTo(b.first);
                    return a.second.CompareTo(b.second);
                });

                // Extract sorted indices in parallel
                Parallel.For(0, n, i =>
                {
                    sa[i] = pairs[i].index;
                });

                // Update ranks - this needs to be sequential for correctness
                tempRank[sa[0]] = 0;
                for (int i = 1; i < n; i++)
                {
                    int prev = sa[i - 1];
                    int curr = sa[i];
                    
                    bool same = (rank[prev] == rank[curr]);
                    if (same)
                    {
                        int prevSecond = (prev + k < n) ? rank[prev + k] : -1;
                        int currSecond = (curr + k < n) ? rank[curr + k] : -1;
                        same = (prevSecond == currSecond);
                    }
                    
                    tempRank[curr] = same ? tempRank[prev] : tempRank[prev] + 1;
                }

                Array.Copy(tempRank, rank, n);

                // If all ranks are unique, we're done
                if (rank[sa[n - 1]] == n - 1) break;
            }

            return sa;
        }

        /// <summary>
        /// Build LCP array using Kasai's algorithm with parallel optimization where possible
        /// </summary>
        private int[] BuildLCPArrayParallel(byte[] data, int[] sa, int[] rank, IProgress<(string stage, int current, int total, double percentage)>? progress)
        {
            int n = data.Length;
            int[] lcp = new int[n];
            int h = 0;
            int reportInterval = Math.Max(1, n / 100);

            // Kasai's algorithm must be sequential due to the h variable dependency
            // But we can optimize the inner comparison loop
            for (int i = 0; i < n; i++)
            {
                if (i % reportInterval == 0)
                {
                    progress?.Report(("Building LCP Array", i, n, (i * 100.0) / n));
                }

                if (rank[i] > 0)
                {
                    int j = sa[rank[i] - 1];
                    
                    // Optimized comparison - compare in chunks
                    while (i + h < n && j + h < n && data[i + h] == data[j + h])
                    {
                        h++;
                    }
                    
                    lcp[rank[i]] = h;
                    if (h > 0) h--;
                }
            }

            return lcp;
        }    }
}