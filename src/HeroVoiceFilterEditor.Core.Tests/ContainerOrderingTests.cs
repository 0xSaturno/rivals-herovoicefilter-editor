using HeroVoiceFilterEditor.Core.Game;
using Xunit;

namespace HeroVoiceFilterEditor.Core.Tests;

public class ContainerOrderingTests
{
    private static readonly string[] Containers =
    [
        @"C:\Paks\pakchunkWwise-Windows.utoc",
        @"C:\Paks\global.utoc",
        @"C:\Paks\Patch_-Windows_1.1.3805839_P.utoc",
        @"C:\Paks\pakchunk0-Windows.utoc",
        @"C:\Paks\Patch_-Windows_1.1.3780998_P.utoc",
        @"C:\Paks\Patch_-Windows_1.1.3791970_P.utoc"
    ];

    [Fact]
    public void Order_PutsBaseContainersBeforePatches()
    {
        IReadOnlyList<string> ordered = ContainerOrdering.Order(Containers);

        int firstPatchIndex = ordered.ToList().FindIndex(ContainerOrdering.IsPatchContainer);
        for (int i = 0; i < firstPatchIndex; i++)
            Assert.False(ContainerOrdering.IsPatchContainer(ordered[i]));
    }

    [Fact]
    public void Order_SortsPatchesByAscendingBuild()
    {
        IReadOnlyList<string> ordered = ContainerOrdering.Order(Containers);
        List<long> patchBuilds = ordered
            .Where(ContainerOrdering.IsPatchContainer)
            .Select(p => ContainerOrdering.PatchBuild(p)!.Value)
            .ToList();

        Assert.Equal(new List<long> { 3780998, 3791970, 3805839 }, patchBuilds);
    }

    [Fact]
    public void Order_ExcludesGlobalUtoc()
    {
        IReadOnlyList<string> ordered = ContainerOrdering.Order(Containers);
        Assert.DoesNotContain(ordered, ContainerOrdering.IsGlobalContainer);
    }

    [Fact]
    public void PatchBuild_ParsesTheDigitsBeforeUnderscoreP()
    {
        // Regression: an earlier version required "_" immediately before the digits, but real
        // container names have a dot there (Patch_-Windows_1.1.3805839_P.utoc), which silently
        // left every build unparsed.
        long? build = ContainerOrdering.PatchBuild(@"C:\Paks\Patch_-Windows_1.1.3805839_P.utoc");
        Assert.Equal(3805839, build);
    }

    [Fact]
    public void DescribeBuild_ReturnsTheHighestPatchBuild()
    {
        Assert.Equal("3805839", ContainerOrdering.DescribeBuild(Containers));
    }

    [Fact]
    public void DescribeBuild_FallsBackToBase_WhenNoPatchesPresent()
    {
        string[] baseOnly = [@"C:\Paks\global.utoc", @"C:\Paks\pakchunk0-Windows.utoc"];
        Assert.Equal("base", ContainerOrdering.DescribeBuild(baseOnly));
    }

    [Fact]
    public void IsPatchContainer_RejectsNonPatchNames()
    {
        Assert.False(ContainerOrdering.IsPatchContainer(@"C:\Paks\pakchunkWwise-Windows.utoc"));
    }
}
