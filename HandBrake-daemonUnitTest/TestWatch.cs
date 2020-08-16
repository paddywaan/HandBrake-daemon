using System;
using System.Collections.Generic;
using System.Text;
using Handbrake_daemon;
using NUnit.Framework;
using System.IO;
using NUnit.Framework.Internal;

namespace HandbrakeCLI_daemonUnitTest
{
    class TestWatch
    {
        private List<string> validPaths;
        private List<string> invalidPaths;
        private const string fPath = "testdb.conf";
        private const string exts = "mp4,mkv,avi";
        private const string profile = @"‪D:\Elliot\Desktop\h265-24RF-Fast.json";
        [SetUp]
        public void Setup()
        {
            validPaths = new List<string>() { @"D:\Elliot\Videos\test"/*, "\"D:\\Elliot\\Videos\\test\""*/ };
            invalidPaths = new List<string>() { @"D:\Elliot\Tools\test test" };
        }

        [Test]
        public void TestReadConfDB()
        {
            using StreamWriter sw = new StreamWriter(fPath);
            var sb = new StringBuilder();
            sb.Append("#Test!"+Environment.NewLine);
            sb.Append($"{validPaths[0]} {validPaths[0]} {validPaths[0]} {profile} {exts} true");
            sw.WriteLine(sb);
            sw.Close();
            var asd = WatcherService.ReadConf(fPath);
            Assert.AreEqual(1, asd.Count);
            Assert.AreEqual(validPaths[1], asd[0].Origin);
        }
    }
}
