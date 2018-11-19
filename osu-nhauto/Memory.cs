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
      
        public int FindSignature(string[] signature, int start, int end, int start2 = -1)
        {
            int addressesFound = 0;
            Dictionary<string[], int> addressMap = new Dictionary<string[], int>();

            int startAddress = start > -1 ? start : (int)process.MainModule.BaseAddress;
            int currentAddress = startAddress;
            uint mbiSize = (uint)Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION));

            int[] lps = initKMP(signature);
            int lockObj = 0;

            bool running = true;
            Task[] runningTasks = new Task[16];
            int output = -1;
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
                            if (currentAddress > end)
                            {
                                if (start2 != -1)
                                {
                                    currentAddress = start2;
                                    start2 = -1;
                                    end = start - (start - start2) / 2;
                                }
                                else
                                    currentAddress = (int)process.MainModule.BaseAddress;
                            }
                            Interlocked.Exchange(ref lockObj, 0);
                        }
                        else
                            continue;

                        if (mbi.AllocationProtect == 0 || (int)mbi.RegionSize <= 0)
                            continue;

                        try
                        {
                            byte[] buffer = ReadBytes((int)mbi.BaseAddress, (int)mbi.RegionSize);
                            //int index = FindPattern(buffer, signature, search, ref running);
                            int tempResult = FindPattern(buffer, signature, lps, (int)mbi.BaseAddress, ref running);
                            if (tempResult != -1)
                            {
                                running = false;
                                output = tempResult;
                                return;
                            }
                        }
                        catch (OverflowException) { }
                    } while (running);
                });
            }
            Task.WaitAll(runningTasks);
            GC.Collect();
            

            if (output != -1)
            {
                return output;
            } 

            else
                throw new Exception("Signatures not found");
        }


        private int FindPattern(byte[] source, string[] signature, int[] lps, int baseAddress, ref bool running)
        {
            int i = 0, j = 0;
            while (i < source.Length && j < signature.Length && running)
            {
                if (signature[j] == "??" || source[i].ToString("X") == signature[j])
                {
                    i++;
                    j++;
                }
                else
                {
                    if (j != 0)
                        j = lps[j - 1];
                    else
                        i++;
                }
            }

            if (j == signature.Length)
            {
                Console.WriteLine($"Found at {(baseAddress + i - j).ToString("X")}");
                for (int z = 0; z < signature.Length; z++)
                {
                    Console.Write($"{source[z + i - j].ToString("X")} ");
                }
                Console.WriteLine();
                running = false;
                return baseAddress + i - j;
            }
            return -1;
        }

        private int[] initKMP(string[] signature)
        {
            int[] lps = new int[signature.Length];
            int i = 1, j = 0;

            lps[0] = 0;

            while (i < lps.Length)
            {
                if (signature[i] == signature[j])
                {
                    j++;
                    lps[i - 1] = j;
                    i++;
                }
                else if (j == 0)
                {
                    lps[i] = 0;
                    i++;
                }
                else
                {
                    j = lps[j - 1];
                }
            }

            return lps;
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
