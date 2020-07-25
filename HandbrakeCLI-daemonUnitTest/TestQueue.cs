using HandbrakeCLI_daemon;
using NUnit.Framework;
using System.Collections.Generic;

namespace HandbrakeCLI_daemonUnitTest
{
    public class TestQueue
    {
        List<string> testRegexStrings;

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
    }
}