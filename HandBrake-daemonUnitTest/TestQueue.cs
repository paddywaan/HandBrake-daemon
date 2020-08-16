using Handbrake_daemon;
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
        List<string> testMedia = new List<string> { "test", "test" + Path.DirectorySeparatorChar + "testing" };
        List<string> testnumberedsrt = new List<string> { "2_eng.srt", "4_fre.srt" };
        List<string> testmedianamesrt = new List<string> { ".eng.srt", ".fre.srt", ".(2_English).srt"};


        [SetUp]
        public void Setup()
        {
            foreach (var media in testMedia)
            {
                FileCreate(media);
            }
            foreach(var numbersrt in testnumberedsrt)
            {
                FileCreate("Subs" + Path.DirectorySeparatorChar + numbersrt);
            }

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
        public void TestGetSubsReturnsParsedInputs()
        {
            foreach (var file in Directory.GetFiles(asmDir + Path.DirectorySeparatorChar + "TestMedia"))
                {

            }

        }
        private void FileCreate(string path)
        {
            System.IO.FileInfo file = new System.IO.FileInfo(asmDir + Path.DirectorySeparatorChar + "TestMedia" + Path.DirectorySeparatorChar + path);
            file.Directory.Create();
            File.Create(file.FullName);
        }
    }
}