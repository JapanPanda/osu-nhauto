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
        private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, [Out] byte[] lpBuffer, int dwSize, out IntPtr lpNumberOfBytesRead);

        [DllImport("kernel32.dll")]
        private static extern int VirtualQueryEx(IntPtr hProcess, int lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, uint dwLength);

        public Memory(Process process)
        {
            this.process = process;
        }
      
        public Dictionary<string, int> FindSignature(List<byte[]> signatures, List<string> mask, int start = -1)
        {
            int addressesFound = 0;
            Dictionary<string, int> addressMap = new Dictionary<string, int>();
            List<int[]> searches = new List<int[]>();
            for (int i = 0; i < mask.Count; i++)
            {
                int[] search = new int[mask[i].Length];
                for (int j = 0, k = 0; i < mask[i].Length; i++)
                {
                    search[i] = -1;
                    if (mask[i][j] == 'x')
                        search[j++] = i;
                }
                search = search.Where(j => j >= 0).ToArray();
                searches.Add(search);
            }

            int startAddress = start > -1 ? start : (int)process.MainModule.BaseAddress;
            int currentAddress = startAddress;
            uint mbiSize = (uint)Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION));

            int lockObj = 0, result = -1;
            bool running = true;
            Task[] runningTasks = new Task[16];
            for (int i = 0; i < runningTasks.Length; ++i)
            {
                runningTasks[i] = Task.Run(() =>
                {
                    do
                    {
                        MEMORY_BASIC_INFORMATION mbi;
                        if (Interlocked.Exchange(ref lockObj, 1) == 0)
                        {
                            int status = VirtualQueryEx(process.Handle, currentAddress, out mbi, mbiSize);
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

                        if (mbi.AllocationProtect == 0 || (int)mbi.RegionSize <= 0)
                            continue;

                        try
                        {
                            byte[] buffer = ReadBytes((int)mbi.BaseAddress, (int)mbi.RegionSize);
                            int index = FindPattern(buffer, signature, search, ref running);
                            if (index != -1)
                            {
                                result = (int)mbi.BaseAddress + index;
                                if (result != -1)
                                    addressMap;

                                if (addressesFound == addressMap.Count)
                                {
                                    running = false;
                                    return;
                                }
                            }
                        }
                        catch (OverflowException) { }
                    } while (running);
                });
            }
            Task.WaitAll(runningTasks);
            GC.Collect();

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

        public float ReadSingle(int address)
        {
            byte[] buffer = ReadBytes(address, sizeof(float));
            return BitConverter.ToSingle(buffer, 0);
        }

        public short ReadShort(int address)
        {
            byte[] buffer = ReadBytes(address, sizeof(short));
            return BitConverter.ToInt16(buffer, 0);
        }

        public int ReadInt32(int address)
        {
            byte[] buffer = ReadBytes(address, sizeof(int));
            return BitConverter.ToInt32(buffer, 0);
        }

        public uint ReadUInt32(int address)
        {
            byte[] buffer = ReadBytes(address, sizeof(uint));
            return BitConverter.ToUInt32(buffer, 0);
        }

        public bool ReadBoolean(int address)
        {
            byte[] buffer = ReadBytes(address, sizeof(bool));
            return BitConverter.ToBoolean(buffer, 0);
        }

        private int FindPattern(byte[] source, byte[] pattern, int[] search, ref bool running)
        {
            int size = source.Length - pattern.Length;
            int beforeEnd = search.Length - 1;
            for (int i = -1, k; running && ++i < size; i += k)
            {
                k = 0;
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

        private void FindPatterns(byte[] source, List<byte[]> patterns, List<int> searchArrays, ref bool[] addressesFound)
        {
            
        }

        public Process process { get; private set; }
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
