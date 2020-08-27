using HandBrake_daemon;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace HandbrakeCLI_daemonUnitTest
{
    public class TestQueue
    {
        List<string> testRegexStrings;
        string asmDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        string testDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + Path.DirectorySeparatorChar + "TestMedia" + Path.DirectorySeparatorChar;
        //List<string> testMedia = new List<string> { "test", "test" + Path.DirectorySeparatorChar + "testing" };
        //List<string> testnumberedsrt = new List<string> { "2_eng.srt", "4_fre.srt" };
        //List<string> testmedianamesrt = new List<string> { ".eng.srt", ".fre.srt", ".(2_English).srt"};

        [TearDown]
        public void TearDown()
        {
            GC.Collect();
            var x = asmDir + Path.DirectorySeparatorChar + "TestMedia" + Path.DirectorySeparatorChar;
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
            Assert.AreEqual("und", QueueService.GetSubs(testDir + "testMedia.mp4").Item2[0]);
        }
        [Test]
        public void TestFNameContainsLangReturnsLang()
        {
            FileCreate("testMedia.mp4");
            FileCreate("testMedia.English.srt");
            Assert.AreEqual("English", QueueService.GetSubs(testDir + "testMedia.mp4").Item2[0]);
        }
        [Test]
        public void TestLangsForSRTNamesOnly()
        {
            FileCreate("testMedia.mp4");
            FileCreate("Subs\\2_English.srt");
            FileCreate("Subs\\3_French.srt");
            Assert.AreEqual("English", QueueService.GetSubs(testDir + "testMedia.mp4").Item2[0]);
            Assert.AreEqual("French", QueueService.GetSubs(testDir + "testMedia.mp4").Item2[1]);
        }
        [Test]
        public void TestIdentialNameInSubsDirectory()
        {
            FileCreate("testMediaOne.mp4");
            FileCreate("Subs\\testMediaOne.srt");
            FileCreate("testMediaTwo.mp4");
            FileCreate("Subs\\testMediaTwo.srt");
            Assert.AreEqual("und", QueueService.GetSubs(testDir + "testMediaOne.mp4").Item2[0]);
            Assert.AreEqual(1, QueueService.GetSubs(testDir + "testMediaOne.mp4").Item2.Count);
            Assert.AreEqual("und", QueueService.GetSubs(testDir + "testMediaTwo.mp4").Item2[0]);
            Assert.AreEqual(1, QueueService.GetSubs(testDir + "testMediaTwo.mp4").Item2.Count);
        }
        [Test]
        public void testTorSubsNotAdded()
        {
            FileCreate("the.expanse.s01e04.1080p.bluray.x264-rovers.mkv");
            FileCreate("Subs\\The.Expanse.S01E04.1080p.BluRay.x264-ROVERS.srt");
            Assert.AreEqual("und", QueueService.GetSubs(testDir + "the.expanse.s01e04.1080p.bluray.x264-rovers.mkv").Item2[0]);
        }
        private void FileCreate(string path)
        {
            System.IO.FileInfo file = new System.IO.FileInfo(asmDir + Path.DirectorySeparatorChar + "TestMedia" + Path.DirectorySeparatorChar + path);
            file.Directory.Create();
            File.Create(file.FullName);
        }

    }
    public class TestSRT
    {
        
    }
}