using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Xunit;
using DbDataDiffr;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;
using static Logging.Logger;

namespace test
{
    public class CleanTest
    {
        public CleanTest()
        {
            InitLogger(Logging.LogLevel.Debug);
        }

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

        [Fact]
        public void CreateAnonPerson()
        {
            var fun = Clean.CreateAnonPerson();
            var person = fun("1");

            Assert.Equal("Justin", person.FirstName);
            Assert.Equal("Konopelski", person.LastName);
            Assert.Equal("Justin.Konopelski64@yahoo.com", person.Email);
        }
        [Fact]
        public void Process()
        {
            var dbConfig = new DbConfigFile
            {
                ConnectionString = "Data Source=localhost;Initial Catalog=testdb;User ID=sa;Password=Pass@word"
            };

            var config = @"
tables:
- name: filemaker_rolodex
  actions:
  - name: Anonymize student data
    type: Anon
    values:
      - name: anon-server-url
        value: http://host.docker.internal:3000/api/1.3/
      - name: input-seed
        value: rolodex_id
      - name: anon-ro_first_name
        value: FirstName
      - name: anon-ro_last_name
        value: LastName
      - name: anon-ro_address
        value: Address1
      # - name: anon-ro_address_apt
      #   value: Address1
      - name: input-nat
        value: ro_country
      - name: anon-ro_email
        value: Email
      - name: anon-DOB
        value: DateOfBirth
      - name: anon-ro_fax
        value: Phone
      - name: anon-ro_cell_phone
        value: Phone
      - name: anon-ro_home_phone
        value: Phone
      - name: anon-ro_work_phone
        value: Phone
      - name: anon-ro_cell_phone_clean
        value: Phone
      - name: anon-ro_home_phone_clean
        value: Phone
      - name: anon-ro_work_phone_clean

            ";
            var cleanConfig = Common.ParseYaml<CleanConfigFile>(config).IfLeft(e => throw e);

            Clean.Process(dbConfig, cleanConfig).IfLeft(e => throw e);
        }
    }
}