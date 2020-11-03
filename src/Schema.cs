using System;
using System.IO;
using System.Data;
using System.Linq;
using System.Collections.Generic;
using static Logging.Logger;
using Microsoft.Extensions.Logging;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DbDataDiffr
{
    public class Schema
    {
        public static int FetchSchema(string workspace)
        {
            Directory.SetCurrentDirectory(workspace);

            var res = Common.ReadDbConfig()
                .ToAsync()
                // .Do(config =>
                // {
                //     Log.LogInformation($"dbconfig = {config.ConnectionString}");
                // })
                // Read the schema from the DB
                .Bind(config =>
                {
                    var conn = new Connection(new DbConnectionString(config.ConnectionString));
                    return FetchDatabaseMetadata(conn)
                        .MapLeft(ex => Error.New($"Error when fetching database metadata. Error: {ex.Message}", ex));
                })
                // .Do(db =>
                // {
                // db.Tables.Take(4)
                //     .ToList()
                //     .ForEach(table =>
                //     {
                //         Common.SerializeToYaml(table.Columns)
                //             .Do(s =>
                //             {
                //                 Log.LogInformation($"Columns: {s}");
                //             });
                //     });
                //     Log.LogInformation($"db = name: {db.Name}");
                //     db.Tables.ToList().ForEach(table =>
                //     {
                //         Log.LogInformation($"table = name: {table.Name}, cols: ({String.Join(", ", table.Columns)})");
                //     });
                // })
                // format schema and print
                .Bind(tm =>
                {
                    Console.WriteLine($"Tables Found: {tm.Tables.Count}");
                    return Common.SerializeToYaml(tm).ToAsync()
                                .MapLeft(ex => Error.New($"Error serializing database metadata. Error: {ex.Message}", ex));
                })
                .Bind(str =>
                {
                    // Log.LogInformation($"Yaml Schema: \n{str}");
                    return Common.WriteDbSchema(str).ToAsync()
                        .MapLeft(ex => Error.New($"Error writing database metadata to file. Error: {ex.Message}", ex));
                })
                .ToEither()
                .Result;

            return res.Match(
                    Left: e =>
                    {
                        var es = new List<Error>() { e };
                        var errors = es.Map(e => $"{e.Message}");
                        Console.WriteLine($"Error(s) occurred: {string.Join("\n- ", errors)}");
                        return 1;
                    },
                    Right: u => 0);
        }

        public static EitherAsync<Error, DatabaseMetadata> FetchDatabaseMetadata(Connection connection)
        {
            // return connection
            //     .GetColumns(dt =>
            //     {
            //         var name = (string)dt.AsEnumerable().First()[0];
            //         var tables = dt.AsEnumerable()
            //             .GroupBy(dr => (string)dr[2]) // TableName
            //             .Aggregate(new Lst<TableMetadata>(), (agg, dr) =>
            //                 agg.Add(new TableMetadata
            //                 {
            //                     Name = dr.Key,
            //                     // If CBool(Row("IsKey")) = True Then
            //                     //     Keys.Add(Column)
            //                     // End If
            //                     // PrimaryKey = dr.Filter(r => r.),
            //                     PrimaryKey = ProcessPrimaryKey(dr),
            //                     Columns = dr.Map(r =>
            //                     {
            //                         // Console.Write($"Col = name: {r[4]}, 5: {r[5]}, 6: {r[6]}, 7: {r[7]} len: {r.ItemArray.Length}");
            //                         return new ColumnMetadata()
            //                         {
            //                             Name = (string)r[3],
            //                             Type = (string)r[7],
            //                             Index = (int)r[4]
            //                         };
            //                     })
            //                     .ToList()
            //                     // .Freeze()
            //                 }));
            //         return new DatabaseMetadata()
            //         {
            //             Name = name,
            //             Tables = tables.ToList()
            //         };
            //     });
            return connection
                .GetColumns()
                .Bind<DatabaseMetadata>(columns =>
                {
                    if (columns.Count() == 0)
                    {
                        return LeftAsync<Error, DatabaseMetadata>(Error.New("Database metadata query returned no columns. Cannot generate schema."));
                    }
                    var name = (string)columns.First().DatabaseName;
                    var tables = columns
                        .GroupBy(col => col.TableName)
                        .Aggregate(new Lst<TableMetadata>(), (agg, tbl) =>
                            agg.Add(new TableMetadata
                            {
                                Name = tbl.Key,
                                PrimaryKey = ProcessPrimaryKey(tbl),
                                Columns = tbl.Map(col =>
                                {
                                    return new ColumnMetadata()
                                    {
                                        Name = col.ColumnName,
                                        Type = col.ColumnType,
                                        Index = col.ColumnId
                                    };
                                })
                                .ToList()
                            }));
                    return RightAsync<Error, DatabaseMetadata>(new DatabaseMetadata()
                    {
                        Name = name,
                        Tables = tables.ToList()
                    });
                });
        }
        public static string ProcessPrimaryKey(IGrouping<string, DbColumnMetadata> rows)
        {
            return rows
                .Filter(r => r.IsPrimaryKey)
                .Map(r => r.ColumnName)
                .FirstOrDefault();
        }
        public static void ShowDataTable(DataTable table, Int32 length)
        {
            foreach (DataColumn col in table.Columns)
            {
                Console.Write("{0,-" + length + "}", col.ColumnName);
            }
            Console.WriteLine();

            foreach (DataRow row in table.Rows)
            {
                foreach (DataColumn col in table.Columns)
                {
                    if (col.DataType.Equals(typeof(DateTime)))
                        Console.Write("{0,-" + length + ":d}", row[col]);
                    else if (col.DataType.Equals(typeof(Decimal)))
                        Console.Write("{0,-" + length + ":C}", row[col]);
                    else
                        Console.Write("{0,-" + length + "}", row[col]);
                }
                Console.WriteLine();
            }
        }

        public static void ShowDataTable(DataTable table)
        {
            ShowDataTable(table, 14);
        }

        private static void ShowColumns(DataTable columnsTable)
        {
            var selectedRows = from info in columnsTable.AsEnumerable()
                               select new
                               {
                                   TableCatalog = info["TABLE_CATALOG"],
                                   TableSchema = info["TABLE_SCHEMA"],
                                   TableName = info["TABLE_NAME"],
                                   ColumnName = info["COLUMN_NAME"],
                                   DataType = info["DATA_TYPE"]
                               };

            Console.WriteLine("{0,-15}{1,-15}{2,-15}{3,-15}{4,-15}", "TableCatalog", "TABLE_SCHEMA",
                "TABLE_NAME", "COLUMN_NAME", "DATA_TYPE");
            foreach (var row in selectedRows)
            {
                Console.WriteLine("{0,-15}{1,-15}{2,-15}{3,-15}{4,-15}", row.TableCatalog,
                    row.TableSchema, row.TableName, row.ColumnName, row.DataType);
            }
        }

        private static void ShowIndexColumns(DataTable indexColumnsTable)
        {
            var selectedRows = from info in indexColumnsTable.AsEnumerable()
                               select new
                               {
                                   TableSchema = info["table_schema"],
                                   TableName = info["table_name"],
                                   ColumnName = info["column_name"],
                                   ConstraintSchema = info["constraint_schema"],
                                   ConstraintName = info["constraint_name"],
                                   KeyType = info["KeyType"]
                               };

            Console.WriteLine("{0,-14}{1,-11}{2,-14}{3,-18}{4,-16}{5,-8}", "table_schema", "table_name", "column_name", "constraint_schema", "constraint_name", "KeyType");
            foreach (var row in selectedRows)
            {
                Console.WriteLine("{0,-14}{1,-11}{2,-14}{3,-18}{4,-16}{5,-8}", row.TableSchema,
                    row.TableName, row.ColumnName, row.ConstraintSchema, row.ConstraintName, row.KeyType);
            }
        }
    }
}
