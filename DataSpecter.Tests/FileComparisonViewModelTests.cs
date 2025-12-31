using Xunit;
using DataSpecter.UI.ViewModels;
using DataSpecter.Core.Models;
using DataSpecter.Core.Interfaces;
using DataSpecter.UI.Models;
using System.IO;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Linq;
using System;

namespace DataSpecter.Tests
{
    public class FakeLcsService : ILcsService
    {
        public Task<(long length, long offset1, long offset2)> FindLcsAsync(BinaryDataSource source1, BinaryDataSource source2)
        {
            return Task.FromResult((10L, 0L, 0L));
        }
    }

    public class FileComparisonViewModelTests
    {
        [Fact]
        public async Task Compare_PopulatesHexData_WithoutUIFreeze()
        {
            // Arrange
            string file1 = Path.GetTempFileName();
            string file2 = Path.GetTempFileName();
            
            // Write some data
            byte[] data = new byte[8192];
            for(int i=0;i<data.Length;i++) data[i] = (byte)(i % 255);
            File.WriteAllBytes(file1, data);
            File.WriteAllBytes(file2, data);

            try 
            {
                using (var ds1 = new BinaryDataSource(file1))
                using (var ds2 = new BinaryDataSource(file2))
                {
                    var files = new ObservableCollection<FileModel>
                    {
                        new FileModel { Name = "File1", Path = file1, DataSource = ds1 },
                        new FileModel { Name = "File2", Path = file2, DataSource = ds2 }
                    };

                    var vm = new FileComparisonViewModel(files, new FakeLcsService());
                    vm.SelectedFile1 = files[0];
                    vm.SelectedFile2 = files[1];

                    // Act
                    await vm.CompareCommand.ExecuteAsync(null);

                    // Assert
                    Assert.NotNull(vm.HexData1);
                    Assert.NotNull(vm.HexData2);
                    
                    // 4096 bytes / 16 bytes per row = 256 rows
                    Assert.Equal(256, vm.HexData1.Count); 
                    Assert.Equal(256, vm.HexData2.Count);
                    
                    // Verify items are populated correctly
                    // Row 0, Item 0 => data[0]
                    Assert.Equal(data[0], vm.HexData1[0].Items[0].Value);
                    
                    // Row 255, Item 15 => data[4095]
                    Assert.Equal(data[4095], vm.HexData1[255].Items[15].Value);
                }
            }
            finally
            {
                try { if(File.Exists(file1)) File.Delete(file1); } catch {}
                try { if(File.Exists(file2)) File.Delete(file2); } catch {}
            }
        }
    }
}
