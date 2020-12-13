using System;
using System.IO;
using System.Data;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;
using static Logging.Logger;
using Microsoft.Extensions.Logging;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using CsvHelper;
using System.Globalization;
// using Csv;

namespace DbDataDiffr
{
    public class Diff
    {
        public static int CreateDiff(string workspace, string diffName, string startSnapshot, string endSnapshot)
        {
            Directory.SetCurrentDirectory(workspace);

            var startSnapshotPath = $"snapshots/{startSnapshot}/";
            var endSnapshotPath = $"snapshots/{endSnapshot}/";

            if (!Directory.Exists(startSnapshotPath))
            {
                Console.WriteLine($"startSnapshot Path ('{startSnapshotPath}') doesnt exist");
                return 1;
            }
            if (!Directory.Exists(endSnapshotPath))
            {
                Console.WriteLine($"endSnapshot Path ('{endSnapshotPath}') doesnt exist");
                return 1;
            }

            var res =
                // from dbconfig in Common.ReadDbConfig().MapLeft(e => new List<Exception>() { e })
                from dbSchema in Common.ReadDbSchema().MapLeft(e => new List<Error>() { Error.New(e) })
                from snaps in GenerateSnapshotList(dbSchema, startSnapshotPath, endSnapshotPath)
                                .MapLeft(es =>
                                {
                                    Console.WriteLine($"After GenerateSnapshotList Errs: {es.Count()}");
                                    return es.ToList();
                                })
                from u in ProcessSnapshots(diffName, snaps)
                select u;

            return res.Match(
                    Left: es =>
                    {
                        // Console.WriteLine($"Err count: {es.Count()}");
                        var errors = es.Map(e => $"{e.Message}");
                        Console.WriteLine($"Error(s) occurred: {string.Join("\n- ", errors)}");
                        return 1;
                    },
                    Right: u => 0);
        }
        public static Either<List<Error>, Unit> ProcessSnapshots(
            string diffName,
            IEnumerable<(string, string, string, string)> snaps)
        {
            var errs = snaps
                .Map((tup) => ($"./diffs/{diffName}/{tup.Item1}", DiffInputStream(tup.Item2, tup.Item3, tup.Item4)))
                .Map(d => d.Item2.Count() > 0 ? WriteDiffBase(d.Item1, SerializeDiffBase(d.Item2)) : Right(unit))
                .Lefts()
                .ToList();

            Console.WriteLine($"ProcessSnapshots Errs: {errs.Count}");

            return errs.Count() == 0
                ? Right<List<Error>, Unit>(unit)
                : Left<List<Error>, Unit>(errs);
        }
        public static Either<List<Error>, IEnumerable<(string, string, string, string)>> GenerateSnapshotList(
            DatabaseMetadata db,
            string snapshotpath1,
            string snapshotpath2) =>
            db.Tables
                .AsEnumerable()
                .Select(t =>
                {
                    var snapshot1 = $"./{snapshotpath1}{t.Name}.csv";
                    var snapshot2 = $"./{snapshotpath2}{t.Name}.csv";

                    var snapExists1 = File.Exists(snapshot1);
                    var snapExists2 = File.Exists(snapshot2);

                    // In case there's no primary key on the table - use the first column
                    var primaryKey = t.PrimaryKey;
                    if (t.PrimaryKey == null)
                    {
                        primaryKey = t.Columns.OrderBy(c => c.Index).First().Name;
                    }

                    if (snapExists1 && snapExists2)
                    {
                        return Right<List<Error>, (string, string, string, string)>((t.Name, primaryKey, snapshot1, snapshot2));
                    }

                    var pathErrors = new List<Error>();
                    if (snapExists1)
                    {
                        pathErrors.Add(Error.New($"Snapshot path missing: {snapshot1}"));
                    }
                    if (snapExists2)
                    {
                        pathErrors.Add(Error.New($"Snapshot path missing: {snapshot2}"));
                    }
                    return Left<List<Error>, (string, string, string, string)>(pathErrors);
                })
                .Sequence();

        public static string SerializeDiffBase(IEnumerable<DiffBase> diffs) =>
            Common.SerializeToYaml(diffs)
                .Match(
                    Right: s => s,
                    Left: e =>
                    {
                        throw new Exception($"SerializeDiffBase failed. Error: {e.Message}");
                    }
                );
        // diffs
        //     .Aggregate(new StringBuilder(), (str, v) =>
        //     {
        //         var k = v.Key;
        //         var t = v.Type;

        //         str.AppendLine($"==========");
        //         str.AppendLine($"Key: {k}");
        //         str.AppendLine($"Type: {t}");

        //         if (t == "Update")
        //         {
        //             var d = ((DiffUpdate)v).Columns;
        //             str.AppendLine($"Values: {SerializeDict(d)}");
        //         }
        //         else if (t == "Insert")
        //         {
        //             var d = ((DiffInsert)v).Columns;
        //             str.AppendLine($"Values: {SerializeDict(d)}");
        //         }
        //         else
        //         {
        //             var d = ((DiffDelete)v).Columns;
        //             str.AppendLine($"Values: {SerializeDict(d)}");
        //         }
        //         return str;
        //     })
        //     .ToString();

        public static Either<Error, Unit> WriteDiffBase(string diffPath, string diffs) =>
             Try(() =>
            {
                Directory.CreateDirectory(Path.GetDirectoryName(diffPath));
                if (!string.IsNullOrWhiteSpace(diffs))
                    File.WriteAllText(diffPath, diffs);
                return unit;
            })
            .ToEither()
            .MapLeft(e => Error.New($"Failed to write diff to: {diffPath}", e));

        public static IEnumerable<DiffBase> DiffInputStream(string primaryKey, string filepath1, string filepath2)
        {
            Console.WriteLine($"DiffInputStream: {primaryKey}, {filepath1}, {filepath2}");
            using (var file1 = new StreamReader(filepath1))
            {
                using (var csv1 = new CsvHelper.CsvReader(file1, CultureInfo.InvariantCulture))
                {
                    using (var file2 = new StreamReader(filepath2))
                    {
                        using (var csv2 = new CsvHelper.CsvReader(file2, CultureInfo.InvariantCulture))
                        {
                            // return MergeStreams(primaryKey, csv1.GetRecords<dynamic>(), csv2.GetRecords<dynamic>());

                            var ea = csv1.GetRecords<dynamic>()
                                .Map(d => d as IDictionary<string, object>)
                                .Map(d => (d[primaryKey] as string, d))
                                .OrderBy(t => t.Item1)
                                .GetEnumerator();
                            var eb = csv2.GetRecords<dynamic>()
                                .Map(d => d as IDictionary<string, object>)
                                .Map(d => (d[primaryKey] as string, d))
                                .OrderBy(t => t.Item1)
                                .GetEnumerator();

                            var aNext = true;
                            var bNext = true;

                            aNext = ea.MoveNext();
                            bNext = eb.MoveNext();

                            var (a1, a2) = ea.Current;
                            var (b1, b2) = eb.Current;

                            string a = a1;
                            IDictionary<string, object> aa = a2;
                            string b = b1;
                            IDictionary<string, object> bb = b2;

                            int ii = 0;

                            //while (ii < 10)
                            while (true)
                            {
                                // Console.WriteLine($"aNext {aNext} {ea.Current} && bNext {bNext} {ea.Current}");
                                if (!aNext && !bNext)
                                {
                                    yield break;
                                }

                                ii++;
                                // Console.WriteLine($"======= Iteration: {ii}");
                                // Console.WriteLine($"A: {SerializeDict(aa)}");
                                // Console.WriteLine($"B: {SerializeDict(bb)}");

                                //      A   B
                                //      1   1
                                //      2   2
                                //      3      If A < B  - DEL
                                //      4   4
                                //          5  If A > B - ADD
                                //      6   6
                                int comp = 0;
                                if (!aNext)
                                {
                                    comp = 1;
                                }
                                else if (!bNext)
                                {
                                    comp = -1;
                                }
                                else
                                {
                                    comp = a.CompareTo(b);
                                }

                                // Console.WriteLine($"a: {a}, b: {b}, comp: {comp}");
                                if (comp == 0)
                                {
                                    // Console.WriteLine($"A/B Diff: {SerializeDict(DiffDicts(aa, bb))}");
                                    var diff = DiffDicts(aa, bb);
                                    if (diff.Count() > 0)
                                    {
                                        yield return new DiffUpdate
                                        {
                                            Key = a,
                                            Type = "Update",
                                            Columns = diff
                                        };
                                    }

                                    aNext = ea.MoveNext();
                                    var (a11, a22) = ea.Current;
                                    a = a11;
                                    aa = a22;
                                    bNext = eb.MoveNext();
                                    var (b11, b22) = eb.Current;
                                    b = b11;
                                    bb = b22;
                                }

                                // If A < B  - DEL
                                if (comp < 0)
                                {
                                    yield return new DiffDelete
                                    {
                                        Key = a,
                                        Type = "Delete",
                                        Columns = aa
                                    };
                                    aNext = ea.MoveNext();
                                    var (a11, a22) = ea.Current;
                                    a = a11;
                                    aa = a22;
                                    // Console.WriteLine($"0 > comp, aNext: {aNext}, bNext: {bNext}, a: {a}, b: {b}");
                                }

                                // If A > B - ADD
                                if (comp > 0)
                                {
                                    yield return new DiffInsert
                                    {
                                        Key = b,
                                        Type = "Insert",
                                        Columns = bb
                                    };
                                    bNext = eb.MoveNext();
                                    var (b11, b22) = eb.Current;
                                    b = b11;
                                    bb = b22;
                                    // Console.WriteLine($"0 < comp, aNext: {aNext}, bNext: {bNext}, a: {a}, b: {b}");
                                }
                            }

                        }
                    }
                }
            }
        }

        static string serializeThing((string, IDictionary<string, object> d) t)
        {
            return $"({t.Item1}, {{{t.Item2.Keys.First()}, {t.Item2.Values.First()}}}";
        }

        public static string SerializeDict(IDictionary<string, object> d)
        {
            var kvs = d.Map(kv => $"{{'{kv.Key}': '{kv.Value}'}}").ToList();
            var kvss = string.Join($", ", kvs);
            return $"{{ {kvss} }}";
        }
        public static string SerializeDict(IDictionary<string, (object, object)> d)
        {
            var kvs = d.Map(kv => $"{{\"{kv.Key}\": (\"{kv.Value.Item1}\", \"{kv.Value.Item2}\")}}").ToList();
            var kvss = string.Join($", ", kvs);
            return $"{{ {kvss} }}";
        }
        public static IDictionary<string, (object, object)> DiffDicts(IDictionary<string, object> a, IDictionary<string, object> b) =>
            a.Filter(kv => !EqualityComparer<object>.Default.Equals(kv.Value, b[kv.Key]))
                // a.Filter(kv =>
                // {
                //     // var t = kv.Value.ToString() != b[kv.Key].ToString();
                //     var t = !EqualityComparer<object>.Default.Equals(kv.Value, b[kv.Key]);
                //     Console.WriteLine($"t={t}, kv1: {kv.Value}, kv2: {b[kv.Key]}");
                //     return t;
                // })
                .Map(kv => new KeyValuePair<string, (object, object)>(kv.Key, (kv.Value, b[kv.Key])))
                .ToDictionary(kv => kv.Key, kv => kv.Value);


        // Example of the problem using IDisposable and Enumerators together
        // TwoStreams2(primaryKey, "snapshots/01/Persons.csv", "snapshots/02/Persons.csv")
        //     .ToList()
        //     .ForEach(i => Console.WriteLine($"item: {i}"));

        public static IEnumerable<string> TwoStreams2(
                    string primaryKey, string filepath1, string filepath2)
        {
            var fs = new FileStream(filepath1, FileMode.Open, FileAccess.Read);
            var sr = new StreamReader(fs, Encoding.UTF8);
            try
            {
                // Console.WriteLine($"=====start===");
                var en = IOUtil.EnumLines(sr).GetEnumerator();

                // var e = en; //.GetEnumerator();
                // Console.WriteLine($"====e=====");
                // e.MoveNext(); // Faults here
                // Console.WriteLine($"====r=====");
                // yield return e.Current;

                return Iterate(en);
            }
            finally
            {
                Console.WriteLine($"====FINALLY");
                sr.Close();
                fs.Close();
            }
        }

        public static IEnumerable<string> Iterate(IEnumerator<dynamic> csv)
        {
            // foreach (var i in csv)
            // {
            //     Console.WriteLine($"===={i}");
            //     yield return i.ToString();
            // }

            var e = csv; //.GetEnumerator();
            Console.WriteLine($"====e=====");
            e.MoveNext(); // Faults here
            Console.WriteLine($"====r=====");
            yield return e.Current;
        }
    }

    public record DiffBase
    {
        public string Key;
        public string Type;
    }

    public record DiffUpdate : DiffBase
    {
        public IDictionary<string, (object, object)> Columns;
    }

    public record DiffInsert : DiffBase
    {
        public IDictionary<string, object> Columns;
    }
    public record DiffDelete : DiffBase
    {
        public IDictionary<string, object> Columns;
    }


    // new DiffColumn { Name = "", Value = ""};
    // public record DiffColumn
    // {
    //     public string Name;
    //     public string Value;
    // }

    public static class IOUtil
    {
        public static IEnumerable<string> EnumLines(System.IO.StreamReader fp)
        {
            while (!fp.EndOfStream)
            {
                var line = fp.ReadLine();
                yield return line;
            }
        }

    }

}