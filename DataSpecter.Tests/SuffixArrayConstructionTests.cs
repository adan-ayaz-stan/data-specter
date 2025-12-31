using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DataSpecter.Core.Interfaces;
using DataSpecter.Core.Models;
using DataSpecter.Infrastructure.Services;
using Xunit;
using Xunit.Abstractions;

namespace DataSpecter.Tests
{
    public class SuffixArrayConstructionTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly ISuffixArrayService _suffixArrayService;

        public SuffixArrayConstructionTests(ITestOutputHelper output)
        {
            _output = output;
            _suffixArrayService = new GpuSuffixArrayService();
        }

        [Fact]
        public async Task BuildSuffixArray_SimpleString_ShouldBeCorrectlySorted()
        {
            // Arrange
            string text = "banana";
            byte[] data = Encoding.ASCII.GetBytes(text);
            string tempFile = Path.GetTempFileName();
            File.WriteAllBytes(tempFile, data);
            
            BinaryDataSource? dataSource = null;
            try
            {
                dataSource = new BinaryDataSource(tempFile);
                _output.WriteLine($"Testing with text: '{text}'");
                _output.WriteLine($"Expected suffix array for 'banana': [5, 3, 1, 0, 4, 2]");
                _output.WriteLine($"Suffixes in order: a, ana, anana, banana, na, nana");

                // Act
                var result = await _suffixArrayService.GenerateAsync(dataSource);
                int[] sa = result.suffixArray;

                // Assert
                _output.WriteLine($"Generated suffix array: [{string.Join(", ", sa)}]");
                
                // Print the actual suffixes in order
                _output.WriteLine("Suffixes in generated order:");
                for (int i = 0; i < sa.Length; i++)
                {
                    string suffix = text.Substring(sa[i]);
                    _output.WriteLine($"  sa[{i}] = {sa[i]}: '{suffix}'");
                }

                // Verify the suffix array is sorted
                for (int i = 0; i < sa.Length - 1; i++)
                {
                    string suffix1 = text.Substring(sa[i]);
                    string suffix2 = text.Substring(sa[i + 1]);
                    int cmp = string.Compare(suffix1, suffix2, StringComparison.Ordinal);
                    
                    Assert.True(cmp <= 0, 
                        $"Suffixes not sorted: sa[{i}]='{suffix1}' should come before sa[{i+1}]='{suffix2}'");
                }

                // Expected suffix array for "banana" is [5, 3, 1, 0, 4, 2]
                int[] expected = new[] { 5, 3, 1, 0, 4, 2 };
                Assert.Equal(expected, sa);
            }
            finally
            {
                dataSource?.Dispose();
                if (File.Exists(tempFile))
                {
                    try { File.Delete(tempFile); } catch { }
                }
            }
        }

        [Fact]
        public async Task Search_AfterBuildingArray_ShouldFindPattern()
        {
            // Arrange
            string text = "banana";
            byte[] data = Encoding.ASCII.GetBytes(text);
            string tempFile = Path.GetTempFileName();
            File.WriteAllBytes(tempFile, data);
            
            BinaryDataSource? dataSource = null;
            try
            {
                dataSource = new BinaryDataSource(tempFile);
                
                // Build suffix array
                var result = await _suffixArrayService.GenerateAsync(dataSource);
                int[] sa = result.suffixArray;
                
                _output.WriteLine($"Text: '{text}'");
                _output.WriteLine($"Suffix array: [{string.Join(", ", sa)}]");

                // Act - search for "ana"
                byte[] pattern = Encoding.ASCII.GetBytes("ana");
                long[] matches = await _suffixArrayService.SearchAsync(dataSource, sa, pattern);

                // Assert
                _output.WriteLine($"Searching for 'ana'");
                _output.WriteLine($"Found {matches.Length} matches at positions: [{string.Join(", ", matches)}]");
                
                // "ana" appears at positions 1 and 3 in "banana"
                Assert.Equal(2, matches.Length);
                Assert.Contains(1L, matches);
                Assert.Contains(3L, matches);
            }
            finally
            {
                dataSource?.Dispose();
                if (File.Exists(tempFile))
                {
                    try { File.Delete(tempFile); } catch { }
                }
            }
        }

        [Fact]
        public async Task Search_SingleBytePattern_ShouldFindAllOccurrences()
        {
            // Arrange
            string text = "banana";
            byte[] data = Encoding.ASCII.GetBytes(text);
            string tempFile = Path.GetTempFileName();
            File.WriteAllBytes(tempFile, data);
            
            BinaryDataSource? dataSource = null;
            try
            {
                dataSource = new BinaryDataSource(tempFile);
                var result = await _suffixArrayService.GenerateAsync(dataSource);
                int[] sa = result.suffixArray;

                // Act - search for 'a'
                byte[] pattern = Encoding.ASCII.GetBytes("a");
                long[] matches = await _suffixArrayService.SearchAsync(dataSource, sa, pattern);

                // Assert
                _output.WriteLine($"Searching for 'a' in '{text}'");
                _output.WriteLine($"Found {matches.Length} matches at positions: [{string.Join(", ", matches)}]");
                
                // 'a' appears at positions 1, 3, 5 in "banana"
                Assert.Equal(3, matches.Length);
                Assert.Contains(1L, matches);
                Assert.Contains(3L, matches);
                Assert.Contains(5L, matches);
            }
            finally
            {
                dataSource?.Dispose();
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
