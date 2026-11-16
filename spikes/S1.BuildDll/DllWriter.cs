using System.Text;

namespace S1.BuildDll;

/// <summary>
/// Writes a keyboard layout DLL (x64) straight from <see cref="RawTables"/>, with no
/// compiler and no kbdutool: a PE file with one export, <c>KbdLayerDescriptor</c>, whose
/// code is <c>lea rax,[rip+disp]; ret</c>, pointing at a KBDTABLES in the data section.
/// The headers copy what Windows' own kbdus.dll uses (native subsystem, no entry point).
/// </summary>
internal static class DllWriter
{
    private const ulong ImageBase = 0x180000000;
    private const uint TextRva = 0x1000;
    private const uint DataRva = 0x2000;
    private const int FileAlign = 0x200;
    private const int SectionAlign = 0x1000;

    private enum FixKind { Va64, Rva32 }

    private sealed class Blob
    {
        public readonly List<byte> Bytes = new();
        public readonly Dictionary<string, int> Labels = new();
        public readonly List<(int Pos, string Label, FixKind Kind)> Fixups = new();

        public int Pos => Bytes.Count;

        public void Label(string name) => Labels[name] = Pos;

        public void Align(int n)
        {
            while (Pos % n != 0)
            {
                Bytes.Add(0);
            }
        }

        public void U8(int v) => Bytes.Add((byte)v);

        public void U16(int v)
        {
            Bytes.Add((byte)v);
            Bytes.Add((byte)(v >> 8));
        }

        public void U32(uint v)
        {
            for (var i = 0; i < 4; i++)
            {
                Bytes.Add((byte)(v >> (8 * i)));
            }
        }

        public void Zero(int n)
        {
            for (var i = 0; i < n; i++)
            {
                Bytes.Add(0);
            }
        }

        public void Ptr(string? label)
        {
            if (label is not null)
            {
                Fixups.Add((Pos, label, FixKind.Va64));
            }

            Zero(8);
        }

        public void Rva(string label)
        {
            Fixups.Add((Pos, label, FixKind.Rva32));
            Zero(4);
        }

        public void Wstr(string label, string s)
        {
            Align(2);
            Label(label);
            foreach (var c in s)
            {
                U16(c);
            }

            U16(0);
        }

        public void Ascii(string label, string s)
        {
            Label(label);
            Bytes.AddRange(Encoding.ASCII.GetBytes(s));
            Bytes.Add(0);
        }
    }

    public static byte[] Build(RawTables t, string dllName = "kbdkeyassign.dll")
    {
        var d = new Blob();

        // Strings first.
        for (var i = 0; i < t.KeyNames.Count; i++)
        {
            d.Wstr($"kn{i}", t.KeyNames[i].Name);
        }

        for (var i = 0; i < t.KeyNamesExt.Count; i++)
        {
            d.Wstr($"ke{i}", t.KeyNamesExt[i].Name);
        }

        for (var i = 0; i < t.KeyNamesDead.Count; i++)
        {
            d.Wstr($"kd{i}", t.KeyNamesDead[i]);
        }

        // MODIFIERS and the VK_TO_BIT table it points at.
        d.Label("vkToBit");
        foreach (var (vk, bits) in t.VkToBit)
        {
            d.U8(vk);
            d.U8(bits);
        }

        d.U16(0);
        d.Align(8);
        d.Label("mods");
        d.Ptr("vkToBit");
        d.U16(t.ModNumber.Length - 1);
        foreach (var m in t.ModNumber)
        {
            d.U8(m);
        }

        // VK_TO_WCHAR rows, then the table of tables.
        for (var ti = 0; ti < t.VkToWchar.Count; ti++)
        {
            var table = t.VkToWchar[ti];
            d.Align(2);
            d.Label($"rows{ti}");
            foreach (var row in table.Rows)
            {
                d.U8(row.Vk);
                d.U8(row.Attributes);
                foreach (var c in row.Chars)
                {
                    d.U16(c);
                }
            }

            d.Zero(2 + (2 * table.Columns));
        }

        d.Align(8);
        d.Label("vkToWchar");
        for (var ti = 0; ti < t.VkToWchar.Count; ti++)
        {
            d.Ptr($"rows{ti}");
            d.U8(t.VkToWchar[ti].Columns);
            d.U8(2 + (2 * t.VkToWchar[ti].Columns));
            d.Zero(6);
        }

        d.Zero(16);

        // DEADKEY[]
        d.Align(4);
        d.Label("dead");
        foreach (var r in t.Dead)
        {
            d.U32(r.Base | ((uint)r.Accent << 16));
            d.U16(r.Composed);
            d.U16(r.Flags);
        }

        d.Zero(8);

        // Key names.
        d.Align(8);
        d.Label("names");
        for (var i = 0; i < t.KeyNames.Count; i++)
        {
            d.U8(t.KeyNames[i].Scan);
            d.Zero(7);
            d.Ptr($"kn{i}");
        }

        d.Zero(16);
        d.Label("namesExt");
        for (var i = 0; i < t.KeyNamesExt.Count; i++)
        {
            d.U8(t.KeyNamesExt[i].Scan);
            d.Zero(7);
            d.Ptr($"ke{i}");
        }

        d.Zero(16);
        d.Label("namesDead");
        for (var i = 0; i < t.KeyNamesDead.Count; i++)
        {
            d.Ptr($"kd{i}");
        }

        d.Zero(8);

        // Scan code tables.
        d.Align(2);
        d.Label("scanToVk");
        foreach (var v in t.ScanToVk)
        {
            d.U16(v);
        }

        d.Align(4);
        d.Label("e0");
        foreach (var (s, v) in t.ScanToVkE0)
        {
            d.U8(s);
            d.U8(0);
            d.U16(v);
        }

        d.Zero(4);
        d.Label("e1");
        foreach (var (s, v) in t.ScanToVkE1)
        {
            d.U8(s);
            d.U8(0);
            d.U16(v);
        }

        d.Zero(4);

        // Ligatures.
        var ligSize = t.LigatureMax == 0 ? 0 : 4 + (2 * t.LigatureMax);
        d.Align(2);
        d.Label("lig");
        foreach (var l in t.Ligatures)
        {
            d.U8(l.Vk);
            d.U8(0);
            d.U16(l.Column);
            foreach (var c in l.Chars)
            {
                d.U16(c);
            }
        }

        d.Zero(Math.Max(ligSize, 4));

        // KBDTABLES, field by field as kbd.h lays it out for x64.
        d.Align(8);
        d.Label("tables");
        d.Ptr("mods");
        d.Ptr("vkToWchar");
        d.Ptr(t.Dead.Count > 0 ? "dead" : null);
        d.Ptr("names");
        d.Ptr("namesExt");
        d.Ptr(t.KeyNamesDead.Count > 0 ? "namesDead" : null);
        d.Ptr("scanToVk");
        d.U8(t.ScanToVk.Length);
        d.Zero(7);
        d.Ptr("e0");
        d.Ptr("e1");
        d.U32(t.LocaleFlags);
        d.U8(t.LigatureMax);
        d.U8(ligSize);
        d.Zero(2);
        d.Ptr(t.Ligatures.Count > 0 ? "lig" : null);
        d.U32(t.KeyboardType);
        d.U32(t.KeyboardSubType);

        // Export directory with one function.
        d.Align(4);
        d.Label("export");
        d.U32(0);
        d.U32(0);
        d.U32(0);
        d.Rva("dllName");
        d.U32(1);
        d.U32(1);
        d.U32(1);
        d.Rva("expFuncs");
        d.Rva("expNames");
        d.Rva("expOrds");
        d.Label("expFuncs");
        d.U32(TextRva);
        d.Label("expNames");
        d.Rva("expName");
        d.Label("expOrds");
        d.U16(0);
        d.Ascii("dllName", dllName);
        d.Ascii("expName", "KbdLayerDescriptor");
        var exportSize = d.Pos - d.Labels["export"];

        // Resolve fixups; collect base relocations for the 64-bit pointers.
        var relocs = new List<uint>();
        var bytes = d.Bytes;
        foreach (var (pos, label, kind) in d.Fixups)
        {
            var target = DataRva + (ulong)d.Labels[label];
            if (kind == FixKind.Rva32)
            {
                Patch(bytes, pos, (uint)target);
            }
            else
            {
                var va = ImageBase + target;
                Patch(bytes, pos, (uint)va);
                Patch(bytes, pos + 4, (uint)(va >> 32));
                relocs.Add(DataRva + (uint)pos);
            }
        }

        // Code: lea rax,[rip+disp32]; ret
        var code = new byte[FileAlign];
        var disp = (int)(DataRva + (uint)d.Labels["tables"]) - (int)(TextRva + 7);
        code[0] = 0x48;
        code[1] = 0x8D;
        code[2] = 0x05;
        BitConverter.GetBytes(disp).CopyTo(code, 3);
        code[7] = 0xC3;

        var reloc = BuildRelocBlocks(relocs);
        var dataRaw = Align(bytes.Count, FileAlign);
        var dataVirtual = Align(bytes.Count, SectionAlign);
        var relocRva = DataRva + (uint)dataVirtual;
        var relocRaw = Align(reloc.Length, FileAlign);
        var sizeOfImage = (int)relocRva + Align(reloc.Length, SectionAlign);

        var file = new List<byte>();
        WriteHeaders(file, dataVirtual: bytes.Count, dataRaw, reloc.Length, relocRaw, relocRva,
            sizeOfImage, exportRva: DataRva + (uint)d.Labels["export"], exportSize);
        file.AddRange(code);
        file.AddRange(bytes);
        file.AddRange(new byte[dataRaw - bytes.Count]);
        file.AddRange(reloc);
        file.AddRange(new byte[relocRaw - reloc.Length]);
        return file.ToArray();
    }

    private static int Align(int v, int a) => (v + a - 1) / a * a;

    private static void Patch(List<byte> b, int pos, uint v)
    {
        for (var i = 0; i < 4; i++)
        {
            b[pos + i] = (byte)(v >> (8 * i));
        }
    }

    private static byte[] BuildRelocBlocks(List<uint> relocs)
    {
        var output = new List<byte>();
        foreach (var page in relocs.OrderBy(r => r).GroupBy(r => r & ~0xFFFu))
        {
            var entries = page.Select(r => (ushort)((10 << 12) | (r & 0xFFF))).ToList();
            if (entries.Count % 2 != 0)
            {
                entries.Add(0);
            }

            output.AddRange(BitConverter.GetBytes(page.Key));
            output.AddRange(BitConverter.GetBytes(8 + (entries.Count * 2)));
            foreach (var e in entries)
            {
                output.AddRange(BitConverter.GetBytes(e));
            }
        }

        return output.ToArray();
    }

    private static void WriteHeaders(List<byte> f, int dataVirtual, int dataRaw, int relocVirtual,
        int relocRaw, uint relocRva, int sizeOfImage, uint exportRva, int exportSize)
    {
        void U16(int v) => f.AddRange(BitConverter.GetBytes((ushort)v));
        void U32(uint v) => f.AddRange(BitConverter.GetBytes(v));
        void U64(ulong v) => f.AddRange(BitConverter.GetBytes(v));

        // DOS header: MZ, and e_lfanew pointing at the PE signature right after it.
        f.AddRange(new byte[64]);
        f[0] = (byte)'M';
        f[1] = (byte)'Z';
        f[0x3C] = 0x40;

        f.AddRange(Encoding.ASCII.GetBytes("PE\0\0"));
        U16(0x8664);      // x64
        U16(3);           // sections
        U32(0);           // timestamp: fixed, so the output is reproducible
        U32(0);
        U32(0);
        U16(240);         // optional header size
        U16(0x2022);      // EXECUTABLE_IMAGE | LARGE_ADDRESS_AWARE | DLL

        U16(0x20B);
        f.Add(14);
        f.Add(0);
        U32((uint)FileAlign);                       // size of code
        U32((uint)(dataRaw + relocRaw));            // size of initialized data
        U32(0);
        U32(0);                                     // entry point: none
        U32(TextRva);
        U64(ImageBase);
        U32(SectionAlign);
        U32(FileAlign);
        U16(10);
        U16(0);
        U16(0);
        U16(0);
        U16(10);
        U16(0);
        U32(0);
        U32((uint)sizeOfImage);
        U32(0x400);                                 // size of headers
        U32(0);                                     // checksum
        U16(1);                                     // native subsystem, as kbdus.dll
        U16(0x4160);
        U64(0x100000);
        U64(0x1000);
        U64(0x100000);
        U64(0x1000);
        U32(0);
        U32(16);
        for (var i = 0; i < 16; i++)
        {
            switch (i)
            {
                case 0:
                    U32(exportRva);
                    U32((uint)exportSize);
                    break;
                case 5:
                    U32(relocRva);
                    U32((uint)relocVirtual);
                    break;
                default:
                    U32(0);
                    U32(0);
                    break;
            }
        }

        void Section(string name, int virtualSize, uint rva, int rawSize, int rawPtr, uint flags)
        {
            var n = new byte[8];
            Encoding.ASCII.GetBytes(name).CopyTo(n, 0);
            f.AddRange(n);
            U32((uint)virtualSize);
            U32(rva);
            U32((uint)rawSize);
            U32((uint)rawPtr);
            U32(0);
            U32(0);
            U16(0);
            U16(0);
            U32(flags);
        }

        Section(".text", 8, TextRva, FileAlign, 0x400, 0x60000020);
        Section(".data", dataVirtual, DataRva, dataRaw, 0x400 + FileAlign, 0x40000040);
        Section(".reloc", relocVirtual, relocRva, relocRaw, 0x400 + FileAlign + dataRaw, 0x42000040);

        f.AddRange(new byte[0x400 - f.Count]);
    }
}
