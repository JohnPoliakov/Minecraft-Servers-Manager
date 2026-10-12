using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Minecraft_Server_Manager.Services;

/// <summary>Owns a Windows job, including children started by a batch launcher.</summary>
public sealed class ProcessGroup : IDisposable
{
    private readonly SafeFileHandle _job;
    private long _lastTimestamp;
    private long _lastCpuTicks;

    public ProcessGroup()
    {
        _job = CreateJobObject(IntPtr.Zero, null);
        if (_job.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
        var limits = new ExtendedLimits();
        limits.Basic.LimitFlags = 0x2000; // JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
        if (!SetInformationJobObject(_job, 9, ref limits, Marshal.SizeOf<ExtendedLimits>()))
        {
            var error = Marshal.GetLastWin32Error();
            _job.Dispose();
            throw new Win32Exception(error);
        }
    }

    public void Assign(Process process)
    {
        if (!AssignProcessToJobObject(_job, process.Handle)) throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    public ProcessMetrics Sample()
    {
        if (!QueryInformationJobObject(_job, 1, out Accounting accounting, Marshal.SizeOf<Accounting>(), IntPtr.Zero))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        long now = Stopwatch.GetTimestamp();
        long ticks = accounting.UserTime + accounting.KernelTime;
        double? cpu = _lastTimestamp == 0 ? null : Math.Clamp(
            TimeSpan.FromTicks(Math.Max(0, ticks - _lastCpuTicks)).TotalSeconds /
            Stopwatch.GetElapsedTime(_lastTimestamp, now).TotalSeconds / Environment.ProcessorCount * 100, 0, 100);
        _lastTimestamp = now;
        _lastCpuTicks = ticks;

        long memory = 0;
        int readable = 0;
        var ids = ReadProcessIds();
        foreach (int id in ids)
        {
            try
            {
                using var process = Process.GetProcessById(id);
                process.Refresh();
                memory += process.WorkingSet64;
                readable++;
            }
            catch (ArgumentException) { } // Exited between snapshot and read.
            catch (InvalidOperationException) { }
            catch (Win32Exception) { }
        }
        return new ProcessMetrics(cpu, readable == 0 && accounting.ActiveProcesses > 0 ? null : memory / 1048576d,
            (int)accounting.ActiveProcesses);
    }

    public int ActiveProcessCount
    {
        get
        {
            if (!QueryInformationJobObject(_job, 1, out Accounting info, Marshal.SizeOf<Accounting>(), IntPtr.Zero))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            return (int)info.ActiveProcesses;
        }
    }

    private int[] ReadProcessIds()
    {
        int capacity = 32;
        while (capacity <= 65536)
        {
            int size = 8 + capacity * IntPtr.Size;
            IntPtr buffer = Marshal.AllocHGlobal(size);
            try
            {
                if (QueryInformationJobObjectBuffer(_job, 3, buffer, size, IntPtr.Zero))
                {
                    int count = Marshal.ReadInt32(buffer, 4);
                    return Enumerable.Range(0, count).Select(i => checked((int)Marshal.ReadIntPtr(buffer, 8 + i * IntPtr.Size))).ToArray();
                }
                int error = Marshal.GetLastWin32Error();
                if (error != 234) throw new Win32Exception(error); // ERROR_MORE_DATA
            }
            finally { Marshal.FreeHGlobal(buffer); }
            capacity *= 2;
        }
        throw new InvalidOperationException("Process group exceeds the supported process count.");
    }

    public void Kill()
    {
        if (!TerminateJobObject(_job, 1)) throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    public void Dispose() => _job.Dispose();

    [StructLayout(LayoutKind.Sequential)] private struct BasicLimits
    {
        public long ProcessTime, JobTime;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSet, MaximumWorkingSet;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass, SchedulingClass;
    }
    [StructLayout(LayoutKind.Sequential)] private struct IoCounters
    {
        public ulong ReadOperations, WriteOperations, OtherOperations, ReadBytes, WriteBytes, OtherBytes;
    }
    [StructLayout(LayoutKind.Sequential)] private struct ExtendedLimits
    {
        public BasicLimits Basic;
        public IoCounters Io;
        public UIntPtr ProcessMemory, JobMemory, PeakProcessMemory, PeakJobMemory;
    }
    [StructLayout(LayoutKind.Sequential)] private struct Accounting
    {
        public long UserTime, KernelTime, PeriodUserTime, PeriodKernelTime;
        public uint PageFaults, TotalProcesses, ActiveProcesses, TerminatedProcesses;
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateJobObject(IntPtr attributes, string? name);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetInformationJobObject(SafeFileHandle job, int type, ref ExtendedLimits info, int size);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AssignProcessToJobObject(SafeFileHandle job, IntPtr process);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryInformationJobObject(SafeFileHandle job, int type, out Accounting info, int size, IntPtr returned);
    [DllImport("kernel32.dll", EntryPoint = "QueryInformationJobObject", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryInformationJobObjectBuffer(SafeFileHandle job, int type, IntPtr info, int size, IntPtr returned);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TerminateJobObject(SafeFileHandle job, uint code);
}

public sealed record ProcessMetrics(double? CpuPercent, double? ResidentMegabytes, int ProcessCount);
