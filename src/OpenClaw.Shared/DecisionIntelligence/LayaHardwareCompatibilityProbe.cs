using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using OpenClaw.Shared.Inference;

namespace OpenClaw.Shared.DecisionIntelligence;

/// <summary>
/// Probes host hardware for Laya runtime and model execution readiness.
/// </summary>
public static class LayaHardwareCompatibilityProbe
{
    public static LayaCompatibilityReport EvaluateCompatibility(
        HostHardwareInfo hardwareInfo,
        string targetInstallPath,
        string requestedModelId = "laya-english")
    {
        var report = new LayaCompatibilityReport
        {
            OperatingSystem = RuntimeInformation.OSDescription,
            Architecture = RuntimeInformation.OSArchitecture.ToString()
        };

        // 1. Architecture Check: Laya ONNX & Python runtimes are verified on x64 / Arm64
        bool archOk = RuntimeInformation.OSArchitecture == Architecture.X64 ||
                      RuntimeInformation.OSArchitecture == Architecture.Arm64;
        report.CpuSupported = archOk;
        if (!archOk)
        {
            report.IncompatibilityReasons.Add($"Unsupported processor architecture: {RuntimeInformation.OSArchitecture}. x64 or Arm64 is required.");
        }

        // 2. RAM Check
        var model = LayaModelRegistry.GetModel(requestedModelId) ?? LayaModelRegistry.GetRecommendedModel();
        report.RecommendedModelId = model.ModelId;

        if (hardwareInfo.TotalPhysicalMemoryBytes.HasValue)
        {
            report.TotalRamBytes = hardwareInfo.TotalPhysicalMemoryBytes.Value;
            report.AvailableRamBytes = hardwareInfo.AvailablePhysicalMemoryBytes ?? hardwareInfo.TotalPhysicalMemoryBytes.Value / 2;

            if (report.TotalRamBytes >= model.MinimumRamBytes)
            {
                report.RamSufficient = true;
            }
            else
            {
                report.RamSufficient = false;
                report.IncompatibilityReasons.Add($"Insufficient system memory. Required: {model.MinimumRamBytes / 1024 / 1024} MB, Installed: {report.TotalRamBytes / 1024 / 1024} MB.");
            }
        }
        else
        {
            // Fallback probe
            try
            {
                var mem = GC.GetGCMemoryInfo();
                report.TotalRamBytes = mem.TotalAvailableMemoryBytes;
                report.RamSufficient = report.TotalRamBytes >= model.MinimumRamBytes;
            }
            catch
            {
                report.RamSufficient = true; // Optimistic assumption if probe blocked
                report.Warnings.Add("Could not accurately query total physical RAM.");
            }
        }

        // 3. GPU / Hardware Acceleration Detection
        if (hardwareInfo.HasNvidiaGpu)
        {
            var nvidiaGpu = hardwareInfo.NvidiaGpus.FirstOrDefault();
            report.GpuName = nvidiaGpu?.Name ?? "NVIDIA GPU";
            report.HasGpuAcceleration = true;
            report.AccelerationType = "CUDA";
        }
        else if (hardwareInfo.Gpus.Count > 0)
        {
            var firstGpu = hardwareInfo.Gpus[0];
            report.GpuName = firstGpu.Name;
            report.HasGpuAcceleration = true;
            report.AccelerationType = "DirectML";
        }
        else
        {
            report.GpuName = "CPU Only";
            report.HasGpuAcceleration = false;
            report.AccelerationType = "CPU (AVX2 / Multi-Threaded)";
        }

        // 4. Storage Space Check
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(targetInstallPath)) ?? "C:\\";
            var drive = new DriveInfo(root);
            report.AvailableDiskSpaceBytes = drive.AvailableFreeSpace;

            long totalRequiredBytes = model.DownloadSizeBytes + model.ExtractedSizeBytes + 200_000_000L; // Runtime buffer
            if (report.AvailableDiskSpaceBytes >= totalRequiredBytes)
            {
                report.DiskSpaceSufficient = true;
            }
            else
            {
                report.DiskSpaceSufficient = false;
                report.IncompatibilityReasons.Add($"Insufficient storage space on drive {drive.Name}. Required: {totalRequiredBytes / 1024 / 1024} MB, Available: {report.AvailableDiskSpaceBytes / 1024 / 1024} MB.");
            }
        }
        catch (Exception ex)
        {
            report.DiskSpaceSufficient = true;
            report.Warnings.Add($"Could not inspect storage volume: {ex.Message}");
        }

        // Final compatibility verdict
        report.IsCompatible = report.CpuSupported && report.RamSufficient && report.DiskSpaceSufficient;

        return report;
    }
}
