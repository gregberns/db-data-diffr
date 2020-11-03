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
    public class CleanTest
    {

        [Fact]
        public void StripTimeFromDate()
        {
            Assert.Equal("1997-04-01T00:00:00", Clean.StripTimeFromDate("1997-04-01 12:23:45"));
        }

        [Fact]
        public void BuildAnonUrl()
        {
            var dict = new Dictionary<string, object>(){
                {"seed", (decimal)1234 },
                {"nat", "United States"},
                {"gender", "Male"}
            };

            var url = Clean.BuildAnonUrl(dict);

            Assert.Equal("?seed=1234&nat=us&gender=male", url);
        }
    }
}