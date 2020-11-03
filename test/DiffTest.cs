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
    public class DiffTest
    {
        public string GetApplicationRoot()
        {
            var exePath = Path.GetDirectoryName(System.Reflection
                              .Assembly.GetExecutingAssembly().CodeBase);
            var i = exePath.IndexOf("bin");
            var appRoot = exePath.Substring(0, i - 1).Replace("file:", "");
            return appRoot;
        }

        public DiffTest()
        {
            var path = GetApplicationRoot();
            Directory.SetCurrentDirectory(path);
        }

        [Fact]
        public void DiffTest1()
        {
            var path = GetApplicationRoot();
            Console.WriteLine($"Current Working Directory: {path}");
            Directory.SetCurrentDirectory(path);

            var l = Diff.DiffInputStream("id", "./DiffTest1/item1.csv", "./DiffTest1/item2.csv").ToArr();

            var l1 = l[0];
            Assert.Equal("1", l1.Key);
            Assert.Equal("Update", l1.Type);
            Assert.Equal(new Dictionary<string, (object, object)>(){
                        { "name", ("johnathan", "john") },
                        { "age", ("4", "8") }
                    }, ((DiffUpdate)l1).Columns);

            var l2 = l[1];
            Assert.Equal("2", l2.Key);
            Assert.Equal("Update", l2.Type);
            Assert.Equal(new Dictionary<string, (object, object)>(){
                        { "age", ("5", "9") }
                    }, ((DiffUpdate)l2).Columns);

            var l3 = l[2];
            Assert.Equal("3", l3.Key);
            Assert.Equal("Delete", l3.Type);
            Assert.Equal(new Dictionary<string, object>(){
                        { "id", "3" },
                        { "name", "eggs" },
                        { "age", "3" }
                    }, ((DiffDelete)l3).Columns);

            var l4 = l[3];
            Assert.Equal("4", l4.Key);
            Assert.Equal("Update", l4.Type);
            Assert.Equal(new Dictionary<string, (object, object)>(){
                        { "name", ("cucumber", "pickles") }
                    }, ((DiffUpdate)l4).Columns);

            var l5 = l[4];
            Assert.Equal("5", l5.Key);
            Assert.Equal("Insert", l5.Type);
            Assert.Equal(new Dictionary<string, object>(){
                        { "id", "5" },
                        { "name", "donuts" },
                        { "age", "3" }
                    }, ((DiffInsert)l5).Columns);

            var l6 = l[5];
            Assert.Equal("6", l6.Key);
            Assert.Equal("Update", l6.Type);
            Assert.Equal(new Dictionary<string, (object, object)>(){
                        { "age", ("10", "11") }
                    }, ((DiffUpdate)l6).Columns);

        }

        // Handle case where Ids don't match up at end - so either last one got added or deleted
        //      A   B
        //      1   1
        //      2   2
        //      3      If A < B  - DEL
        //      4   4
        //          5  If A > B - ADD
        //          5  If A > B - ADD
        [Fact]
        public void Diff_LastInsert()
        {
            var path = GetApplicationRoot();
            Console.WriteLine($"Current Working Directory: {path}");
            Directory.SetCurrentDirectory(path);

            var l = Diff.DiffInputStream("id", "./DiffTest2/item1.csv", "./DiffTest2/item2.csv").ToArr();

            var l1 = l[0];
            Assert.Equal("1", l1.Key);
            Assert.Equal("Update", l1.Type);
            Assert.Equal(new Dictionary<string, (object, object)>(){
                        { "name", ("johnathan", "john") },
                        { "age", ("4", "8") }
                    }, ((DiffUpdate)l1).Columns);

            var l2 = l[1];
            Assert.Equal("2", l2.Key);
            Assert.Equal("Update", l2.Type);
            Assert.Equal(new Dictionary<string, (object, object)>(){
                        { "age", ("5", "9") }
                    }, ((DiffUpdate)l2).Columns);

            var l3 = l[2];
            Assert.Equal("3", l3.Key);
            Assert.Equal("Delete", l3.Type);
            Assert.Equal(new Dictionary<string, object>(){
                        { "id", "3" },
                        { "name", "eggs" },
                        { "age", "3" }
                    }, ((DiffDelete)l3).Columns);

            var l4 = l[3];
            Assert.Equal("4", l4.Key);
            Assert.Equal("Update", l4.Type);
            Assert.Equal(new Dictionary<string, (object, object)>(){
                        { "name", ("cucumber", "pickles") }
                    }, ((DiffUpdate)l4).Columns);

            var l5 = l[4];
            Assert.Equal("5", l5.Key);
            Assert.Equal("Insert", l5.Type);
            Assert.Equal(new Dictionary<string, object>(){
                        { "id", "5" },
                        { "name", "donuts" },
                        { "age", "3" }
                    }, ((DiffInsert)l5).Columns);

            var l6 = l[5];
            Assert.Equal("6", l6.Key);
            Assert.Equal("Insert", l6.Type);
            Assert.Equal(new Dictionary<string, object>(){
                        { "id", "6" },
                        { "name", "Soda" },
                        { "age", "11" }
                    }, ((DiffInsert)l6).Columns);

            Assert.Equal(6, l.Count());
        }

        // Handle case where Ids don't match up at end - so either last one got added or deleted
        //      A   B
        //      1   1
        //      2   2
        //          3  If A > B  - ADD
        //      4   4
        //      5      If A < B - DEL
        //      6      If A < B - DEL
        [Fact]
        public void Diff_LastDelete()
        {
            var path = GetApplicationRoot();
            Console.WriteLine($"Current Working Directory: {path}");
            Directory.SetCurrentDirectory(path);

            var l = Diff.DiffInputStream("id", "./DiffTest3/item1.csv", "./DiffTest3/item2.csv").ToArr();

            var l1 = l[0];
            Assert.Equal("1", l1.Key);
            Assert.Equal("Update", l1.Type);
            Assert.Equal(new Dictionary<string, (object, object)>(){
                        { "name", ("johnathan", "john") },
                        { "age", ("4", "8") }
                    }, ((DiffUpdate)l1).Columns);

            var l2 = l[1];
            Assert.Equal("2", l2.Key);
            Assert.Equal("Update", l2.Type);
            Assert.Equal(new Dictionary<string, (object, object)>(){
                        { "age", ("5", "9") }
                    }, ((DiffUpdate)l2).Columns);

            var l3 = l[2];
            Assert.Equal("3", l3.Key);
            Assert.Equal("Insert", l3.Type);
            Assert.Equal(new Dictionary<string, object>(){
                        { "id", "3" },
                        { "name", "eggs" },
                        { "age", "3" }
                    }, ((DiffInsert)l3).Columns);

            var l4 = l[3];
            Assert.Equal("4", l4.Key);
            Assert.Equal("Update", l4.Type);
            Assert.Equal(new Dictionary<string, (object, object)>(){
                        { "name", ("cucumber", "pickles") }
                    }, ((DiffUpdate)l4).Columns);

            var l5 = l[4];
            Assert.Equal("5", l5.Key);
            Assert.Equal("Delete", l5.Type);
            Assert.Equal(new Dictionary<string, object>(){
                        { "id", "5" },
                        { "name", "donuts" },
                        { "age", "3" }
                    }, ((DiffDelete)l5).Columns);

            var l6 = l[5];
            Assert.Equal("6", l6.Key);
            Assert.Equal("Delete", l6.Type);
            Assert.Equal(new Dictionary<string, object>(){
                        { "id", "6" },
                        { "name", "Soda" },
                        { "age", "11" }
                    }, ((DiffDelete)l6).Columns);

            Assert.Equal(6, l.Count());
        }

        [Fact]
        public void Diff_BothEmpty()
        {
            var path = GetApplicationRoot();
            Console.WriteLine($"Current Working Directory: {path}");
            Directory.SetCurrentDirectory(path);

            var l = Diff.DiffInputStream("id", "./DiffTest4/item1.csv", "./DiffTest4/item2.csv").ToArr();

            Assert.Equal(0, l.Count());

            // var l1 = l[0];
            // Assert.Equal("1", l1.Key);
            // Assert.Equal("Update", l1.Type);
            // Assert.Equal(new Dictionary<string, (object, object)>(){
            //             { "name", ("johnathan", "john") },
            //             { "age", ("4", "8") }
            //         }, ((DiffUpdate)l1).Columns);

            // var l2 = l[1];
            // Assert.Equal("2", l2.Key);
            // Assert.Equal("Update", l2.Type);
            // Assert.Equal(new Dictionary<string, (object, object)>(){
            //             { "age", ("5", "9") }
            //         }, ((DiffUpdate)l2).Columns);

            // var l3 = l[2];
            // Assert.Equal("3", l3.Key);
            // Assert.Equal("Insert", l3.Type);
            // Assert.Equal(new Dictionary<string, object>(){
            //             { "id", "3" },
            //             { "name", "eggs" },
            //             { "age", "3" }
            //         }, ((DiffInsert)l3).Columns);

            // var l4 = l[3];
            // Assert.Equal("4", l4.Key);
            // Assert.Equal("Update", l4.Type);
            // Assert.Equal(new Dictionary<string, (object, object)>(){
            //             { "name", ("cucumber", "pickles") }
            //         }, ((DiffUpdate)l4).Columns);

            // var l5 = l[4];
            // Assert.Equal("5", l5.Key);
            // Assert.Equal("Delete", l5.Type);
            // Assert.Equal(new Dictionary<string, object>(){
            //             { "id", "5" },
            //             { "name", "donuts" },
            //             { "age", "3" }
            //         }, ((DiffDelete)l5).Columns);

            // var l6 = l[5];
            // Assert.Equal("6", l6.Key);
            // Assert.Equal("Delete", l6.Type);
            // Assert.Equal(new Dictionary<string, object>(){
            //             { "id", "6" },
            //             { "name", "Soda" },
            //             { "age", "11" }
            //         }, ((DiffDelete)l6).Columns);


        }

        public static T unwrap<T>(Option<T> o) => o.Match(t => t, () => default(T));

        [Fact]
        public void Test2()
        {
            var a = new Dictionary<string, object>(){
                    {"id", "1"},
                    {"name", "johnathan"},
                    {"age", "4"}
                };
            var b = new Dictionary<string, object>(){
                    {"id", "1"},
                    {"name", "john"},
                    {"age", "4"}
                };

            var r = Diff.DiffDicts(a, b);

            Assert.Equal(new Dictionary<string, (object, object)>(){
                    {"name", ("johnathan", "john")}
                }, r);
        }

    }
}
