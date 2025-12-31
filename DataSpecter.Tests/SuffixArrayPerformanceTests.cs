using System;
using System.Diagnostics;
using System.IO;
using DataSpecter.Core.Interfaces;
using DataSpecter.Core.Models;
using DataSpecter.Infrastructure.Services;
using Xunit;
using Xunit.Abstractions;

namespace DataSpecter.Tests
{
    public class SuffixArrayPerformanceTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly ISuffixArrayService _suffixArrayService;

        public SuffixArrayPerformanceTests(ITestOutputHelper output)
        {
            _output = output;
            _suffixArrayService = new GpuSuffixArrayService();
        }

        [Fact]
        public async Task BuildSuffixArray_10MBFile_ShouldCompleteQuickly()
        {
            // Arrange: Create a 10MB test file with realistic data
            string tempFile = Path.GetTempFileName();
            const int fileSize = 10 * 1024 * 1024; // 10MB
            
            try
            {
                // Generate test data with patterns (similar to log files)
                _output.WriteLine($"Generating {fileSize:N0} byte test file...");
                using (var fs = File.Create(tempFile))
                {
                    Random rand = new Random(42); // Deterministic seed for reproducible tests
                    byte[] buffer = new byte[4096];
                    int remaining = fileSize;
                    
                    while (remaining > 0)
                    {
                        int chunkSize = Math.Min(buffer.Length, remaining);
                        
                        // Generate semi-realistic data (mix of ASCII text and binary)
                        for (int i = 0; i < chunkSize; i++)
                        {
                            if (rand.Next(100) < 70) // 70% printable ASCII
                            {
                                buffer[i] = (byte)(32 + rand.Next(95)); // Space to ~
                            }
                            else
                            {
                                buffer[i] = (byte)rand.Next(256);
                            }
                        }
                        
                        fs.Write(buffer, 0, chunkSize);
                        remaining -= chunkSize;
                    }
                }
                
                using var dataSource = new BinaryDataSource(tempFile);
                _output.WriteLine($"File created: {dataSource.Length:N0} bytes");

                // Act: Build suffix array and measure time
                var stopwatch = Stopwatch.StartNew();
                
                var progress = new Progress<(string stage, int current, int total, double percentage)>(report =>
                {
                    if (report.current % (report.total / 10) == 0 || report.percentage >= 100)
                    {
                        _output.WriteLine($"[{report.stage}] {report.percentage:F1}% complete");
                    }
                });
                
                var result = await _suffixArrayService.GenerateAsync(dataSource, progress);
                
                stopwatch.Stop();
                
                // Assert
                _output.WriteLine($"\n=== PERFORMANCE RESULTS ===");
                _output.WriteLine($"File size: {fileSize:N0} bytes ({fileSize / (1024.0 * 1024.0):F2} MB)");
                _output.WriteLine($"Suffix array construction time: {result.saTime.TotalSeconds:F2} seconds");
                _output.WriteLine($"LCP array construction time: {result.lcpTime.TotalSeconds:F2} seconds");
                _output.WriteLine($"Total time: {stopwatch.Elapsed.TotalSeconds:F2} seconds");
                _output.WriteLine($"Throughput: {fileSize / result.saTime.TotalSeconds / (1024.0 * 1024.0):F2} MB/s");
                
                // Verify correctness
                Assert.Equal(fileSize, result.suffixArray.Length);
                Assert.Equal(fileSize, result.lcpArray.Length);
                
                // Performance target: should complete in < 10 seconds for 10MB
                // (Original issue was 27 seconds - we're aiming for at least 3x improvement)
                Assert.True(result.saTime.TotalSeconds < 10, 
                    $"Suffix array construction took {result.saTime.TotalSeconds:F2}s, expected < 10s");
                
                _output.WriteLine($"\n✅ Performance test PASSED (completed in {stopwatch.Elapsed.TotalSeconds:F2}s)");
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    try { File.Delete(tempFile); } catch { }
                }
            }
        }

        [Fact]
        public async Task BuildSuffixArray_SmallFiles_ShouldCompleteVeryQuickly()
        {
            // Test with various smaller file sizes
            int[] testSizes = { 1024, 10 * 1024, 100 * 1024, 1024 * 1024 }; // 1KB, 10KB, 100KB, 1MB
            
            foreach (int size in testSizes)
            {
                string tempFile = Path.GetTempFileName();
                
                try
                {
                    // Generate random data
                    byte[] data = new byte[size];
                    new Random(42).NextBytes(data);
                    File.WriteAllBytes(tempFile, data);
                    
                    using var dataSource = new BinaryDataSource(tempFile);
                    
                    var stopwatch = Stopwatch.StartNew();
                    var result = await _suffixArrayService.GenerateAsync(dataSource);
                    stopwatch.Stop();
                    
                    _output.WriteLine($"Size: {size:N0} bytes - SA Time: {result.saTime.TotalMilliseconds:F0}ms, " +
                                    $"LCP Time: {result.lcpTime.TotalMilliseconds:F0}ms, " +
                                    $"Total: {stopwatch.Elapsed.TotalMilliseconds:F0}ms");
                    
                    Assert.Equal(size, result.suffixArray.Length);
                }
                finally
                {
                    if (File.Exists(tempFile))
                    {
                        try { File.Delete(tempFile); } catch { }
                    }
                }
            }
        }

        [Fact]
        public async Task SearchPattern_After10MBBuild_ShouldBeFast()
        {
            // Arrange: Create test file with known patterns
            string tempFile = Path.GetTempFileName();
            const int fileSize = 10 * 1024 * 1024;
            string searchPattern = "FORENSIC_MARKER";
            int expectedMatches = 100;
            
            try
            {
                // Create file with embedded patterns
                _output.WriteLine($"Creating {fileSize:N0} byte file with {expectedMatches} search patterns...");
                using (var fs = File.Create(tempFile))
                {
                    Random rand = new Random(42);
                    int bytesWritten = 0;
                    int patternsWritten = 0;
                    
                    while (bytesWritten < fileSize)
                    {
                        // Periodically insert the search pattern
                        if (patternsWritten < expectedMatches && 
                            rand.Next(fileSize / expectedMatches) < 100)
                        {
                            byte[] pattern = System.Text.Encoding.ASCII.GetBytes(searchPattern);
                            fs.Write(pattern, 0, pattern.Length);
                            bytesWritten += pattern.Length;
                            patternsWritten++;
                        }
                        else
                        {
                            // Write random data
                            int chunkSize = Math.Min(1024, fileSize - bytesWritten);
                            byte[] buffer = new byte[chunkSize];
                            rand.NextBytes(buffer);
                            fs.Write(buffer, 0, buffer.Length);
                            bytesWritten += buffer.Length;
                        }
                    }
                }
                
                using var dataSource = new BinaryDataSource(tempFile);
                _output.WriteLine($"Building suffix array...");
                
                // Build suffix array
                var buildTime = Stopwatch.StartNew();
                var result = await _suffixArrayService.GenerateAsync(dataSource);
                buildTime.Stop();
                
                _output.WriteLine($"Suffix array built in {buildTime.Elapsed.TotalSeconds:F2}s");
                
                // Search for pattern
                byte[] searchBytes = System.Text.Encoding.ASCII.GetBytes(searchPattern);
                var searchTime = Stopwatch.StartNew();
                var matches = await _suffixArrayService.SearchAsync(dataSource, result.suffixArray, searchBytes);
                searchTime.Stop();
                
                // Assert
                _output.WriteLine($"\n=== SEARCH RESULTS ===");
                _output.WriteLine($"Pattern: '{searchPattern}'");
                _output.WriteLine($"Search time: {searchTime.Elapsed.TotalMilliseconds:F2}ms");
                _output.WriteLine($"Matches found: {matches.Length}");
                
                // Search should be very fast (< 100ms)
                Assert.True(searchTime.Elapsed.TotalMilliseconds < 100,
                    $"Search took {searchTime.Elapsed.TotalMilliseconds:F2}ms, expected < 100ms");
                
                _output.WriteLine($"\n✅ Search test PASSED");
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    try { File.Delete(tempFile); } catch { }
                }
            }
        }

        public void Dispose()
        {
            if (_suffixArrayService is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}
