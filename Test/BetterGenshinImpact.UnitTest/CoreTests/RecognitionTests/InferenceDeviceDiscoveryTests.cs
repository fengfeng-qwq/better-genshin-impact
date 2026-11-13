using BetterGenshinImpact.Core.Recognition.ONNX;

namespace BetterGenshinImpact.UnitTest.CoreTests.RecognitionTests;

public sealed class InferenceDeviceDiscoveryTests
{
    private const string Amd = "1002:1638";
    private const string Nvidia = "10DE:25E0";
    private const string AmdName = "AMD Radeon(TM) Graphics";
    private const string NvidiaName = "NVIDIA GeForce RTX 3050 Ti Laptop GPU";

    private static DirectMlAdapterCandidate Pci(string model, int index, string name, string pci)
        => new(model, true, new($"DXGI:{model}:{pci}", name, index, index.ToString()));

    // 非 PCI 入口的 D3DKMT 地址查询会成功，但返回全 F 的无效地址，因此同型号入口的身份完全相同。
    private static DirectMlAdapterCandidate NonPci(string model, int index, string name)
        => new(model, false, new($"DXGI:{model}:4294967295:65535:65535", name, index, index.ToString()));

    private static IReadOnlyList<InferenceDeviceInfo> Resolve(DirectMlAdapterCandidate[] candidates)
        => InferenceDeviceDiscovery.NormalizeDirectMlDevices(
            InferenceDeviceDiscovery.FilterVirtualAdapterDuplicates(candidates));

    [Fact]
    public void VirtualDisplayDuplicates_AreDroppedWhenSameModelPhysicalAdapterIsListed()
    {
        // 实机数据：三个 IddCx 虚拟显示驱动让 DXGI 为同一块 AMD 核显多报了三个非 PCI 入口。
        var devices = Resolve(
        [
            Pci(Amd, 0, AmdName, "6:0:0"),
            Pci(Nvidia, 1, NvidiaName, "1:0:0"),
            NonPci(Amd, 2, AmdName),
            NonPci(Amd, 3, AmdName),
            NonPci(Amd, 4, AmdName)
        ]);

        Assert.Equal(2, devices.Count);
        Assert.Equal(AmdName, devices[0].Name);
        Assert.Equal(0, devices[0].Index);
        Assert.Equal(NvidiaName, devices[1].Name);
        Assert.Equal(1, devices[1].Index);
    }

    [Fact]
    public void VirtualDuplicates_AreDroppedRegardlessOfEnumerationOrder()
    {
        var devices = Resolve(
        [
            NonPci(Amd, 0, AmdName),
            Pci(Amd, 1, AmdName, "6:0:0")
        ]);

        Assert.Single(devices);
        Assert.Equal(1, devices[0].Index);
        Assert.Equal(AmdName, devices[0].Name);
    }

    [Fact]
    public void NonPciAdapters_AreKeptWhenNoPhysicalAdapterExists()
    {
        // 虚拟机 GPU 直通场景：唯一的可用显卡就是非 PCI 入口，过滤后不能空掉。
        var devices = Resolve(
        [
            NonPci(Amd, 0, AmdName),
            NonPci(Nvidia, 1, "NVIDIA Virtual GPU")
        ]);

        Assert.Equal(2, devices.Count);
    }

    [Fact]
    public void NonPciAdapterOfDifferentModel_IsKept()
    {
        var devices = Resolve(
        [
            Pci(Amd, 0, AmdName, "6:0:0"),
            NonPci(Nvidia, 1, "NVIDIA Virtual GPU")
        ]);

        Assert.Equal(2, devices.Count);
        Assert.Equal("NVIDIA Virtual GPU", devices[1].Name);
    }

    [Fact]
    public void TwoPhysicalAdaptersOfSameModel_KeepOriginalIndexInSuffix()
    {
        var devices = Resolve(
        [
            Pci(Amd, 0, AmdName, "6:0:0"),
            Pci(Nvidia, 1, NvidiaName, "1:0:0"),
            Pci(Amd, 2, AmdName, "7:0:0")
        ]);

        Assert.Equal(3, devices.Count);
        Assert.Equal($"{AmdName}（设备 1）", devices[0].Name);
        Assert.Equal(NvidiaName, devices[1].Name);
        Assert.Equal($"{AmdName}（设备 3）", devices[2].Name);
    }
}
