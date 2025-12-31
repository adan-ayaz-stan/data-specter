using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DataSpecter.Core.Interfaces;
using DataSpecter.Core.Models;
using DataSpecter.Infrastructure.Services;
using Xunit;
using Xunit.Abstractions;

namespace DataSpecter.Tests
{
    public class SuffixArraySearchTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly ISuffixArrayService _suffixArrayService;
        private readonly string _testFilePath;
        private BinaryDataSource? _dataSource;

        public SuffixArraySearchTests(ITestOutputHelper output)
        {
            _output = output;
            _suffixArrayService = new GpuSuffixArrayService();
            _testFilePath = Path.Combine(Directory.GetCurrentDirectory(), "sample_data", "Manifest_UFSFiles_Win64.txt");
            
            _output.WriteLine($"Test file path: {_testFilePath}");
            _output.WriteLine($"File exists: {File.Exists(_testFilePath)}");
            
            if (File.Exists(_testFilePath))
            {
                var fileInfo = new FileInfo(_testFilePath);
                _output.WriteLine($"File size: {fileInfo.Length:N0} bytes");
            }
        }

        [Fact]
        public async Task Search_ShouldFindTtfPattern_InTestFile()
        {
            // Arrange
            if (!File.Exists(_testFilePath))
            {
                _output.WriteLine($"SKIP: Test file not found at {_testFilePath}");
                return;
            }

            _dataSource = new BinaryDataSource(_testFilePath);
            _output.WriteLine($"DataSource created. Length: {_dataSource.Length:N0}");

            // Check if file is too large
            if (_dataSource.Length > 100 * 1024 * 1024)
            {
                _output.WriteLine($"SKIP: File too large ({_dataSource.Length:N0} bytes > 100MB limit)");
                return;
            }

            // Build suffix array
            _output.WriteLine("Building suffix array...");
            var result = await _suffixArrayService.GenerateAsync(_dataSource);
            _output.WriteLine($"Suffix Array built: {result.suffixArray.Length:N0} entries in {result.saTime.TotalSeconds:F2}s");
            _output.WriteLine($"LCP Array built: {result.lcpArray.Length:N0} entries in {result.lcpTime.TotalSeconds:F2}s");

            // Act - Search for ".ttf"
            byte[] pattern = System.Text.Encoding.UTF8.GetBytes(".ttf");
            _output.WriteLine($"Searching for pattern: {string.Join(" ", pattern.Select(b => b.ToString("X2")))} ('{System.Text.Encoding.UTF8.GetString(pattern)}')");
            
            // First, let's verify the pattern exists in the file by naive search
            _output.WriteLine("Verifying pattern exists with naive search...");
            int naiveCount = 0;
            byte[] searchBuffer = new byte[8192];
            for (long i = 0; i < Math.Min(_dataSource.Length, 1_000_000); i += 4096)
            {
                int read = _dataSource.ReadRange(i, searchBuffer, 0, (int)Math.Min(searchBuffer.Length, _dataSource.Length - i));
                for (int j = 0; j <= read - pattern.Length; j++)
                {
                    bool match = true;
                    for (int k = 0; k < pattern.Length; k++)
                    {
                        if (searchBuffer[j + k] != pattern[k])
                        {
                            match = false;
                            break;
                        }
                    }
                    if (match)
                    {
                        _output.WriteLine($"  Naive search found match at offset {i + j}");
                        naiveCount++;
                        if (naiveCount >= 5) break;
                    }
                }
                if (naiveCount >= 5) break;
            }
            _output.WriteLine($"Naive search found {naiveCount} matches in first 1MB");
            
            // Let's manually test the Compare method at a known offset
            _output.WriteLine($"\nTesting Compare at known offset 14755...");
            var service = new SuffixArrayService();
            var compareMethod = typeof(SuffixArrayService).GetMethod("Compare", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (compareMethod != null)
            {
                int cmpResult = (int)compareMethod.Invoke(service, new object[] { _dataSource, 14755, pattern })!;
                _output.WriteLine($"Compare(dataSource, 14755, pattern) = {cmpResult} (should be 0 for exact match)");
                
                // Read actual bytes at offset 14755
                byte[] testBuffer = new byte[10];
                _dataSource.ReadRange(14755, testBuffer, 0, 10);
                _output.WriteLine($"Bytes at offset 14755: {string.Join(" ", testBuffer.Select(b => b.ToString("X2")))} ('{System.Text.Encoding.UTF8.GetString(testBuffer)}')");
            }
            
            // Check if 14755 is in the suffix array
            bool foundInSA = false;
            for (int i = 0; i < result.suffixArray.Length; i++)
            {
                if (result.suffixArray[i] == 14755)
                {
                    foundInSA = true;
                    _output.WriteLine($"Offset 14755 found in suffix array at index {i}");
                    
                    // Check surrounding entries
                    _output.WriteLine($"SA[{i-2}] = {result.suffixArray[Math.Max(0, i-2)]}");
                    _output.WriteLine($"SA[{i-1}] = {result.suffixArray[Math.Max(0, i-1)]}");
                    _output.WriteLine($"SA[{i}] = {result.suffixArray[i]}");
                    _output.WriteLine($"SA[{i+1}] = {result.suffixArray[Math.Min(result.suffixArray.Length-1, i+1)]}");
                    _output.WriteLine($"SA[{i+2}] = {result.suffixArray[Math.Min(result.suffixArray.Length-1, i+2)]}");
                    
                    // Check what Compare returns for these surrounding entries
                    for (int j = Math.Max(0, i-2); j <= Math.Min(result.suffixArray.Length-1, i+2); j++)
                    {
                        if (compareMethod != null)
                        {
                            int cmp = (int)compareMethod.Invoke(service, new object[] { _dataSource, result.suffixArray[j], pattern })!;
                            byte[] suffix = new byte[Math.Min(8, (int)(_dataSource.Length - result.suffixArray[j]))];
                            _dataSource.ReadRange(result.suffixArray[j], suffix, 0, suffix.Length);
                            _output.WriteLine($"  Compare(SA[{j}]={result.suffixArray[j]}) = {cmp}, suffix: {System.Text.Encoding.UTF8.GetString(suffix).Replace("\t", "\\t").Replace("\n", "\\n")}");
                        }
                    }
                    break;
                }
            }
            if (!foundInSA)
            {
                _output.WriteLine("WARNING: Offset 14755 NOT found in suffix array!");
            }
            
            // Find where all .ttf matches are in the suffix array
            _output.WriteLine("\nSearching for all .ttf match offsets in suffix array...");
            int[] ttfOffsets = new int[] { 14755, 14826, 14971, 15049, 15119 };
            foreach (var offset in ttfOffsets)
            {
                for (int i = 0; i < result.suffixArray.Length; i++)
                {
                    if (result.suffixArray[i] == offset)
                    {
                        if (compareMethod != null)
                        {
                            int cmp = (int)compareMethod.Invoke(service, new object[] { _dataSource, offset, pattern })!;
                            _output.WriteLine($"  Offset {offset} at SA[{i}], Compare={cmp}");
                        }
                        break;
                    }
                }
            }
            
            // Check if there's a consecutive range of .ttf matches somewhere
            _output.WriteLine("\nSearching for consecutive range of .ttf matches in SA...");
            for (int i = 0; i < result.suffixArray.Length - 5; i++)
            {
                int matchCount = 0;
                for (int j = 0; j < 5; j++)
                {
                    if (compareMethod != null)
                    {
                        int cmp = (int)compareMethod.Invoke(service, new object[] { _dataSource, result.suffixArray[i + j], pattern })!;
                        if (cmp == 0) matchCount++;
                    }
                }
                if (matchCount >= 3)
                {
                    _output.WriteLine($"Found {matchCount} consecutive matches starting at SA[{i}]:");
                    for (int j = 0; j < Math.Min(10, result.suffixArray.Length - i); j++)
                    {
                        if (compareMethod != null)
                        {
                            int cmp = (int)compareMethod.Invoke(service, new object[] { _dataSource, result.suffixArray[i + j], pattern })!;
                            _output.WriteLine($"  SA[{i + j}] = {result.suffixArray[i + j]}, Compare={cmp}");
                        }
                    }
                    break;
                }
            }
            
            var offsets = await _suffixArrayService.SearchAsync(_dataSource, result.suffixArray, pattern);

            // Assert
            _output.WriteLine($"Found {offsets.Length} matches");
            
            Assert.NotEmpty(offsets);
            
            // Verify each match
            foreach (var offset in offsets.Take(10))
            {
                byte[] buffer = new byte[pattern.Length];
                _dataSource.ReadRange(offset, buffer, 0, pattern.Length);
                string found = System.Text.Encoding.UTF8.GetString(buffer);
                _output.WriteLine($"  Offset {offset}: '{found}'");
                Assert.Equal(".ttf", found);
            }
            
            if (offsets.Length > 10)
            {
                _output.WriteLine($"  ... and {offsets.Length - 10} more matches");
            }
        }

        [Fact]
        public async Task Search_ShouldFindRobotoPattern_InTestFile()
        {
            // Arrange
            if (!File.Exists(_testFilePath))
            {
                _output.WriteLine($"SKIP: Test file not found at {_testFilePath}");
                return;
            }

            _dataSource = new BinaryDataSource(_testFilePath);

            if (_dataSource.Length > 100 * 1024 * 1024)
            {
                _output.WriteLine($"SKIP: File too large");
                return;
            }

            var result = await _suffixArrayService.GenerateAsync(_dataSource);
            _output.WriteLine($"Index built: {result.suffixArray.Length:N0} entries");

            // Act - Search for "Roboto"
            byte[] pattern = System.Text.Encoding.UTF8.GetBytes("Roboto");
            _output.WriteLine($"Searching for: '{System.Text.Encoding.UTF8.GetString(pattern)}'");
            
            var offsets = await _suffixArrayService.SearchAsync(_dataSource, result.suffixArray, pattern);

            // Assert
            _output.WriteLine($"Found {offsets.Length} matches");
            Assert.NotEmpty(offsets);
        }

        [Fact]
        public async Task Search_ShouldReturnEmpty_ForNonExistentPattern()
        {
            // Arrange
            if (!File.Exists(_testFilePath))
            {
                _output.WriteLine($"SKIP: Test file not found at {_testFilePath}");
                return;
            }

            _dataSource = new BinaryDataSource(_testFilePath);

            if (_dataSource.Length > 100 * 1024 * 1024)
            {
                _output.WriteLine($"SKIP: File too large");
                return;
            }

            var result = await _suffixArrayService.GenerateAsync(_dataSource);

            // Act - Search for something that doesn't exist
            byte[] pattern = System.Text.Encoding.UTF8.GetBytes("XYZNOTFOUND123");
            _output.WriteLine($"Searching for non-existent: '{System.Text.Encoding.UTF8.GetString(pattern)}'");
            
            var offsets = await _suffixArrayService.SearchAsync(_dataSource, result.suffixArray, pattern);

            // Assert
            _output.WriteLine($"Found {offsets.Length} matches (expected 0)");
            Assert.Empty(offsets);
        }

        [Fact]
        public void Compare_ShouldWork_WithSimplePattern()
        {
            // This tests the Compare method behavior
            string testFile = Path.GetTempFileName();
            try
            {
                // Create a simple test file
                File.WriteAllText(testFile, "Hello World! This is a test.");
                
                using var ds = new BinaryDataSource(testFile);
                var service = new SuffixArrayService();
                
                // Use reflection to test the private Compare method
                var compareMethod = typeof(SuffixArrayService).GetMethod("Compare", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (compareMethod != null)
                {
                    byte[] pattern = System.Text.Encoding.UTF8.GetBytes("World");
                    
                    // Should match at offset 6
                    int result1 = (int)compareMethod.Invoke(service, new object[] { ds, 6, pattern })!;
                    _output.WriteLine($"Compare at offset 6 (should match): {result1}");
                    Assert.Equal(0, result1);
                    
                    // Should not match at offset 0
                    int result2 = (int)compareMethod.Invoke(service, new object[] { ds, 0, pattern })!;
                    _output.WriteLine($"Compare at offset 0 (should not match): {result2}");
                    Assert.NotEqual(0, result2);
                }
                else
                {
                    _output.WriteLine("SKIP: Could not access Compare method via reflection");
                }
            }
            finally
            {
                if (File.Exists(testFile))
                    File.Delete(testFile);
            }
        }

        public void Dispose()
        {
            _dataSource?.Dispose();
        }
    }
}
