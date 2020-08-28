using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace HandbrakeCLI_daemonUnitTest
{
    static class TestHelper
    {
        public static string ASMDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        public static string TESTDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + Path.DirectorySeparatorChar + "TestMedia" + Path.DirectorySeparatorChar;
        public static void FileCreate(string path)
        {
            System.IO.FileInfo file = new System.IO.FileInfo(ASMDir + Path.DirectorySeparatorChar + "TestMedia" + Path.DirectorySeparatorChar + path);
            file.Directory.Create();
            File.Create(file.FullName);
        }
    }
}
