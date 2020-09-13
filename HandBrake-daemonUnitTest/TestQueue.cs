using HandBrake_daemon;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using static HandbrakeCLI_daemonUnitTest.TestHelper;

namespace HandbrakeCLI_daemonUnitTest
{
    public class TestQueue
    {
        List<string> testRegexStrings;

        [TearDown]
        public void TearDown()
        {
            GC.Collect();
            var x = ASMDir + Path.DirectorySeparatorChar + "TestMedia" + Path.DirectorySeparatorChar;
            if (Directory.Exists(x)) Directory.Delete(x, true);
        }

        [SetUp]
        public void Setup()
        {

            testRegexStrings = new List<string> { "asdqwhasfd1234985413243213!\"$$^£% () ^%£$!.2_English.srt",
                    "asdqwhasfd1234985413243213!$$^£%()^%£$!.Eng.srt",
                    "asdqwhasfd1234985413243213!$$^£%()^%£$!.ENG.srt",
                    "asdqwhasfd1234985413243213!$$^£%()^%£$!.eng.srt",
                    "asdqwhasfd1234985413243213!$$^£%()^%£$!.(2_English).srt",
                    "asdqwhasfd1234985413243213!$$^£%()^%£$!.asdqwhasfd1234985413243213!$$^£%()^%£$!.srt",
                    "2_English.srt",
                    "4_French.srt",
            };
        }

        [Test]
        public void TestRegexParsesSRTLanguage()
        {
            Assert.AreEqual("English", QueueService.GetSubLang(testRegexStrings[0]));
            Assert.AreEqual("Eng", QueueService.GetSubLang(testRegexStrings[1]));
            Assert.AreEqual("ENG", QueueService.GetSubLang(testRegexStrings[2]));
            Assert.AreEqual("eng", QueueService.GetSubLang(testRegexStrings[3]));
            Assert.AreEqual("English", QueueService.GetSubLang(testRegexStrings[4]));
            Assert.AreEqual("English", QueueService.GetSubLang(testRegexStrings[6]));
            Assert.AreEqual("French", QueueService.GetSubLang(testRegexStrings[7]));
        }

        [Test]
        public void TestRegexParseFailureReturnsUnd()
        {
            Assert.AreEqual("und", QueueService.GetSubLang(testRegexStrings[5]));
        }

        [Test]
        public void TestIdenticalFNameSRTReturnsUndLang()
        {
            FileCreate("testMedia.mp4");
            FileCreate("testMedia.srt");
            Assert.AreEqual("und", QueueService.GetSubs(TESTDir + "testMedia.mp4").Item2[0]);
        }
        [Test]
        public void TestFNameContainsLangReturnsLang()
        {
            FileCreate("testMedia.mp4");
            FileCreate("testMedia.English.srt");
            Assert.AreEqual("English", QueueService.GetSubs(TESTDir + "testMedia.mp4").Item2[0]);
        }
        [Test]
        public void TestLangsForSRTNamesOnly()
        {
            FileCreate("testMedia.mp4");
            FileCreate("Subs\\2_English.srt");
            FileCreate("Subs\\3_French.srt");
            Assert.AreEqual("English", QueueService.GetSubs(TESTDir + "testMedia.mp4").Item2[0]);
            Assert.AreEqual("French", QueueService.GetSubs(TESTDir + "testMedia.mp4").Item2[1]);
        }
        [Test]
        public void TestIdentialNameInSubsDirectory()
        {
            FileCreate("testMediaOne.mp4");
            FileCreate("Subs\\testMediaOne.srt");
            FileCreate("testMediaTwo.mp4");
            FileCreate("Subs\\testMediaTwo.srt");
            Assert.AreEqual("und", QueueService.GetSubs(TESTDir + "testMediaOne.mp4").Item2[0]);
            Assert.AreEqual(1, QueueService.GetSubs(TESTDir + "testMediaOne.mp4").Item2.Count);
            Assert.AreEqual("und", QueueService.GetSubs(TESTDir + "testMediaTwo.mp4").Item2[0]);
            Assert.AreEqual(1, QueueService.GetSubs(TESTDir + "testMediaTwo.mp4").Item2.Count);
        }
        /// <summary>
        /// Required for non NTFS
        /// </summary>
        [Test]
        public void TestUpperCaseSensitiveDirectory()
        {
            FileCreate("testMedia.mp4");
            FileCreate("Subs\\testMedia.srt");
            Assert.AreEqual("und", QueueService.GetSubs(TESTDir + "testMedia.mp4").Item2[0]);
        }
        /// <summary>
        /// Required for non NTFS
        /// </summary>
        [Test]
        public void TestLowerCaseSensitiveDirectory()
        {
            FileCreate("testMedia.mp4");
            FileCreate("subs\\testMedia.srt");
            Assert.AreEqual("und", QueueService.GetSubs(TESTDir + "testMedia.mp4").Item2[0]);
        }
    }
}