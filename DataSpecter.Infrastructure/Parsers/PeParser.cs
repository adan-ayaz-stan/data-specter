using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using DataSpecter.Core.Interfaces;
using DataSpecter.Core.Models;

namespace DataSpecter.Infrastructure.Parsers
{
    public class PeParser : IStructureParser
    {
        public bool CanParse(string fileName, byte[] headerBytes)
        {
            if (headerBytes.Length < 2) return false;
            // MZ signature
            return headerBytes[0] == 0x4D && headerBytes[1] == 0x5A;
        }

        public async Task<List<StructureItem>> ParseAsync(BinaryDataSource dataSource)
        {
            return await Task.Run(() =>
            {
                var root = new List<StructureItem>();
                try
                {
                    // 1. DOS Header
                    byte[] dosHeader = new byte[64];
                    dataSource.ReadRange(0, dosHeader, 0, 64);
                    
                    var dosNode = new StructureItem("DOS Header", "", 0, 64);
                    dosNode.Children.Add(new StructureItem("e_magic", "MZ", 0, 2));
                    
                    int e_lfanew = BitConverter.ToInt32(dosHeader, 0x3C);
                    dosNode.Children.Add(new StructureItem("e_lfanew", $"0x{e_lfanew:X}", 0x3C, 4));
                    
                    root.Add(dosNode);

                    // 2. PE Header
                    if (e_lfanew > 0 && e_lfanew < dataSource.Length - 4)
                    {
                        byte[] peSig = new byte[4];
                        dataSource.ReadRange(e_lfanew, peSig, 0, 4);
                        if (peSig[0] == 'P' && peSig[1] == 'E' && peSig[2] == 0 && peSig[3] == 0)
                        {
                            var peNode = new StructureItem("NT Headers", "PE Signature", e_lfanew, 4);
                            root.Add(peNode);
                            
                            // File Header (20 bytes after PE sig)
                            long fileHeaderOffset = e_lfanew + 4;
                            byte[] fileHeader = new byte[20];
                            dataSource.ReadRange(fileHeaderOffset, fileHeader, 0, 20);
                            
                            var fhNode = new StructureItem("File Header", "", fileHeaderOffset, 20);
                            short numberOfSections = BitConverter.ToInt16(fileHeader, 2);
                            fhNode.Children.Add(new StructureItem("Machine", $"0x{BitConverter.ToInt16(fileHeader, 0):X4}", fileHeaderOffset, 2));
                            fhNode.Children.Add(new StructureItem("NumberOfSections", numberOfSections.ToString(), fileHeaderOffset + 2, 2));
                            peNode.Children.Add(fhNode);

                            // Optional Header (Standard + Windows fields)
                            long optHeaderOffset = fileHeaderOffset + 20;
                            short sizeOfOptionalHeader = BitConverter.ToInt16(fileHeader, 16);
                            
                            if (sizeOfOptionalHeader > 0)
                            {
                                byte[] optHeader = new byte[sizeOfOptionalHeader];
                                dataSource.ReadRange(optHeaderOffset, optHeader, 0, sizeOfOptionalHeader);
                                var optNode = new StructureItem("Optional Header", "", optHeaderOffset, sizeOfOptionalHeader);
                                
                                short magic = BitConverter.ToInt16(optHeader, 0);
                                string magicType = (magic == 0x10b) ? "PE32" : (magic == 0x20b) ? "PE32+" : "Unknown";
                                optNode.Children.Add(new StructureItem("Magic", $"{magicType} (0x{magic:X})", optHeaderOffset, 2));
                                
                                int entryPoint = BitConverter.ToInt32(optHeader, 16);
                                optNode.Children.Add(new StructureItem("AddressOfEntryPoint", $"0x{entryPoint:X}", optHeaderOffset + 16, 4));

                                peNode.Children.Add(optNode);
                            }

                            // Sections
                            long sectionHeadersOffset = optHeaderOffset + sizeOfOptionalHeader;
                            var sectionsNode = new StructureItem("Section Headers", $"{numberOfSections} Sections", sectionHeadersOffset, numberOfSections * 40);
                            
                            for(int i=0; i<numberOfSections; i++)
                            {
                                long offset = sectionHeadersOffset + (i * 40);
                                byte[] secData = new byte[40];
                                dataSource.ReadRange(offset, secData, 0, 40);
                                
                                string name = Encoding.ASCII.GetString(secData, 0, 8).TrimEnd('\0');
                                int virtualSize = BitConverter.ToInt32(secData, 8);
                                int virtualAddress = BitConverter.ToInt32(secData, 12);
                                int rawSize = BitConverter.ToInt32(secData, 16);
                                int rawPointer = BitConverter.ToInt32(secData, 20);
                                
                                var secNode = new StructureItem($"Section {i}", name, offset, 40);
                                secNode.Children.Add(new StructureItem("Name", name, offset, 8));
                                secNode.Children.Add(new StructureItem("Virtual Size", $"0x{virtualSize:X}", offset + 8, 4));
                                secNode.Children.Add(new StructureItem("Virtual Address", $"0x{virtualAddress:X}", offset + 12, 4));
                                secNode.Children.Add(new StructureItem("Raw Size", $"0x{rawSize:X}", offset + 16, 4));
                                secNode.Children.Add(new StructureItem("Raw Pointer", $"0x{rawPointer:X}", offset + 20, 4));
                                
                                sectionsNode.Children.Add(secNode);
                            }
                            root.Add(sectionsNode);
                        }
                    }
                }
                catch (Exception) { /* Handle read errors */ }
                
                return root;
            });
        }
    }
}
