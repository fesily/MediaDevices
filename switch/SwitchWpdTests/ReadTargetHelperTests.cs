using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SwitchWpd.Tests
{
    [TestClass()]
    public class ReadTargetHelperTests
    {
        [TestInitialize]
        public void SetRoot()
        {
            TilesManager.Root = "G:\\switch";
        }
        [TestMethod()]
        public void ReadMultiDirTest()
        {
        }

        [TestMethod()]
        public void IsMultiDirLineTest()
        {
            Assert.IsTrue(ReadTargetHelper.IsMultiDirLine(@"G:\switch\1 G:\switch\2"));
        }
    }
}