using Xunit;
using DataSpecter.UI.ViewModels;
using DataSpecter.Core.Models;
using DataSpecter.Core.Interfaces;
using DataSpecter.UI.Models;
using System.IO;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;

namespace DataSpecter.Tests
{
    public class FakeEntropyService : IEntropyService
    {
        public Task<double[]> CalculateEntropyAsync(BinaryDataSource dataSource, int blockSize = 256)
        {
            return Task.FromResult(new double[0]);
        }
    }

    public class FakeSuffixArrayService : ISuffixArrayService
    {
        public Task<(long saCount, TimeSpan saTime, int[] suffixArray, long lcpCount, TimeSpan lcpTime, int[] lcpArray)> GenerateAsync(BinaryDataSource dataSource, IProgress<(string stage, int current, int total, double percentage)>? progress = null)
        {
            return Task.FromResult((0L, TimeSpan.Zero, new int[0], 0L, TimeSpan.Zero, new int[0]));
        }

        public Task<long[]> SearchAsync(BinaryDataSource dataSource, int[] suffixArray, byte[] pattern)
        {
            // Return dummy results
            return Task.FromResult(new long[] { 10, 20, 30 });
        }
    }

    public class AnalysisWorkspaceViewModelTests
    {
        [Fact]
        public async Task Search_UpdatesCountAndResults_WhenUsingSuffixArray()
        {
            // Arrange
            string file1 = Path.GetTempFileName();
            File.WriteAllBytes(file1, new byte[100]);

            try 
            {
                using (var ds1 = new BinaryDataSource(file1))
                {
                    var files = new ObservableCollection<FileModel>
                    {
                        new FileModel 
                        { 
                            Name = "File1", 
                            Path = file1, 
                            DataSource = ds1,
                            SuffixArray = new int[0] // Simulate indexed file
                        }
                    };

                    var vm = new AnalysisWorkspaceViewModel(files, new FakeEntropyService(), new FakeSuffixArrayService());
                    vm.SelectedFile = files[0];
                    vm.SearchQuery = "test";
                    vm.IsHex = false;

                    // Act
                    await vm.SearchCommand.ExecuteAsync(null);

                    // Assert
                    Assert.Equal(3, vm.SearchCount);
                    Assert.Equal(3, vm.SearchResults.Count);
                    Assert.Equal(10, vm.SearchResults[0]);
                }
            }
            finally
            {
                try { if(File.Exists(file1)) File.Delete(file1); } catch {}
            }
        }

        [Fact]
        public async Task Search_UpdatesCountAndResults_WhenUsingNaiveSearch()
        {
            // Arrange
            string file1 = Path.GetTempFileName();
            // Create file with content "AABBAABB"
            File.WriteAllBytes(file1, new byte[] { 0xAA, 0xBB, 0xCC, 0xAA, 0xBB }); 

            try 
            {
                using (var ds1 = new BinaryDataSource(file1))
                {
                    var files = new ObservableCollection<FileModel>
                    {
                        new FileModel 
                        { 
                            Name = "File1", 
                            Path = file1, 
                            DataSource = ds1,
                            SuffixArray = null // Simulate NOT indexed
                        }
                    };

                    var vm = new AnalysisWorkspaceViewModel(files, new FakeEntropyService(), null);
                    vm.SelectedFile = files[0];
                    vm.SearchQuery = "AABB"; // Should match at 0 and 3
                    vm.IsHex = true;

                    // Act
                    await vm.SearchCommand.ExecuteAsync(null);

                    // Assert
                    // "AA BB CC AA BB"
                    // SearchQuery is "AABB" (Hex) -> bytes [0xAA, 0xBB]
                    // Index 0: AA BB -> Match
                    // Index 3: AA BB -> Match
                    
                    Assert.Equal(2, vm.SearchCount);
                    Assert.Contains(0, vm.SearchResults);
                    Assert.Contains(3, vm.SearchResults);
                }
            }
            finally
            {
                try { if(File.Exists(file1)) File.Delete(file1); } catch {}
            }
        }
    }
}
