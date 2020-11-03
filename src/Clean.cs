using System;
using System.IO;
using System.Data;
using System.Linq;
using System.Threading;
using System.Net.Http;
using System.Collections.Generic;
using System.Threading.Tasks;
using static Logging.Logger;
using Microsoft.Extensions.Logging;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace DbDataDiffr
{
    public class Clean
    {
        public static int RunClean(string workspace)
        {
            Directory.SetCurrentDirectory(workspace);

            const string configFilename = "clean.yml";

            var res =
                from cleanConfig in Common.ParseYamlFile<CleanConfigFile>(configFilename)
                                    .Do(c =>
                                    {
                                        c.Tables.ForEach(t =>
                                        {
                                            t.Actions.ForEach(a =>
                                            {
                                                Log.LogDebug($"Table {t.Name}, Action = Name: {a.Name}, Type: {a.Type}, Values: {SerializeDbActionValues(a.Values)}");
                                            });
                                        });
                                    })
                from dbConfig in Common.ReadDbConfig()
                                    .Do(config =>
                                    {
                                        Log.LogInformation($"dbconfig = {config.ConnectionString}");
                                    })
                from i in Process(dbConfig, cleanConfig)
                select i;

            res.IfLeft(e => Log.LogError($"Faulted: {e.Message} \n{e.Exception}"));
            return res.Match(Right: _ => 0, _ => 1);
        }
        public static string SerializeDbActionValues(List<DbActionValue> values) =>
            values != null ? string.Join(", ", values.Select(i => $"{{ {SerializeDbActionValue(i)} }}")) : "<empty>";
        public static string SerializeDbActionValue(DbActionValue value) =>
            $"{value.Name}: \"{value.Value}\"";

        public static Either<Error, Unit> Process(DbConfigFile dbConfig, CleanConfigFile cleanConfig)
        {
            var conn = new Connection(new DbConnectionString(dbConfig.ConnectionString));

            Log.LogDebug($"Tables: {string.Join(", ", cleanConfig.Tables.Map(t => t.Name))}");

            var r = cleanConfig.Tables
                .Map(t =>
                {
                    IEnumerable<Task<Either<Error, Unit>>> a = t.Actions
                        .Map(action => ProcessAction(conn, t.Name, action).ToEither());

                    return Task.WhenAll(a)
                        .Map(e => e.AsEnumerable().Lefts())
                        .Do(es =>
                        {
                            es.ToList().ForEach(e =>
                            {
                                Log.LogError($"Table: {t.Name}, Message: {e.Message}");
                            });
                            Log.LogDebug($"ProcessAction Complete. Table: {t.Name}");
                        });
                });

            return Task.WhenAll(r)
                .Map(e => e.AsEnumerable())
                .BindT(e => e)
                .Map(e =>
                {
                    WriteErrors(e);
                    return e.Count() == 0
                        ? Right<Error, Unit>(unit)
                        : Left<Error, Unit>(Error.New($"Errors occurred (Count: {e.Count()})"));
                })
                .Do(_ =>
                {
                    Log.LogDebug("Process Almost Complete.");
                    System.Threading.Thread.Sleep(500);
                    Log.LogDebug("Process Complete.");
                })
                .GetAwaiter().GetResult();
        }

        public static void WriteErrors(IEnumerable<Error> es)
        {
            es.Map(e =>
            {
                var verboseLogging = true;
                var stack = verboseLogging
                    ? e.Exception.Map(ex =>
                        verboseLogging
                            ? $", StackTrace: {ex.StackTrace}"
                            : "")
                        .Match(Some: s => s, None: () => "")
                    : "";
                return $"Error = Code: {e.Code}, Message: {e.Message}{stack}";
            })
            .ToList()
            .ForEach(e => Log.LogError(e));
        }

        public static EitherAsync<Error, Unit> ProcessAction(Connection dbConnection, string tableName, DbAction action)
        {
            switch (action.Type.ToLower())
            {
                case "truncate":
                    return TruncateAction(dbConnection, tableName);
                case "cull":
                    var cullValues = action.Values.AsEnumerable().ToDictionary(a => a.Name, a => a.Value);
                    return CullAction(dbConnection, tableName, cullValues);
                case "anon":
                    Log.LogDebug($"Anon START");
                    var anonValues = action.Values.AsEnumerable().ToDictionary(a => a.Name, a => a.Value);
                    return AnonomizeAction(dbConnection, tableName, anonValues);
                default:
                    return LeftAsync<Error, Unit>(new Exception($"Unknown action type: '{action.Type}'."));
            }
        }

        public static EitherAsync<Error, Unit> TruncateAction(Connection dbConnection, string tableName)
        {
            Log.LogDebug($"Truncate Table: {tableName}");

            var sql = $"TRUNCATE TABLE {tableName}";

            return Connection.ExecuteAsync(dbConnection, sql).ToAsync()
                .Bind<Unit>(i =>
                    i == -1 ? RightAsync<Error, Unit>(unit)
                    : LeftAsync<Error, Unit>(Error.New($"A normal '{sql}' should return a -1, but '{i}' was returned."))
                );
        }
        public static EitherAsync<Error, Unit> CullAction(Connection dbConnection, string tableName, Dictionary<string, string> values)
        {
            Log.LogDebug($"Cull Table: {tableName}, Clause: \"{values["clause"]}\"");

            var sql = $"DELETE FROM {tableName} WHERE {values["clause"]}";

            return Connection.ExecuteAsync(dbConnection, sql).ToAsync()
                .Bind<Unit>(i =>
                {
                    Log.LogDebug($"Cull: SQL: '{sql}'. Result: {i}");
                    return RightAsync<Error, Unit>(unit);
                });
        }

        public static EitherAsync<Error, Unit> AnonomizeAction(Connection dbConnection, string tableName, Dictionary<string, string> values)
        {
            var serviceEndpoint = values["anon-server-url"];

            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Connection", "Keep-Alive");
            client.BaseAddress = new Uri(serviceEndpoint);
            client.Timeout.Add(new TimeSpan(0, 10, 0));
            var threadCount = 2;
            var _throttler = new SemaphoreSlim(threadCount, threadCount);

            /// Identity Properties
            /// Look for keys starting with `input-`, used to query table for data to use to anonymize data, such as nationality, gender, etc.
            ///  - Seed (required): Usually the primary key of the table. Used to generate a unique user. If seed is stable, identity output will be stable.
            ///  - Nationality: identity properties will reflect the nationality supplied. Supported values: us
            ///  - Gender: identity created will have male or female names. Supported Values: male, female

            var seedColumn = values["input-seed"];
            var projectClause = string.Join(", ", values.Keys
                .Where(k => k.ToLower().StartsWith("input-"))
                .Select(k =>
                {
                    //Like seed, gender, etc
                    var propertyName = k.Substring("input-".Length());
                    var columnName = values[k];
                    return $"{columnName} as {propertyName}";
                }));
            var initSql = $"SELECT {projectClause} FROM {tableName}";

            /// Identity Columns
            ///  Look for keys that start with 'anon-',
            ///  Those key will be the db column name (since its unique)
            ///   The Value will be the Anon Key name

            List<AnonColumn> colsToAnon = values.Keys
                .Where(k => k.ToLower().StartsWith("anon-"))
                .Where(k => k != "anon-server-url")
                .Select(k =>
                {
                    var columnName = k.Substring("anon-".Length());
                    var anonValue = values[k];
                    return new AnonColumn()
                    {
                        ColumnName = columnName,
                        AnonValue = anonValue
                    };
                })
                .ToList();

            Log.LogDebug($"Anon - Initial SQL: '{initSql}', SeedColumn: {seedColumn}");

            return Connection.QueryAsync2<dynamic>(dbConnection, initSql, new { }).ToAsync()
                .MapLeft(e =>
                {
                    Log.LogError($"QueryAsync Failed. Sql: {initSql}, Message: {e.Message}");
                    return e;
                })
                .Bind(rows =>
                {
                    Log.LogDebug($"Anon - Initial SQL returned. Count {rows.Count()}");
                    var people = rows.Map(async row =>
                    {
                        var returnedColumns = row as IDictionary<string, object>;
                        var seed = returnedColumns["seed"];

                        Log.LogDebug($"COLUMNS RETURNED: {string.Join(", ", returnedColumns.Keys)}");

                        var requestUri = BuildAnonUrl(returnedColumns);

                        Log.LogDebug($"Anon - Initial SQL result: Seed: {seed}, ANON_URL: {requestUri}");

                        await _throttler.WaitAsync().ConfigureAwait(false);

                        return await Common.HttpGet<AnonResults>(client, requestUri)
                            .MapLeft(e =>
                            {
                                _throttler.Release();
                                Log.LogError($"HttpRequest Failed. Seed: {seed}, Message: {e.Message}");
                                return e;
                            })
                            .Map(res =>
                            {
                                _throttler.Release();
                                Console.WriteLine($"PHONE_PHONE_PHONE: {res.results[0].phone}");
                                var a = AnonResultsToPerson(res);
                                return a;
                            })
                            .Bind(person =>
                            {
                                var personDict = person.ToDictionary();
                                var setClause = string.Join(", ", colsToAnon
                                    .Map(c => $"{c.ColumnName} = '{personDict[c.AnonValue]}'"));

                                var sql = $"UPDATE {tableName} SET {setClause} WHERE {seedColumn} = {seed}";

                                Log.LogDebug($"Anon - Start UPDATE. Seed: {person.Seed}, Sql: '{sql}'");

                                return Connection.ExecuteAsync(dbConnection, sql).ToAsync()
                                    .MapLeft(e =>
                                    {
                                        Log.LogError($"ExecuteAsync Failed. Seed: {seed}, Message: {e.Message}");
                                        return e;
                                    })
                                    .Bind<Unit>(i =>
                                    {
                                        Log.LogDebug($"Anon - UpdateAnon Complete. Result: {i}, Sql: '{sql}'");
                                        return RightAsync<Error, Unit>(unit);
                                    });
                            })
                            .ToEither();
                    });

                    return Task.WhenAll(people)
                        .Map(e => e.AsEnumerable().Lefts())
                        .Map(errs =>
                        {
                            WriteErrors(errs);
                            return errs.Count() == 0
                                ? Right<Error, Unit>(unit)
                                : Left<Error, Unit>(Error.New($"Errors occurred (Count: {errs.Count()})"));
                        }).ToAsync();
                });
        }
        // public static EitherAsync<Error, AnonResults> GetAnon(HttpClient client, IDictionary<string, object> r)
        // {
        //     var s = r["seed"];
        //     var n = r["nat"] == "United States" ? "us" : "";
        //     var g = r["gender"];

        //     var requestUri = $"?seed={s}&nat={n}&gender={g}";

        //     return Common.HttpGet<AnonResults>(client, requestUri);
        // }
        public static AnonPerson AnonResultsToPerson(AnonResults r) =>
            new AnonPerson
            {
                FirstName = r.results[0].name.first,
                LastName = r.results[0].name.last,
                Address1 = $"{r.results[0].location.street.number} {r.results[0].location.street.name}",
                Email = r.results[0].email,
                Phone = r.results[0].phone,
                Cell = r.results[0].cell,
                DateOfBirth = StripTimeFromDate(r.results[0].dob.date),
                Age = r.results[0].dob.age,
                Seed = r.info.seed
            };
        public static string StripTimeFromDate(string date) =>
            DateTime.Parse(date).Date.ToString("s");

        public static string BuildAnonUrl(IDictionary<string, object> returnedColumns)
        {
            // var returnedColumns = r as IDictionary<string, object>;
            var colVals = (new List<string>() {
                    "seed",
                    "nat",
                    "gender"
                })
            .Filter(col => returnedColumns.Keys.Exists(k => k == col))
            .Select(col =>
            {
                // Make sure to convert to string to compare - Some primary keys will be Decimal
                var value = returnedColumns[col].ToString();
                if (col == "nat")
                {
                    // Going to hard code this to US for now because the High ASCII values are causing a DB collation issue
                    // value = value == "United States" ? "us" : "";
                    value = "us";
                }
                if (col == "gender")
                {
                    value = value.ToLower();
                }
                return $"{col}={value}";
            });

            var requestUri = "?" + string.Join("&", colVals);

            return requestUri;
        }

    }

    public record AnonColumn
    {
        public string ColumnName;
        public string AnonValue;
    }

    public record AnonPerson
    {
        public string FirstName;
        public string LastName;
        public string Address1;
        public string Email;
        public string Phone;
        public string Cell;
        public string DateOfBirth;
        public string Age;
        public string Seed;

        public override string ToString() =>
            $"FirstName: {FirstName}, LastName: {LastName}, Address1: {Address1}, Email: {Email}, Cell: {Cell}, DateOfBirth: {DateOfBirth}, Age: {Age}, Seed: {Seed}";

        public Dictionary<string, string> ToDictionary() =>
            new Dictionary<string, string> {
                {"FirstName",FirstName},
                {"LastName",LastName},
                {"Address1",Address1},
                {"Email",Email},
                {"Phone",Phone},
                {"Cell",Cell},
                {"DateOfBirth", DateOfBirth},
                {"Age",Age},
                {"Seed",Seed}
            };
    }

    public record CleanConfigFile
    {
        public List<TableCleanConfig> Tables;
    }

    public record TableCleanConfig
    {
        public string Name;
        public List<DbAction> Actions;
    }
    public record DbAction
    {
        public string Name;
        public string Type;
        public List<DbActionValue> Values;
    }
    public record DbActionValue
    {
        public string Name;
        public string Value;
    }


    public record AnonResults
    {
        public List<AnonResult> results;
        public AnonInfo info;
    }

    public record AnonResult
    {
        public string gender;
        public AnonName name;
        public AnonLocation location;
        public AnonDob dob;
        public string email;
        public string phone;
        public string cell;
        public string nat;
    }
    public record AnonName
    {
        public string title;
        public string first;
        public string last;
    }
    public record AnonLocation
    {
        public AnonStreet street;
        public string city;
        public string state;
        public string country;
        public string postcode;
    }
    public record AnonStreet
    {
        public string number;
        public string name;
    }
    public record AnonDob
    {
        public string date;
        public string age;
    }
    public record AnonInfo
    {
        public string seed;
        public int results;
        public int page;
        public string version;
    }
}