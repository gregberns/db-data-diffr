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

namespace DbDataDiffr
{
    public class Snapshot
    {
        public static int CreateSnapshot(string workspace, string snapshotName)
        {
            Directory.SetCurrentDirectory(workspace);

            var e =
                from dbconfig in Common.ReadDbConfig().MapLeft(e => new List<Exception>() { e })
                from dbschema in Common.ReadDbSchema().MapLeft(e => new List<Exception>() { e })
                from u in ProcessTables(snapshotName, dbconfig, dbschema)
                select u;

            return e.Match(
                    Left: es =>
                    {
                        var errors = es.Map(e => $"{e.Message}");
                        Console.WriteLine($"Error(s) occurred: {string.Join("\n- ", errors)}");
                        return 1;
                    },
                    Right: u => 0);
        }
        public static Either<List<Exception>, Unit> ProcessTables(string snapshotName, DbConfigFile dbConfig, DatabaseMetadata dbSchema)
        {
            var conn = new Connection(new DbConnectionString(dbConfig.ConnectionString));
            var tables = dbSchema.Tables.AsEnumerable();
            var snapshotFilePath = Path.Combine("snapshots", snapshotName);
            // Could fail
            Directory.CreateDirectory(snapshotFilePath);

            // handle all tables
            return tables
                .Map(table =>
                {
                    var r = SnapshotTable(conn, snapshotFilePath, table);
                    Console.WriteLine($"Snapshot written. Table: {table.Name}");
                    return r;
                })
                .Map(e => e.Match(Right: u => None, Left: e => Some(e)))
                .Sequence()
                .Match(Some: es => Left(es.ToList()), None: () => Right(unit));
        }
        public static Either<Exception, Unit> SnapshotTable(Connection conn, string snapshotFilePath, TableMetadata t)
        {
            var sql = TableToSql(t);
            var filepath = Path.Combine(snapshotFilePath, $"{t.Name}.csv");

            return use(new StreamWriter(filepath), outfile =>
            {
                // write headers
                var columnNames =
                    t.Columns.AsEnumerable()
                        .OrderBy(c => c.Index)
                        .Select(c => c.Name);
                outfile.WriteLine(string.Join(",", columnNames));

                // write body
                return conn.Query<dynamic>(sql)
                    .MapT(x => (IDictionary<string, object>)x)
                    .IterT(dict =>
                    {
                        var line = FormatLine(dict);
                        // Console.WriteLine($"Write Line = keys: {String.Join(", ", dict.Keys.ToList())}, line: ({line})");
                        outfile.WriteLine(line);
                    });
            });
        }
        public static string FormatLine(IDictionary<string, object> o)
        {
            var fields = o.Map(kv => FormatCell(kv.Value));
            return string.Join(",", fields);
        }
        public static string FormatCell(object cell) =>
            // Surround cell with `"` if it contains a `,`
            Optional(cell == null ? "" : cell.ToString())
                .Map(cell =>
                {
                    return cell.Contains(',') || cell.Contains('"')
                        ? string.Concat("\"", cell.Replace("\"", "\"\""), "\"")
                        : cell;
                })
                .Map(cell =>
                {
                    return cell.Contains("\r\n") ? cell.Replace("\r\n", "\n") : cell;
                })
                .Map(cell =>
                {
                    return cell.Contains('\n') ? cell.Replace("\n", "\\n") : cell;
                })
                .Match(s => s, () => "");
        // cell == null ? "" : cell.ToString()
        // : cell.ToString().Contains(',') ? string.Concat("\"", cell.ToString().Replace("\"", "\"\""), "\"")
        // : cell.ToString();

        public static string TableToSql(TableMetadata table)
        {
            var columnNames = table.Columns.OrderBy(c => c.Index).Map(c => c.Name);
            var columns = String.Join(", ", columnNames);
            return $"SELECT {columns} FROM {table.Name}";
        }
    }
}