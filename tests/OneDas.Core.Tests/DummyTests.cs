using Microsoft.DotNet.PlatformAbstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using OneDas.PackageManagement;
using System.IO;
using Xunit;

namespace OneDas.Core.Tests
{
    public class DummyTests
    {
        [Fact]
        public async void OneDasPackageManagerCreatesAssetsFile()
        {
            var optionsMock = new Mock<IOptions<OneDasOptions>>();
            optionsMock.SetupGet(x => x.Value).Returns(new OneDasOptions());
            optionsMock.Object.Value.RestoreRuntimeId = RuntimeEnvironment.GetRuntimeIdentifier();

            var loggerFactory = new LoggerFactory();
            var packageManager = new OneDasPackageManager(optionsMock.Object, loggerFactory);

            Directory.CreateDirectory(optionsMock.Object.Value.NugetLocalDirectoryPath);

            // TODO: upload ExtensionSample to allow testing
            await packageManager.InstallAsync("OneDas.Extension.Mat73");
        }

        [Fact]
        public void FactTest()
        {
            Assert.False(true == false, "true should be equal to true");
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(0)]
        public void TheoryTest(int value)
        {
            switch (value)
            {
                case -1:
                    Assert.True(value < 1, $"{value} should be < 1");
                    break;

                case 0:
                    Assert.True(value < 1, $"{value} should be < 1");
                    break;

                default:
                    Assert.False(value < 1, $"{value} should be < 1");
                    break;
            }
        }
    }
}