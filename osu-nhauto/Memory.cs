using System;
using System.Linq;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace osu_nhauto
{
    class Memory
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadProcessMemory
            (IntPtr hProcess,
            IntPtr lpBaseAddress,
            [Out] byte[] lpBuffer,
            int dwSize,
            out IntPtr lpNumberOfBytesRead);

        private Process process;

        public Memory(Process process)
        {
            this.process = process;
        }

        public int FindSignature(byte[] signature, int regionSize, int scanSize, string Mask, int start = -1)
        {
            int startAddress = start > -1 ? start : (int)process.MainModule.BaseAddress;
            int endAddress = startAddress + scanSize;

            int currentAddress = startAddress;
            int region = regionSize;

            byte[] buffer = new byte[region];

            while (currentAddress < endAddress)
            {
                buffer = ReadBytes(currentAddress, region + signature.Length);
                int index = FindPattern(buffer, signature, Mask);

                if (index != -1)
                    return currentAddress + index;

                currentAddress += region;
            }

            throw new Exception("Signature not found");
        }

        public byte[] ReadBytes(int address, int size)
        {
            byte[] buffer = new byte[size];
            ReadProcessMemory(process.Handle, (IntPtr)address, buffer, size, out IntPtr read);
            return buffer;
        }

        public Single ReadSingle(int address)
        {
            byte[] buffer = ReadBytes(address, sizeof(Single));
            return BitConverter.ToSingle(buffer, 0);
        }

        public Int32 ReadInt32(int address)
        {
            byte[] buffer = ReadBytes(address, sizeof(Int32));
            return BitConverter.ToInt32(buffer, 0);
        }

        public Boolean ReadBoolean(int address)
        {
            byte[] buffer = ReadBytes(address, sizeof(Boolean));
            return BitConverter.ToBoolean(buffer, 0);
        }

        private int FindPattern(byte[] source, byte[] pattern, string mask)
        {
            bool found = false;
            int[] search = new int[mask.Length];
            for (int i = 0, j = 0; i < mask.Length; ++i)
            {
                if (mask[i] == 'x')
                    search[j++] = i + 1;
            }
            search = search.Where(i => i != 0).ToArray();
            for (int i = 0; i < source.Length - pattern.Length; i++)
            {
                found = true;
                for (int j = 0; j < search.Length; j++)
                {
                    int offset = search[j] - 1;
                    if (source[i + offset] != pattern[offset])
                    {
                        found = false;
                        break;
                    }
                }

                if (found)
                    return i;
            }

            return -1;
        }

        public Process Process { get => process; }
    }
}
