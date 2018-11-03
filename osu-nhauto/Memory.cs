using System;
using System.Linq;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

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

        [DllImport("kernel32.dll")]
        private static extern int VirtualQueryEx(IntPtr hProcess, int lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, uint dwLength);

        private Process process;

        public Memory(Process process)
        {
            this.process = process;
        }

        public int FindSignature(byte[] signature, string mask, int start = -1)
        {
            int[] search = new int[mask.Length];
            for (int i = 0, j = 0; i < mask.Length; ++i)
            {
                search[i] = -1;
                if (mask[i] == 'x')
                    search[j++] = i;
            }
            search = search.Where(i => i >= 0).ToArray();

            int startAddress = start > -1 ? start : (int)process.MainModule.BaseAddress;
            int currentAddress = startAddress;
            uint mbiSize = (uint)Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION));

            int lockObj = 0, result = -1;
            running = true;
            List<int> cache = new List<int>();
            for (int i = 0; i < 16; ++i)
            {
                Task.Run(() =>
                {
                    do
                    {
                        MEMORY_BASIC_INFORMATION mbi;
                        if (Interlocked.Exchange(ref lockObj, 1) == 0)
                        {
                            VirtualQueryEx(process.Handle, currentAddress, out mbi, mbiSize);
                            int area = (int)mbi.BaseAddress + (int)mbi.RegionSize;
                            if (currentAddress == area)
                            {
                                running = false;
                                return;
                            }
                            currentAddress = area;
                            Interlocked.Exchange(ref lockObj, 0);
                        }
                        else
                            continue;
                        try
                        {
                            byte[] buffer = ReadBytes((int)mbi.BaseAddress, (int)mbi.RegionSize);
                            int index = FindPattern(buffer, signature, mask, search);
                            if (index != -1)
                            {
                                result = (int)mbi.BaseAddress + index;
                                running = false;
                                return;
                            }
                        }
                        catch (OverflowException)
                        {
                            result = -1;
                            return;
                        }
                    } while (running);
                });
            }
            while (running) {}
            if (result != -1) 
                return result;
            else
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

        private int FindPattern(byte[] source, byte[] pattern, string mask, int[] search)
        {
            int size = source.Length - pattern.Length;
            int beforeEnd = search.Length - 1;
            for (int i = -1, k; ++i < size; i += k)
            {
                k = 0;
                if (!running)
                    break;
                for (int j = 0; j < search.Length; ++j)
                {
                    if (source[i + search[j]] != pattern[search[j]])
                        break;
                    if (j == beforeEnd)
                        return i;
                    k = search[j];
                }
            }

            return -1;
        }

        public Process Process { get => process; }
        public bool running;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MEMORY_BASIC_INFORMATION
    {
        public IntPtr BaseAddress;
        public IntPtr AllocationBase;
        public uint AllocationProtect;
        public IntPtr RegionSize;
        public uint State;
        public uint Protect;
        public uint Type;
    }
}
