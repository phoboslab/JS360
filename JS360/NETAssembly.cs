#if !XBOX

// (c) Greg Jenkins
// http://www.ring3circus.com/downloads/admiraldebilitate/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace NETAssembly
{
    class NotNETAssemblyException : Exception
    {
        //
    };

    [StructLayout(LayoutKind.Sequential)]
    struct PESection
    {
        public UInt64 Name;
        public UInt32 VirtualSize;
        public UInt32 RVA;
        public UInt32 RawSize;
        public UInt32 RawOffset;
        public UInt32 RelocsOffset;
        public UInt32 LineNosOffset;
        public UInt16 NumRelocs;
        public UInt16 NumLineNos;
        public UInt32 Characteristics;
    }; // 28 bytes

    enum CLRHeaderFlags
    {
        ILOnly              = 0x00000001,
        Requires32Bit       = 0x00000002,
        ILLibrary           = 0x00000004,
        StronNameSigned     = 0x00000008,
        NativeEntryPoint    = 0x00000010,
        TrackDebugData      = 0x00010000
    };

    struct StreamHeader
    {
        public String Name;
        public UInt32 Offset;
        public UInt32 Size;
    };

    struct MetaData
    {
        public UInt32 Signature;
        public UInt16 MajorVersion;
        public UInt16 MinorVersion;
        public String Version;
        public int NumStreams;
        public StreamHeader[] StreamHeaders;
    };

    struct CLRHeader
    {
        public UInt32 cb;
        public UInt16 MajorRuntimeVersion;
        public UInt16 MinorRuntimeVersion;

        public UInt32 MetaDataRVA;
        public UInt32 MetaDataSize;
        public UInt32 Flags;

        public CLRHeaderFlags EntryPoint; // Either an RVA or a token

        public UInt32 ResourcesOffset;
        public UInt32 ResourcesSize;
        public UInt32 StrongNameSignatureOffset;
        public UInt32 StrongNameSignatureSize;
        public UInt32 CodeManagerTableOffset;
        public UInt32 CodeManagerTableSize;
        public UInt32 VTableFixupsOffset;
        public UInt32 VTableFixupsSize;
        public UInt32 ExportAddressTableJumpsOffset;
        public UInt32 ExportAddressTableJumpsSize;
        public UInt32 ManagedNativeHeaderOffset;
        public UInt32 ManagedNativeHeaderSize;
    };

    struct AssemblyRef
    {
        public long VersionOffset;
        public UInt16 MajVersion;
        public UInt16 MinVersion;
        public UInt16 BuildNumber;
        public UInt16 RevisionNumber;
        public UInt32 Flags;
        public long FlagsOffset;
        public UInt32 PublicKeyOrToken;
        public long PublicKeyOffset;
        public byte[] PublicKey;
        public long PublicKeyOrTokenOffset;
        public UInt32 NameStringIndex;
        public long NameStringIndexOffset;
        public String Name;
        public UInt32 CultureStringIndex;
        public String Culture;
        public UInt32 HashValueIndex;
    };

    class NETAssembly
    {
        public NETAssembly()
        {
            is_loaded = false;
            references = new List<AssemblyRef>();
        }

        ~NETAssembly()
        {
            if (file != null)
            {
                file.Close();
                file.Dispose();
            }
        }

        public void Close()
        {
            if (is_loaded) return;
            is_loaded = false;
            file.Close();
            file.Dispose();
            file = null;
        }

        public bool IsLoaded()
        {
            return is_loaded;
        }

        public bool IsSigned()
        {
            if (!is_loaded) return false;
            return (((clr_header.Flags & 8) != 0) || ((AssemblyFlags & 1) != 0));
        }

        public void Load(String filename)
        {
            // Check writeability
            bool writeable = false;
            try
            {
                file = new FileStream(filename, FileMode.Open, FileAccess.ReadWrite);
                // Okay, so continue to open for read only
                file.Close();
                file.Dispose();
                writeable = true;
            }
            catch (IOException)
            {
                // Defer exception until the end of the function
                // so the file can be treated as read-only
                writeable = false;
            }
            
            if (is_loaded) file.Close();
            file = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            
            Location = Path.GetFullPath(filename);
            Parse();

            is_loaded = true;
            if (!writeable) throw new IOException();
        }

        public UInt32 RVAToFileOffset(UInt32 RVA)
        {
            // Find containing section
            int section = 0;
            for (section = 0; section < num_sections; ++section)
            {
                if (sections[section].RVA <= RVA && (sections[section].RVA + sections[section].VirtualSize) > RVA) break;
            }
            if (section >= num_sections) throw new InvalidOperationException();

            return (RVA - sections[section].RVA) + sections[section].RawOffset;
        }

        public List<AssemblyRef> GetReferencedAssemblies()
        {
            if (!is_loaded) throw new InvalidOperationException("No assembly is loaded.");

            return references;
        }

        bool string_indices_are_dword = false;
        private uint ReadStringIndex()
        {
            byte[] buffer = new byte[4];
            if (string_indices_are_dword)
            {
                file.Read(buffer, 0, 4);
                return BitConverter.ToUInt32(buffer, 0);
            }
            else
            {
                file.Read(buffer, 0, 2);
                return BitConverter.ToUInt16(buffer, 0);
            }            
        }

        bool guid_indices_are_dword = false;
        private uint ReadGUIDIndex()
        {
            byte[] buffer = new byte[4];
            if (guid_indices_are_dword)
            {
                file.Read(buffer, 0, 4);
                return BitConverter.ToUInt32(buffer, 0);
            }
            else
            {
                file.Read(buffer, 0, 2);
                return BitConverter.ToUInt16(buffer, 0);
            }
        }

        bool blob_indices_are_dword = false;
        private uint ReadBlobIndex()
        {
            byte[] buffer = new byte[4];
            if (blob_indices_are_dword)
            {
                file.Read(buffer, 0, 4);
                return BitConverter.ToUInt32(buffer, 0);
            }
            else
            {
                file.Read(buffer, 0, 2);
                return BitConverter.ToUInt16(buffer, 0);
            }
        }

        int size_type_def_of_ref = 0;
        int size_has_constant = 0;
        int size_has_custom_attribute = 0;
        int size_has_field_marshall = 0;
        int size_has_decl_security = 0;
        int size_member_ref_parent = 0;
        int size_has_semantic = 0;
        int size_method_def_or_ref = 0;
        int size_member_forwarded = 0;
        int size_implementation  = 0;
        int size_custom_attribute_type = 0;
        int size_resolution_scope = 0;

        int size_field_index = 0;
        int size_method_index = 0;
        int size_param_index = 0;
        int size_type_def_index = 0;
        int size_event_index = 0;
        int size_property_index = 0;
        int size_module_ref_index = 0;
        int size_assembly_ref_index = 0;

        private void ComputeIndexSizes(UInt32[] sizes)
        {
            size_type_def_of_ref = 2;
            if (Math.Max(Math.Max(sizes[1], sizes[2]), sizes[27]) >= (1 << 14)) size_type_def_of_ref = 4;

            size_has_constant = 2;
            if (Math.Max(Math.Max(sizes[4], sizes[8]), sizes[23]) >= (1 << 14)) size_has_constant = 4;

            size_has_custom_attribute = 2;
            uint acc = 0;
            acc = Math.Max(acc, sizes[0]);
            acc = Math.Max(acc, sizes[6]);
            acc = Math.Max(acc, sizes[4]);
            acc = Math.Max(acc, sizes[2]);
            acc = Math.Max(acc, sizes[1]);
            acc = Math.Max(acc, sizes[8]);
            acc = Math.Max(acc, sizes[9]);
            acc = Math.Max(acc, sizes[10]);
            // acc = Math.Max(acc, sizes[0]);
            acc = Math.Max(acc, sizes[23]);
            acc = Math.Max(acc, sizes[20]);
            acc = Math.Max(acc, sizes[17]);
            acc = Math.Max(acc, sizes[26]);
            acc = Math.Max(acc, sizes[27]);
            acc = Math.Max(acc, sizes[32]);
            acc = Math.Max(acc, sizes[35]);
            acc = Math.Max(acc, sizes[38]);
            acc = Math.Max(acc, sizes[39]);
            acc = Math.Max(acc, sizes[40]);
            if (acc >= (1 << 11)) size_has_custom_attribute = 4;

            size_has_field_marshall = 2;
            if (Math.Max(sizes[4], sizes[8]) >= (acc << 15)) size_has_field_marshall = 4;

            size_has_decl_security = 2;
            if (Math.Max(Math.Max(sizes[2], sizes[6]), sizes[32]) >= (1 << 14)) size_has_decl_security = 4;

            size_member_ref_parent = 2;
            acc = 0;
            acc = Math.Max(acc, sizes[1]);
            acc = Math.Max(acc, sizes[2]);
            acc = Math.Max(acc, sizes[26]);
            acc = Math.Max(acc, sizes[6]);
            acc = Math.Max(acc, sizes[27]);
            if (acc >= (1 << 13)) size_member_ref_parent = 4;

            size_has_semantic = 2;
            if (Math.Max(sizes[20], sizes[23]) >= (1 << 15)) size_has_semantic = 4;

            size_method_def_or_ref = 2;
            if (Math.Max(sizes[6], sizes[10]) >= (1 << 15)) size_method_def_or_ref = 4;

            size_member_forwarded = 2;
            if (Math.Max(sizes[4], sizes[6]) >= (1 << 15)) size_member_forwarded = 4;

            size_implementation  = 2;
            if (Math.Max(Math.Max(sizes[35], sizes[38]), sizes[39]) >= (acc << 14)) size_implementation = 4;

            size_custom_attribute_type = 2;
            if (Math.Max(sizes[6], sizes[10]) >= (1 << 13)) size_custom_attribute_type = 4;

            size_resolution_scope = 2;
            acc = 0;
            acc = Math.Max(acc, sizes[0]);
            acc = Math.Max(acc, sizes[26]);
            acc = Math.Max(acc, sizes[35]);
            acc = Math.Max(acc, sizes[1]);
            if (acc >= (1 << 14)) size_resolution_scope = 4;

            size_field_index = 2;
            if (sizes[4] >= (1 << 15)) size_field_index = 4;

            size_method_index = 2;
            if (sizes[6] >= (1 << 15)) size_method_index = 4;

            size_param_index = 2;
            if (sizes[8] >= (1 << 15)) size_param_index = 4;

            size_type_def_index = 2;
            if (sizes[2] >= (1 << 15)) size_type_def_index = 4;

            size_event_index = 2;
            if (sizes[20] >= (1 << 15)) size_event_index = 4;

            size_property_index = 2;
            if (sizes[23] >= (1 << 15)) size_property_index = 4;

            size_module_ref_index = 2;
            if (sizes[26] >= (1 << 15)) size_module_ref_index = 4;

            size_assembly_ref_index = 2;
            if (sizes[35] >= (1 << 15)) size_assembly_ref_index = 4;
        }

        private String ReadStringByIndex(UInt32 offset)
        {
            long pos = file.Position;
            byte[] buffer = new byte[256];
            buffer[0] = 0;
            try
            {
                file.Seek(offset + metadata.StreamHeaders[string_table_index].Offset, SeekOrigin.Begin);
                file.Read(buffer, 0, 256);
            }
            catch
            {
                // Do nothing
            }
            String result = Encoding.ASCII.GetString(buffer, 0, 256);
            int length = result.IndexOf('\0');
            result = result.Substring(0, length);
            file.Seek(pos, SeekOrigin.Begin);
            return result;
        }

        private byte[] ReadPublicKeyToken(bool IsNotHashed)
        {
            byte[] key;
            int key_length = 0;
            if (!IsNotHashed)
            {
                key_length = file.ReadByte();
                key = new byte[key_length];
                file.Read(key, 0, key_length);
                return key;
            }

            // Otherwise the full key is present, so compute hash
            int mystery_byte = file.ReadByte();
            key_length = file.ReadByte();
            key = new byte[key_length];
            file.Read(key, 0, key_length);
            SHA1Managed sha = new SHA1Managed();
            byte[] hash = sha.ComputeHash(key);
            byte[] low_eight = new byte[8];
            for (int i = 0; i < 8; ++i) // I'm sure there's a smarter way to do this
            {
                low_eight[i] = hash[hash.Length - i - 1];
            }
            return low_eight;
        }

        int string_table_index;
        int meta_table_index;
        int us_table_index;
        int guid_table_index;
        int blob_table_index;
        private void ParseTables()
        {
            byte[] buffer = new byte[256];
            file.Seek(metadata.StreamHeaders[meta_table_index].Offset, SeekOrigin.Begin);

            file.Read(buffer, 0, 4); // Reserved DWORD = 0
            file.Read(buffer, 0, 1); // Major version
            file.Read(buffer, 0, 1); // Minor version
            file.Read(buffer, 0, 1); // Heap offset sizes
            char heap_offset_sizes = BitConverter.ToChar(buffer, 0);
            file.Read(buffer, 0, 1); // Reserved byte = 0

            string_indices_are_dword = ((heap_offset_sizes & 1) != 0);
            guid_indices_are_dword = ((heap_offset_sizes & 2) != 0);
            blob_indices_are_dword = ((heap_offset_sizes & 4) != 0);

            UInt64 table_mask = 0;
            file.Read(buffer, 0, 8);
            table_mask = BitConverter.ToUInt64(buffer, 0);

            UInt64 sorted_mask = 0;
            file.Read(buffer, 0, 8);
            sorted_mask = BitConverter.ToUInt64(buffer, 0);

            // Count the tables
            int num_tables = 0;
            for (int i = 0; i < 64; ++i)
            {
                if ((table_mask & ((ulong)1 << i)) != 0) ++num_tables;
            }

            // Get the sizes
            UInt32[] table_sizes = new UInt32[64];
            int highest_used_table = 0;
            for (int i = 0; i < 64; ++i)
            {
                uint size = 0;
                if ((table_mask & ((ulong)1 << i)) != 0)
                {
                    file.Read(buffer, 0, 4);
                    size = BitConverter.ToUInt32(buffer, 0);
                    highest_used_table = i;
                }
                table_sizes[i] = size;
            }

            ComputeIndexSizes(table_sizes);

            // Process the tables
            for (int i = 0; i <= highest_used_table; ++i)
            {
                if ((table_mask & ((ulong)1 << i)) != 0)
                {
                    uint num_entries = table_sizes[i];
                    for (int j = 0; j < num_entries; ++j)
                    {
                        switch (i)
                        {
                            case 0: // Modules
                                file.Read(buffer, 0, 2); // Generation = 0
                                uint index = ReadStringIndex(); // Name
                                Name = ReadStringByIndex(index);
                                ReadGUIDIndex(); // MVID
                                ReadGUIDIndex(); // EncID
                                ReadGUIDIndex(); // EncBaseID
                                break;
                            case 1: // TypeRef
                                file.Read(buffer, 0, size_resolution_scope);
                                ReadStringIndex(); // TypeName
                                ReadStringIndex(); // TypeNamespace
                                break;
                            case 2: // TypeDef
                                file.Read(buffer, 0, 4); // Flags
                                ReadStringIndex(); // TypeName
                                ReadStringIndex(); // TypeNamespace
                                file.Read(buffer, 0, size_type_def_of_ref); // Extends
                                file.Read(buffer, 0, size_field_index);// FieldList
                                file.Read(buffer, 0, size_method_index);// MethodList
                                break;
                            case 4: // Field
                                file.Read(buffer, 0, 2); // Flags
                                ReadStringIndex(); // Name
                                ReadBlobIndex(); // Signature
                                break;
                            case 6: // MethodDef
                                file.Read(buffer, 0, 4); // RVA
                                file.Read(buffer, 0, 2); // ImplFlags
                                file.Read(buffer, 0, 2); // Flags
                                ReadStringIndex(); // Name
                                ReadBlobIndex(); // Signature
                                file.Read(buffer, 0, size_param_index); // ParamList
                                break;
                            case 8: // Param
                                file.Read(buffer, 0, 2); // Flags
                                file.Read(buffer, 0, 2); // Sequence
                                ReadStringIndex(); // Name
                                break;
                            case 9: // InterfaceImpl
                                file.Read(buffer, 0, size_type_def_index); // Class
                                file.Read(buffer, 0, size_type_def_of_ref); // Interface
                                break;
                            case 10: // MemberRef
                                file.Read(buffer, 0, size_member_ref_parent); // Class
                                ReadStringIndex(); // Name
                                ReadBlobIndex(); // Index
                                break;
                            case 11: // Constant
                                file.Read(buffer, 0, 2); // Type
                                file.Read(buffer, 0, size_has_constant); // Parent
                                ReadBlobIndex(); // Value
                                break;
                            case 12: // CustomAttribute
                                file.Read(buffer, 0, size_has_custom_attribute); // Parent
                                file.Read(buffer, 0, size_custom_attribute_type); // Type
                                ReadBlobIndex(); // Value
                                break;
                            case 13: // FieldMarshall
                                file.Read(buffer, 0, size_has_field_marshall); // Parent
                                ReadBlobIndex(); // NativeType
                                break;
                            case 14: // DeclSecurity
                                file.Read(buffer, 0, 2); // Action
                                file.Read(buffer, 0, size_has_decl_security); // Parent
                                ReadBlobIndex(); // PermissionSet
                                break;
                            case 15: // ClassLayout
                                file.Read(buffer, 0, 2); // PackingSize
                                file.Read(buffer, 0, 4); // ClassSize
                                file.Read(buffer, 0, size_type_def_index); // Parent
                                break;
                            case 16: // FieldLayout
                                file.Read(buffer, 0, 4); // Offset
                                file.Read(buffer, 0, size_field_index); // Field
                                break;
                            case 17: // StandaloneSig
                                ReadBlobIndex(); // Signature
                                break;
                            case 18: // EventMapTable
                                file.Read(buffer, 0, size_type_def_index); // Parent
                                file.Read(buffer, 0, size_event_index); // EventList
                                break;
                            case 20: // Event
                                file.Read(buffer, 0, 2); // EventFlag
                                ReadStringIndex(); // Name
                                file.Read(buffer, 0, size_type_def_of_ref); // EventType
                                break;
                            case 21: // PropertyMap
                                file.Read(buffer, 0, size_type_def_index); // Parent
                                file.Read(buffer, 0, size_property_index); // Parent
                                break;
                            case 23: // PropertyTable
                                file.Read(buffer, 0, 2); // Flags
                                ReadStringIndex(); // Name
                                ReadBlobIndex(); // Type
                                break;
                            case 24: // MethodSemantics
                                file.Read(buffer, 0, 2); // Semantics
                                file.Read(buffer, 0, size_method_index); // Method
                                file.Read(buffer, 0, size_has_semantic); // Association
                                break;
                            case 25: // MethodImplTable
                                file.Read(buffer, 0, size_type_def_index); // Class
                                file.Read(buffer, 0, size_method_def_or_ref); // MethodBody
                                file.Read(buffer, 0, size_method_def_or_ref); // MethodDeclaration
                                break;
                            case 26: // ModuleRef
                                ReadStringIndex(); // Name
                                break;
                            case 27: // TypeSpec
                                ReadBlobIndex(); // Signature
                                break;
                            case 28: // ImplMap
                                file.Read(buffer, 0, 2); // MappingFlags
                                file.Read(buffer, 0, size_member_forwarded); // MemberForwarded
                                ReadStringIndex(); // ImportName
                                file.Read(buffer, 0, size_module_ref_index); // ImportScope
                                break;
                            case 29: // FieldRVA
                                file.Read(buffer, 0, 4); // RVA
                                file.Read(buffer, 0, size_field_index); // Field
                                break;
                            case 32: // Assembly
                                file.Read(buffer, 0, 4); // HashAlgId
                                file.Read(buffer, 0, 2); // MajVersion
                                file.Read(buffer, 0, 2); // MinVersion
                                file.Read(buffer, 0, 2); // BuildNumber
                                file.Read(buffer, 0, 2); // RevisionNumber
                                file.Read(buffer, 0, 4); // Flags
                                AssemblyFlags = BitConverter.ToUInt32(buffer, 0);
                                AssemblyFlagsOffset = file.Position - 4;
                                AssemblyPublicKeyIndexOffset = file.Position;
                                AssemblyPublicKeyIndex = ReadBlobIndex(); // Public Key
                                ReadStringIndex(); // Name
                                ReadStringIndex(); // Culture

                                // Get Public Key
                                long save_pos = file.Position;
                                file.Seek(metadata.StreamHeaders[blob_table_index].Offset + AssemblyPublicKeyIndex, SeekOrigin.Begin);
                                PublicKey = ReadPublicKeyToken((AssemblyFlags & 1) != 0);
                                file.Seek(save_pos, SeekOrigin.Begin);
                                break;
                            case 33: // AssemblyProcessor
                                file.Read(buffer, 0, 4); // Processor
                                break;
                            case 34: // AssemblyOS
                                file.Read(buffer, 0, 4); // OSPlatformID
                                file.Read(buffer, 0, 4); // OSMajorVersion
                                file.Read(buffer, 0, 4); // OSMinorVersion
                                break;
                            case 35: // AssemblyRef
                                long base_offset = file.Position;
                                file.Read(buffer, 0, 12);
                                
                                AssemblyRef assembly_ref = new AssemblyRef();
                                assembly_ref.VersionOffset = base_offset;
                                assembly_ref.MajVersion = BitConverter.ToUInt16(buffer, 0);
                                assembly_ref.MinVersion = BitConverter.ToUInt16(buffer, 2);
                                assembly_ref.BuildNumber = BitConverter.ToUInt16(buffer, 4);
                                assembly_ref.RevisionNumber = BitConverter.ToUInt16(buffer, 6);
                                assembly_ref.FlagsOffset = base_offset + 8;
                                assembly_ref.Flags = BitConverter.ToUInt32(buffer, 8);
                                assembly_ref.PublicKeyOrTokenOffset = base_offset + 12;
                                assembly_ref.PublicKeyOrToken = ReadBlobIndex();
                                assembly_ref.NameStringIndexOffset = base_offset + 12 + (blob_indices_are_dword ? 4 : 2);
                                assembly_ref.NameStringIndex = ReadStringIndex();
                                assembly_ref.CultureStringIndex = ReadStringIndex();
                                assembly_ref.HashValueIndex = ReadBlobIndex();

                                if (assembly_ref.NameStringIndex != 0)
                                {
                                    assembly_ref.Name = ReadStringByIndex(assembly_ref.NameStringIndex);
                                }
                                else
                                {
                                    assembly_ref.Name = "";
                                }
                                if (assembly_ref.CultureStringIndex != 0)
                                {
                                    assembly_ref.Culture = ReadStringByIndex(assembly_ref.CultureStringIndex);
                                }
                                else
                                {
                                    assembly_ref.Culture = "";
                                }

                                // Get Public Key
                                save_pos = file.Position;
                                assembly_ref.PublicKeyOffset = metadata.StreamHeaders[blob_table_index].Offset + assembly_ref.PublicKeyOrToken;
                                file.Seek(assembly_ref.PublicKeyOffset, SeekOrigin.Begin);
                                assembly_ref.PublicKey = ReadPublicKeyToken((assembly_ref.Flags & 1) != 0);
                                file.Seek(save_pos, SeekOrigin.Begin);

                                references.Add(assembly_ref);
                                break;
                            case 36: // AssemblyRefProcessor
                                file.Read(buffer, 0, 4); // Processor
                                file.Read(buffer, 0, size_assembly_ref_index); // AssemblyRef
                                break;
                            case 37: // AssemblyRefOS
                                file.Read(buffer, 0, 4); // OSPlatformId
                                file.Read(buffer, 0, 4); // OSMajorVersion
                                file.Read(buffer, 0, 4); // OSMinorVersion
                                file.Read(buffer, 0, size_assembly_ref_index); // AssemblyRef
                                break;
                            case 38: // File
                                file.Read(buffer, 0, 4); // Flags
                                ReadStringIndex(); // Name
                                ReadBlobIndex(); // HashValue
                                break;
                            case 39: // Exported Type
                                file.Read(buffer, 0, 4); // Flags
                                file.Read(buffer, 0, size_type_def_index); // TypeDefId
                                ReadStringIndex(); // TypeName
                                ReadStringIndex(); // TypeNamespace
                                file.Read(buffer, 0, size_implementation); // Implementation
                                break;
                            case 40: // ManifestResource
                                file.Read(buffer, 0, 4); // Offset
                                file.Read(buffer, 0, 4); // Flags
                                ReadStringIndex(); // Name
                                file.Read(buffer, 0, size_implementation); // Implementation
                                break;
                            case 41:
                                file.Read(buffer, 0, size_type_def_index); // NestedClass
                                file.Read(buffer, 0, size_type_def_index); // EncodingClass
                                break;
                        }
                    }
                }
            }
        }

        UInt32 clr_offset = 0;
        private void Parse()
        {

            clr_header.cb = 0;
            clr_header.MajorRuntimeVersion = 0;
            clr_header.MinorRuntimeVersion = 0;
            clr_header.MetaDataRVA = 0;
            clr_header.MetaDataSize = 0;
            clr_header.Flags = 0;
            clr_header.EntryPoint = 0;
            clr_header.ResourcesOffset = 0;
            clr_header.ResourcesSize = 0;
            clr_header.StrongNameSignatureOffset = 0;
            clr_header.StrongNameSignatureSize = 0;
            clr_header.CodeManagerTableOffset = 0;
            clr_header.CodeManagerTableSize = 0;
            clr_header.VTableFixupsOffset = 0;
            clr_header.VTableFixupsSize = 0;
            clr_header.ExportAddressTableJumpsOffset = 0;
            clr_header.ExportAddressTableJumpsSize = 0;
            clr_header.ManagedNativeHeaderOffset = 0;
            clr_header.ManagedNativeHeaderSize = 0;

            // Read the PE header
            int magic1 = file.ReadByte();
            int magic2 = file.ReadByte();
            if (magic1 != 0x4D || magic2 != 0x5A) throw new BadImageFormatException();

            byte[] buffer = new byte[256];
            file.Seek(0x3C, SeekOrigin.Begin);
            file.Read(buffer, 0, 4);
            UInt32 pe_header_offset = BitConverter.ToUInt32(buffer, 0);

            UInt32 clr_header_offset = pe_header_offset + 0xE8;
            file.Seek(clr_header_offset, SeekOrigin.Begin);
            file.Read(buffer, 0, 8);
            clr_offset = BitConverter.ToUInt32(buffer, 0);
            UInt32 clr_size = BitConverter.ToUInt32(buffer, 4);

            if (clr_offset == 0) throw new NotNETAssemblyException();
            if (clr_size > 0x1000) throw new BadImageFormatException();

            // Load the section map
            file.Seek(pe_header_offset + 6, SeekOrigin.Begin);
            file.Read(buffer, 0, 2);
            num_sections = BitConverter.ToInt16(buffer, 0);
            UInt32 sections_offset = pe_header_offset + 0xF8;
            file.Seek(sections_offset, SeekOrigin.Begin);
            sections = new PESection[num_sections];

            byte[] raw_section = new byte[0x28];
            for (int i = 0; i < num_sections; ++i)
            {
                file.Read(raw_section, 0, 0x28);
                GCHandle gch_sec = GCHandle.Alloc(raw_section, GCHandleType.Pinned);
                sections[i] = (PESection)Marshal.PtrToStructure(gch_sec.AddrOfPinnedObject(), typeof(PESection));
                gch_sec.Free();
            }

            // Get the CLR header
            clr_offset = RVAToFileOffset(clr_offset);

            byte[] raw_header = new byte[clr_size];
            file.Seek(clr_offset, SeekOrigin.Begin);
            file.Read(raw_header, 0, (int)clr_size);
            GCHandle gch_raw = GCHandle.Alloc(raw_header, GCHandleType.Pinned);
            clr_header = (CLRHeader)Marshal.PtrToStructure(gch_raw.AddrOfPinnedObject(), typeof(CLRHeader));
            gch_raw.Free();

            // Get the MetaData stream
            UInt32 meta_offset = RVAToFileOffset(clr_header.MetaDataRVA);

            file.Seek(meta_offset, SeekOrigin.Begin);
            file.Read(buffer, 0, 16);
            metadata.Signature = BitConverter.ToUInt32(buffer, 0);
            metadata.MajorVersion = BitConverter.ToUInt16(buffer, 4);
            metadata.MinorVersion = BitConverter.ToUInt16(buffer, 6);
            UInt32 version_length = BitConverter.ToUInt32(buffer, 12);
            
            if (metadata.Signature != 0x424A5342) throw new NotNETAssemblyException();

            file.Read(buffer, 0, (int) version_length);
            metadata.Version = Encoding.ASCII.GetString(buffer, 0, (int)version_length);
            version_length = (uint) metadata.Version.IndexOf('\0');
            metadata.Version = metadata.Version.Substring(0, (int)version_length);
            version_length = RoundUp(version_length, 4);

            file.Read(buffer, 0, 2); // Unused 'Flags' word

            file.Read(buffer, 0, 2);
            metadata.NumStreams = BitConverter.ToInt16(buffer, 0);

            metadata.StreamHeaders = new StreamHeader[metadata.NumStreams];
            for (int i = 0; i < metadata.NumStreams; ++i)
            {
                file.Read(buffer, 0, 8);
                metadata.StreamHeaders[i].Offset = BitConverter.ToUInt32(buffer, 0) + meta_offset;
                metadata.StreamHeaders[i].Size = BitConverter.ToUInt32(buffer, 4);

                long pos = file.Position;
                file.Read(buffer, 0, 256);
                metadata.StreamHeaders[i].Name = Encoding.ASCII.GetString(buffer);
                uint name_length = (uint) metadata.StreamHeaders[i].Name.IndexOf('\0');
                metadata.StreamHeaders[i].Name= metadata.StreamHeaders[i].Name.Substring(0, (int) name_length);
                name_length = RoundUp(name_length + 1, 4);
                pos += name_length;
                file.Seek(pos, SeekOrigin.Begin);
            }

            // Identify key streams indices
            string_table_index = -1;
            meta_table_index = -1;
            us_table_index = -1;
            guid_table_index = -1;
            blob_table_index = -1;

            for (int i = 0; i < metadata.StreamHeaders.Count(); ++i)
            {
                if (metadata.StreamHeaders[i].Name == "#Strings")
                {
                    string_table_index = i;
                } else if (metadata.StreamHeaders[i].Name == "#~")
                {
                    meta_table_index = i;
                } else if (metadata.StreamHeaders[i].Name == "#US")
                {
                    us_table_index = i;
                } else if (metadata.StreamHeaders[i].Name == "#GUID")
                {
                    guid_table_index = i;
                } else if (metadata.StreamHeaders[i].Name == "#Blob")
                {
                    blob_table_index = i;
                }
            }
                if (string_table_index == -1 ||
                    meta_table_index == -1 ||
                    us_table_index == -1 ||
                    guid_table_index == -1 ||
                    blob_table_index == -1) throw new NotNETAssemblyException();

            ParseTables();
        }

        private UInt32 RoundUp(UInt32 number, UInt32 modulus)
        {
            return modulus * ((number % modulus == 0 ? number : (number + modulus)) / modulus);
        }

        public void RemoveSigning()
        {
            if (!is_loaded) return;

            // Reload for writing
            file.Close();
            file.Dispose();
            string fname = file.Name;
            try
            {
                file = new FileStream(fname, FileMode.Open, FileAccess.ReadWrite);
            }
            catch (FileLoadException)
            {
                //..
            }

            // Patch header
            clr_header.Flags &= ~ (uint) 8;
            clr_header.StrongNameSignatureOffset = 0;
            clr_header.StrongNameSignatureSize = 0;
            AssemblyFlags &= ~ (uint) 1;

            // CLR Flags & Signature
            byte[] buffer = new byte[Marshal.SizeOf(clr_header.GetType())];
            GCHandle gcHandle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            Marshal.StructureToPtr(clr_header, gcHandle.AddrOfPinnedObject(), true);
            file.Seek(clr_offset, SeekOrigin.Begin);
            file.Write(buffer, 0, buffer.Length);
            gcHandle.Free();

            // Assembly Flags
            file.Seek(AssemblyFlagsOffset, SeekOrigin.Begin);
            file.Write(BitConverter.GetBytes(AssemblyFlags), 0, 4);

            // Assembly Public Key
            file.Seek(AssemblyPublicKeyIndexOffset, SeekOrigin.Begin);
            UInt32 zero = 0;
            file.Write(BitConverter.GetBytes(zero), 0, (blob_indices_are_dword ? 4 : 2));

            file.Close();
            file.Dispose();
            file = new FileStream(fname, FileMode.Open, FileAccess.Read);
        }

        public void RemoveSignedReferences(Hashtable assemblies, List<NETAssembly> MarkedAssemblies)
        {
            if (!is_loaded) return;

            // Reload for writing
            file.Close();
            file.Dispose();
            string fname = file.Name;
            try
            {
                file = new FileStream(fname, FileMode.Open, FileAccess.ReadWrite);
            }
            catch (FileLoadException)
            {
                //System.Windows.Forms.MessageBox.Show("Failed to open '" + fname + "' for writing.", "File Access Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }

            // Patch references
            byte[] zero_bytes = new byte[2];
            zero_bytes[0] = 0;
            zero_bytes[1] = 0;
            foreach (AssemblyRef reference in references)
            {
                NETAssembly assembly = (NETAssembly) assemblies[reference.Name];
                if (assembly != null)
                {
                    if (MarkedAssemblies.Contains(assembly))
                    {
                        // Remove the assembly-reference
                        file.Seek(reference.PublicKeyOrTokenOffset, SeekOrigin.Begin);
                        file.Write(zero_bytes, 0, 2);
                    }
                }
            }
            file.Close();
            file.Dispose();
            file = new FileStream(fname, FileMode.Open, FileAccess.Read);
        }


        public void SetVersionForReference(string name, byte[] version, byte[] publicKey)
        {
            if (!is_loaded) return;

            // Reload for writing
            file.Close();
            file.Dispose();
            string fname = file.Name;
            try
            {
                file = new FileStream(fname, FileMode.Open, FileAccess.ReadWrite);
            }
            catch (FileLoadException)
            {
                //System.Windows.Forms.MessageBox.Show("Failed to open '" + fname + "' for writing.", "File Access Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }

            // Patch references
            foreach (AssemblyRef reference in references)
            {
                if (reference.Name == name)
                {
                    file.Seek(reference.VersionOffset, SeekOrigin.Begin);
                    file.Write(version, 0, 8);

                    file.Seek(reference.PublicKeyOffset+1, SeekOrigin.Begin);
                    file.Write(publicKey, 0, 8);
                }
            }
            file.Close();
            file.Dispose();
            file = new FileStream(fname, FileMode.Open, FileAccess.Read);
        }

        public String Location;
        private bool is_loaded;
        private int num_sections;
        private FileStream file;
        PESection[] sections;
        public MetaData metadata;
        public List<AssemblyRef> references;
        public String Name;
        public UInt32 AssemblyFlags;
        public long AssemblyFlagsOffset;
        public UInt32 AssemblyPublicKeyIndex;
        public long AssemblyPublicKeyIndexOffset;
        public CLRHeader clr_header;
        public byte[] PublicKey;
    }
}

#endif
