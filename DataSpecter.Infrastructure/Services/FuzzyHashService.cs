using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DataSpecter.Core.Interfaces;
using DataSpecter.Core.Models;

namespace DataSpecter.Infrastructure.Services
{
    /// <summary>
    /// Implementation of fuzzy hashing (SSDEEP/CTPH) and file similarity comparison.
    /// </summary>
    public class FuzzyHashService : IFuzzyHashService
    {
        private const int MinBlockSize = 3;
        private const int MaxBlockSize = 3 * 1024 * 1024; // 3MB max block size
        private const int SpamSumLength = 64;
        private const int RollingWindow = 7;

        /// <summary>
        /// Computes the fuzzy hash (SSDEEP) of a file.
        /// </summary>
        public async Task<string> ComputeFuzzyHashAsync(BinaryDataSource dataSource)
        {
            return await Task.Run(() =>
            {
                long fileSize = dataSource.Length;
                
                // Calculate appropriate block size
                int blockSize = CalculateBlockSize(fileSize);
                
                // Compute two signatures with different block sizes
                var signature1 = ComputeSignature(dataSource, blockSize);
                var signature2 = ComputeSignature(dataSource, blockSize * 2);
                
                // Format as SSDEEP hash: blocksize:signature1:signature2
                return $"{blockSize}:{signature1}:{signature2}";
            });
        }

        /// <summary>
        /// Compares two fuzzy hashes and returns a similarity score (0-100).
        /// </summary>
        public int CompareFuzzyHashes(string hash1, string hash2)
        {
            if (string.IsNullOrEmpty(hash1) || string.IsNullOrEmpty(hash2))
                return 0;

            var parts1 = hash1.Split(':');
            var parts2 = hash2.Split(':');

            if (parts1.Length != 3 || parts2.Length != 3)
                return 0;

            int blockSize1 = int.Parse(parts1[0]);
            int blockSize2 = int.Parse(parts2[0]);

            // Block sizes must be compatible (same or factor of 2)
            if (blockSize1 != blockSize2 && blockSize1 != blockSize2 * 2 && blockSize2 != blockSize1 * 2)
                return 0;

            // Compare signatures
            int score1 = CompareSignatures(parts1[1], parts2[1]);
            int score2 = CompareSignatures(parts1[2], parts2[2]);
            
            // Also try cross-comparison for different block sizes
            int score3 = CompareSignatures(parts1[1], parts2[2]);
            int score4 = CompareSignatures(parts1[2], parts2[1]);

            return Math.Max(Math.Max(score1, score2), Math.Max(score3, score4));
        }

        /// <summary>
        /// Computes byte-level similarity using common substring analysis.
        /// </summary>
        public async Task<SimilarityResult> ComputeByteLevelSimilarityAsync(
            BinaryDataSource dataSource1,
            BinaryDataSource dataSource2,
            int[]? suffixArray1 = null,
            int[]? lcpArray1 = null)
        {
            return await Task.Run(() =>
            {
                var sw = Stopwatch.StartNew();
                
                long size1 = dataSource1.Length;
                long size2 = dataSource2.Length;
                
                // For very large files, sample chunks for performance
                const int maxSampleSize = 10 * 1024 * 1024; // 10MB sample
                const int chunkSize = 4096; // 4KB chunks
                
                long sampleSize1 = Math.Min(size1, maxSampleSize);
                long sampleSize2 = Math.Min(size2, maxSampleSize);
                
                // Calculate number of chunks to sample
                int numChunks1 = (int)((sampleSize1 + chunkSize - 1) / chunkSize);
                int numChunks2 = (int)((sampleSize2 + chunkSize - 1) / chunkSize);
                
                // Hash each chunk for fast comparison
                var chunks1 = HashChunks(dataSource1, numChunks1, chunkSize);
                var chunks2 = HashChunks(dataSource2, numChunks2, chunkSize);
                
                // Find common chunks
                var commonChunks = chunks1.Keys.Intersect(chunks2.Keys).ToList();
                int commonChunksCount = commonChunks.Count;
                
                // Calculate similarity percentage
                int totalChunks = Math.Max(numChunks1, numChunks2);
                double similarityPercentage = totalChunks > 0 
                    ? (commonChunksCount * 100.0 / totalChunks) 
                    : 0.0;
                
                // Find longest common substring
                var (longestLength, offset1, offset2) = FindLongestCommonSubstring(
                    dataSource1, dataSource2, Math.Min(size1, maxSampleSize / 2));
                
                sw.Stop();
                
                return new SimilarityResult
                {
                    SimilarityPercentage = similarityPercentage,
                    LongestCommonSubstringLength = longestLength,
                    LongestCommonSubstringOffset1 = offset1,
                    LongestCommonSubstringOffset2 = offset2,
                    TotalBytesAnalyzed = Math.Min(sampleSize1, sampleSize2),
                    CommonChunksCount = commonChunksCount,
                    CalculationTimeMs = sw.ElapsedMilliseconds
                };
            });
        }

        #region SSDEEP Implementation

        private int CalculateBlockSize(long fileSize)
        {
            // Calculate block size so we get approximately 64 hash points
            int blockSize = (int)(fileSize / SpamSumLength);
            
            // Ensure it's at least MinBlockSize
            if (blockSize < MinBlockSize)
                blockSize = MinBlockSize;
            
            // Round to nearest power of 2
            blockSize = (int)Math.Pow(2, Math.Floor(Math.Log(blockSize) / Math.Log(2)));
            
            // Clamp to valid range
            return Math.Max(MinBlockSize, Math.Min(MaxBlockSize, blockSize));
        }

        private string ComputeSignature(BinaryDataSource dataSource, int blockSize)
        {
            var signature = new StringBuilder();
            var rollingHash = new RollingHash(RollingWindow);
            
            byte[] buffer = new byte[8192]; // 8KB read buffer
            long offset = 0;
            long length = dataSource.Length;
            
            uint blockHash = 0;
            int blockPos = 0;
            
            const string base64Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
            
            while (offset < length && signature.Length < SpamSumLength)
            {
                int toRead = (int)Math.Min(buffer.Length, length - offset);
                int read = dataSource.ReadRange(offset, buffer, 0, toRead);
                
                if (read == 0) break;
                
                for (int i = 0; i < read && signature.Length < SpamSumLength; i++)
                {
                    byte b = buffer[i];
                    
                    // Update rolling hash
                    rollingHash.Update(b);
                    
                    // Update block hash (FNV-1a)
                    blockHash ^= b;
                    blockHash *= 0x01000193;
                    
                    blockPos++;
                    
                    // Check if we've hit a block boundary
                    if (rollingHash.Sum % blockSize == (blockSize - 1))
                    {
                        signature.Append(base64Chars[(int)(blockHash % 64)]);
                        blockHash = 0;
                        blockPos = 0;
                    }
                }
                
                offset += read;
            }
            
            // Add final partial block if any
            if (blockPos > 0 && signature.Length < SpamSumLength)
            {
                signature.Append(base64Chars[(int)(blockHash % 64)]);
            }
            
            return signature.ToString();
        }

        private int CompareSignatures(string sig1, string sig2)
        {
            if (string.IsNullOrEmpty(sig1) || string.IsNullOrEmpty(sig2))
                return 0;

            int len1 = sig1.Length;
            int len2 = sig2.Length;
            
            // Use edit distance (Levenshtein) to compare signatures
            int distance = ComputeEditDistance(sig1, sig2);
            
            // Convert distance to similarity score (0-100)
            int maxLen = Math.Max(len1, len2);
            if (maxLen == 0) return 100;
            
            int score = (int)(100 * (1.0 - (double)distance / maxLen));
            return Math.Max(0, Math.Min(100, score));
        }

        private int ComputeEditDistance(string s1, string s2)
        {
            int m = s1.Length;
            int n = s2.Length;
            
            // Use space-optimized version (single row)
            int[] prev = new int[n + 1];
            int[] curr = new int[n + 1];
            
            for (int j = 0; j <= n; j++)
                prev[j] = j;
            
            for (int i = 1; i <= m; i++)
            {
                curr[0] = i;
                
                for (int j = 1; j <= n; j++)
                {
                    int cost = s1[i - 1] == s2[j - 1] ? 0 : 1;
                    curr[j] = Math.Min(Math.Min(
                        curr[j - 1] + 1,      // Insert
                        prev[j] + 1),         // Delete
                        prev[j - 1] + cost);  // Replace
                }
                
                var temp = prev;
                prev = curr;
                curr = temp;
            }
            
            return prev[n];
        }

        #endregion

        #region Similarity Comparison

        private Dictionary<ulong, List<long>> HashChunks(BinaryDataSource dataSource, int numChunks, int chunkSize)
        {
            var chunkHashes = new Dictionary<ulong, List<long>>();
            byte[] buffer = new byte[chunkSize];
            
            for (int i = 0; i < numChunks; i++)
            {
                long offset = (long)i * chunkSize;
                int read = dataSource.ReadRange(offset, buffer, 0, chunkSize);
                
                if (read > 0)
                {
                    ulong hash = ComputeFNV1aHash(buffer, read);
                    
                    if (!chunkHashes.ContainsKey(hash))
                        chunkHashes[hash] = new List<long>();
                    
                    chunkHashes[hash].Add(offset);
                }
            }
            
            return chunkHashes;
        }

        private ulong ComputeFNV1aHash(byte[] data, int length)
        {
            const ulong FnvPrime = 0x00000100000001B3;
            const ulong FnvOffsetBasis = 0xcbf29ce484222325;
            
            ulong hash = FnvOffsetBasis;
            
            for (int i = 0; i < length; i++)
            {
                hash ^= data[i];
                hash *= FnvPrime;
            }
            
            return hash;
        }

        private (int length, long offset1, long offset2) FindLongestCommonSubstring(
            BinaryDataSource dataSource1, 
            BinaryDataSource dataSource2, 
            long maxBytesToCheck)
        {
            const int minMatchLength = 16; // Minimum meaningful match
            int longestLength = 0;
            long bestOffset1 = 0;
            long bestOffset2 = 0;
            
            // Use a sliding window approach with hashing
            const int windowSize = 256;
            byte[] window1 = new byte[windowSize];
            byte[] window2 = new byte[windowSize];
            
            long size1 = Math.Min(dataSource1.Length, maxBytesToCheck);
            long size2 = Math.Min(dataSource2.Length, maxBytesToCheck);
            
            // Sample at intervals for performance
            int sampleInterval = Math.Max(1, (int)(size1 / 1000));
            
            for (long offset1 = 0; offset1 < size1 - minMatchLength; offset1 += sampleInterval)
            {
                int read1 = dataSource1.ReadRange(offset1, window1, 0, windowSize);
                if (read1 < minMatchLength) continue;
                
                ulong hash1 = ComputeFNV1aHash(window1, Math.Min(read1, 64));
                
                // Search for this pattern in file2
                for (long offset2 = 0; offset2 < size2 - minMatchLength; offset2 += sampleInterval)
                {
                    int read2 = dataSource2.ReadRange(offset2, window2, 0, windowSize);
                    if (read2 < minMatchLength) continue;
                    
                    ulong hash2 = ComputeFNV1aHash(window2, Math.Min(read2, 64));
                    
                    // Quick hash check first
                    if (hash1 != hash2) continue;
                    
                    // Found potential match, now verify and extend
                    int matchLength = 0;
                    long checkOffset1 = offset1;
                    long checkOffset2 = offset2;
                    
                    while (checkOffset1 < size1 && checkOffset2 < size2 && matchLength < windowSize)
                    {
                        byte b1 = window1[matchLength];
                        byte b2 = window2[matchLength];
                        
                        if (b1 != b2) break;
                        
                        matchLength++;
                    }
                    
                    if (matchLength > longestLength)
                    {
                        longestLength = matchLength;
                        bestOffset1 = offset1;
                        bestOffset2 = offset2;
                    }
                }
            }
            
            return (longestLength, bestOffset1, bestOffset2);
        }

        #endregion

        #region Rolling Hash

        private class RollingHash
        {
            private readonly int _windowSize;
            private readonly byte[] _window;
            private int _position;
            private uint _sum;

            public RollingHash(int windowSize)
            {
                _windowSize = windowSize;
                _window = new byte[windowSize];
                _position = 0;
                _sum = 0;
            }

            public void Update(byte b)
            {
                // Remove oldest byte's contribution
                _sum -= _window[_position];
                
                // Add new byte
                _window[_position] = b;
                _sum += b;
                
                // Move position
                _position = (_position + 1) % _windowSize;
            }

            public uint Sum => _sum;
        }

        #endregion
    }
}
