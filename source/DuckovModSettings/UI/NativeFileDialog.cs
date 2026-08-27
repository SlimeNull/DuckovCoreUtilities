using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using Process = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;

namespace SlimeNull.DuckovModSettings.UI
{
    internal static class NativeFileDialog
    {
        private const int MaximumPathLength = 32768;
        private const int OpenFileExplorer = 0x00080000;
        private const int OpenFileNoChangeDirectory = 0x00000008;
        private const int OpenFilePathMustExist = 0x00000800;
        private const int OpenFileMustExist = 0x00001000;

        public static bool IsValidFilter(string? filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
            {
                return false;
            }

            var parts = filter.Split('|');
            if (parts.Length < 2 || parts.Length % 2 != 0)
            {
                return false;
            }

            foreach (var part in parts)
            {
                if (string.IsNullOrWhiteSpace(part))
                {
                    return false;
                }
            }
            return true;
        }

        public static bool TryOpen(string filter, string? currentPath, out string selectedPath)
        {
            selectedPath = string.Empty;
            try
            {
                return Application.platform switch
                {
                    RuntimePlatform.WindowsEditor or RuntimePlatform.WindowsPlayer =>
                        TryOpenWindows(filter, currentPath, out selectedPath),
                    RuntimePlatform.OSXEditor or RuntimePlatform.OSXPlayer =>
                        TryOpenMac(filter, currentPath, out selectedPath),
                    _ => false,
                };
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DuckovModSettings] Could not open the file dialog: {ex.Message}");
                return false;
            }
        }

        private static bool TryOpenWindows(string filter, string? currentPath, out string selectedPath)
        {
            selectedPath = string.Empty;
            var filterPointer = IntPtr.Zero;
            var filePointer = IntPtr.Zero;
            var directoryPointer = IntPtr.Zero;
            try
            {
                filterPointer = Marshal.StringToHGlobalUni(ToWindowsFilter(filter));
                filePointer = Marshal.AllocHGlobal(MaximumPathLength * sizeof(char));
                Marshal.WriteInt16(filePointer, 0, 0);

                var initialDirectory = GetInitialDirectory(currentPath);
                if (!string.IsNullOrEmpty(initialDirectory))
                {
                    directoryPointer = Marshal.StringToHGlobalUni(initialDirectory);
                }

                var dialog = new OpenFileName
                {
                    StructureSize = Marshal.SizeOf(typeof(OpenFileName)),
                    Owner = GetActiveWindow(),
                    Filter = filterPointer,
                    FilterIndex = 1,
                    File = filePointer,
                    MaximumFileLength = MaximumPathLength,
                    InitialDirectory = directoryPointer,
                    Flags = OpenFileExplorer | OpenFileNoChangeDirectory | OpenFilePathMustExist | OpenFileMustExist,
                };

                if (!GetOpenFileName(ref dialog))
                {
                    return false;
                }

                selectedPath = Marshal.PtrToStringUni(filePointer) ?? string.Empty;
                return !string.IsNullOrWhiteSpace(selectedPath);
            }
            finally
            {
                Free(filterPointer);
                Free(filePointer);
                Free(directoryPointer);
            }
        }

        private static bool TryOpenMac(string filter, string? currentPath, out string selectedPath)
        {
            selectedPath = string.Empty;
            var command = new StringBuilder("choose file");
            var extensions = GetExtensions(filter);
            if (extensions.Count > 0)
            {
                command.Append(" of type {");
                for (var i = 0; i < extensions.Count; i++)
                {
                    if (i > 0)
                    {
                        command.Append(", ");
                    }
                    command.Append('"').Append(EscapeAppleScript(extensions[i])).Append('"');
                }
                command.Append('}');
            }

            var initialDirectory = GetInitialDirectory(currentPath);
            if (!string.IsNullOrEmpty(initialDirectory))
            {
                command.Append(" default location POSIX file \"")
                    .Append(EscapeAppleScript(initialDirectory))
                    .Append('"');
            }

            var script =
                "try\n" +
                "set selectedFile to " + command + "\n" +
                "return POSIX path of selectedFile\n" +
                "on error number -128\n" +
                "return \"\"\n" +
                "end try";
            var startInfo = new ProcessStartInfo
            {
                FileName = "/usr/bin/osascript",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return false;
            }

            process.StandardInput.Write(script);
            process.StandardInput.Close();
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                Debug.LogWarning($"[DuckovModSettings] macOS file dialog failed: {error.Trim()}");
                return false;
            }

            selectedPath = output.TrimEnd('\r', '\n');
            return !string.IsNullOrWhiteSpace(selectedPath);
        }

        private static string ToWindowsFilter(string filter)
        {
            var parts = filter.Split('|');
            if (!IsValidFilter(filter))
            {
                parts = new[] { "All Files", "*.*" };
            }
            return string.Join("\0", parts) + "\0\0";
        }

        private static List<string> GetExtensions(string filter)
        {
            var result = new List<string>();
            var parts = filter.Split('|');
            for (var i = 1; i < parts.Length; i += 2)
            {
                foreach (var pattern in parts[i].Split(';'))
                {
                    var extension = Path.GetExtension(pattern.Trim()).TrimStart('.');
                    if (!string.IsNullOrWhiteSpace(extension) &&
                        extension != "*" &&
                        !result.Contains(extension))
                    {
                        result.Add(extension);
                    }
                }
            }
            return result;
        }

        private static string? GetInitialDirectory(string? currentPath)
        {
            if (string.IsNullOrWhiteSpace(currentPath))
            {
                return null;
            }

            try
            {
                var fullPath = Path.GetFullPath(currentPath);
                if (Directory.Exists(fullPath))
                {
                    return fullPath;
                }

                var directory = Path.GetDirectoryName(fullPath);
                return !string.IsNullOrEmpty(directory) && Directory.Exists(directory)
                    ? directory
                    : null;
            }
            catch
            {
                return null;
            }
        }

        private static string EscapeAppleScript(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static void Free(IntPtr pointer)
        {
            if (pointer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(pointer);
            }
        }

        [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetOpenFileName(ref OpenFileName openFileName);

        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct OpenFileName
        {
            public int StructureSize;
            public IntPtr Owner;
            public IntPtr Instance;
            public IntPtr Filter;
            public IntPtr CustomFilter;
            public int MaximumCustomFilterLength;
            public int FilterIndex;
            public IntPtr File;
            public int MaximumFileLength;
            public IntPtr FileTitle;
            public int MaximumFileTitleLength;
            public IntPtr InitialDirectory;
            public IntPtr Title;
            public int Flags;
            public short FileOffset;
            public short FileExtension;
            public IntPtr DefaultExtension;
            public IntPtr CustomData;
            public IntPtr Hook;
            public IntPtr TemplateName;
            public IntPtr Reserved;
            public int ReservedSize;
            public int ExtendedFlags;
        }
    }
}
