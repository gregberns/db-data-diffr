using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Xunit;
using DbDataDiffr;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace test
{
    public class Helper
    {
        public static string GetApplicationRoot()
        {
            var exePath = Path.GetDirectoryName(System.Reflection
                              .Assembly.GetExecutingAssembly().CodeBase);
            var i = exePath.IndexOf("bin");
            var appRoot = exePath.Substring(0, i - 1).Replace("file:", "");
            return appRoot;
        }

    }
}
