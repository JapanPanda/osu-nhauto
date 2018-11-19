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
      
        public Dictionary<string[], int> FindSignature(List<string[]> signatures, int start = -1)
        {
            int addressesFound = 0;
            Dictionary<string[], int> addressMap = new Dictionary<string[], int>();

            int startAddress = start > -1 ? start : (int)process.MainModule.BaseAddress;
            int currentAddress = startAddress;
            uint mbiSize = (uint)Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION));

            foreach (string[] signature in signatures)
            {
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
                            //int index = FindPattern(buffer, signature, search, ref running);
                            bool updated = FindPattern(buffer, signature, (int)mbi.BaseAddress, ref addressMap, ref running);
                                if (updated)
                                {
                                    Console.WriteLine("Found some address");
                                    addressesFound++;

                                    if (addressMap.Count == signatures.Count)
                                    {
                                        Console.WriteLine($"{addressMap.Count} == {signatures.Count}");
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
            }

            if (addressMap.Count != 0)
            {
                return addressMap;
            } 

            else
                throw new Exception("Signatures not found");
        }


        private bool FindPattern(byte[] source, string[] signature, int baseAddress, ref Dictionary<string[], int> addressMap, ref bool running)
        {
            bool sigFound = false;
            int i = 0, j = 0;
            int[] lps = initKMP(signature);
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
                addressMap.Add(signature, baseAddress + i - j);
                running = false;
                sigFound = true;
            }
            return sigFound;
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
        
        // have to find a way to find / match multiple strings
        private bool FindPatterns(byte[] source, List<string[]> signatures, int baseAddress, ref Dictionary<string[], int> addressMap, ref bool running)
        {
            bool sigFound = false;
            for (int a = 0; a < signatures.Count; a++)
            {
                String[] signature = signatures[a];
                int size = source.Length - signature.Length;
                for (int i = 0; i < size && running; i++)
                {
                    if (signatures[a][0] == "-")
                        break;

                    if (signature[0] != "??" && source[i].ToString("X") == signature[0])
                    {
                        int counter = 1;
                        while (counter < signature.Length)
                        {
                            if (signature[counter] == "??" || (signature[counter] != "??" && source[i + counter].ToString("X") == signature[counter]))
                                counter++;

                            else
                                break;
                        }
                        if (counter == signature.Length)
                        {
                            int fill;
                            if (!addressMap.TryGetValue(signature, out fill))
                            {
                                signatures[a][0] = "-";
                                addressMap.Add(signature, i + baseAddress);
                                sigFound = true;
                                break;
                            }
                        }
                    }
                }
            }
            return sigFound;
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
