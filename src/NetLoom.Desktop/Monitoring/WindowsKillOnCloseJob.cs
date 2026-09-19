using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace NetLoom.Desktop.Monitoring
{
    internal sealed class WindowsKillOnCloseJob :
        IDisposable
    {
        private const uint JobObjectLimitKillOnJobClose =
            0x00002000;

        private IntPtr _handle;

        public WindowsKillOnCloseJob()
        {
            _handle =
                CreateJobObject(
                    IntPtr.Zero,
                    null);

            if (_handle == IntPtr.Zero)
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error());
            }

            var information =
                new JobObjectExtendedLimitInformation();

            information.BasicLimitInformation.LimitFlags =
                JobObjectLimitKillOnJobClose;

            var length =
                Marshal.SizeOf(
                    typeof(JobObjectExtendedLimitInformation));

            var pointer =
                Marshal.AllocHGlobal(
                    length);

            try
            {
                Marshal.StructureToPtr(
                    information,
                    pointer,
                    false);

                if (!SetInformationJobObject(
                    _handle,
                    9,
                    pointer,
                    (uint)length))
                {
                    throw new Win32Exception(
                        Marshal.GetLastWin32Error());
                }
            }
            catch
            {
                Dispose();
                throw;
            }
            finally
            {
                Marshal.FreeHGlobal(
                    pointer);
            }
        }

        public void Assign(
            Process process)
        {
            if (process == null)
            {
                throw new ArgumentNullException(
                    nameof(process));
            }

            if (_handle == IntPtr.Zero)
            {
                throw new ObjectDisposedException(
                    nameof(WindowsKillOnCloseJob));
            }

            if (!AssignProcessToJobObject(
                _handle,
                process.Handle))
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error());
            }
        }

        public void Dispose()
        {
            if (_handle == IntPtr.Zero)
            {
                return;
            }

            CloseHandle(
                _handle);

            _handle = IntPtr.Zero;
        }

        [DllImport(
            "kernel32.dll",
            CharSet = CharSet.Unicode,
            SetLastError = true)]
        private static extern IntPtr CreateJobObject(
            IntPtr securityAttributes,
            string name);

        [DllImport(
            "kernel32.dll",
            SetLastError = true)]
        private static extern bool SetInformationJobObject(
            IntPtr job,
            int informationClass,
            IntPtr jobObjectInformation,
            uint jobObjectInformationLength);

        [DllImport(
            "kernel32.dll",
            SetLastError = true)]
        private static extern bool AssignProcessToJobObject(
            IntPtr job,
            IntPtr process);

        [DllImport(
            "kernel32.dll",
            SetLastError = true)]
        private static extern bool CloseHandle(
            IntPtr handle);

        [StructLayout(LayoutKind.Sequential)]
        private struct JobObjectBasicLimitInformation
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IoCounters
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JobObjectExtendedLimitInformation
        {
            public JobObjectBasicLimitInformation
                BasicLimitInformation;

            public IoCounters IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }
    }
}
