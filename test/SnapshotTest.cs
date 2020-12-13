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
    public class SnapshotTest
    {

        [Fact]
        public void FormatCell()
        {
            Assert.Equal("\"abc,def\"", Snapshot.FormatCell("abc,def"));
            Assert.Equal("\"ab\"\"c\"\"def\"", Snapshot.FormatCell("ab\"c\"def"));

            Assert.Equal("abc\\ndef", Snapshot.FormatCell("abc\ndef"));
            Assert.Equal("abc\\ndef", Snapshot.FormatCell("abc\r\ndef"));

        }
    }
}