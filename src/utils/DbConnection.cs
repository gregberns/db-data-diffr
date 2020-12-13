using System;
using System.Data;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using Dapper;
using System.Net.Http;
using static Logging.Logger;
using Microsoft.Extensions.Logging;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;
using System.Text.Json;

namespace DbDataDiffr
{
    public class Connection
    {
        public readonly DbConnectionString ConnectionString;
        public readonly DbAccessToken AccessToken;
        // public readonly DbCredentials Credentials;
        public readonly bool DbIsLocal;

        public Connection(DbConnectionString connectionString, DbAccessToken accessToken)
        {
            ConnectionString = connectionString;
            AccessToken = accessToken;
            DbIsLocal = false;
        }

        public Connection(DbConnectionString connectionString)
        {
            ConnectionString = connectionString;
            DbIsLocal = true;
        }

        // public static EitherAsync<Error, int> ExecuteAsync(Connection conn, string query) =>
        //     use(
        //         new SqlConnection(conn.ConnectionString.ToString()),
        //         conn => TryAsync(() =>
        //         {
        //             conn.Open();
        //             return conn.ExecuteAsync(query);
        //         }).ToEither());

        public static async Task<Either<Error, int>> ExecuteAsync(Connection conn, string query)
        {
            try
            {
                using (var c = new SqlConnection(conn.ConnectionString.ToString()))
                {
                    try
                    {
                        // Log.LogDebug($"ConnStateEx1 {c.State} - {query}");
                        // c.Open();
                        // Log.LogDebug($"ConnStateEx2 {c.State} - {query}");
                        return await c.ExecuteAsync(query);
                    }
                    catch (Exception e)
                    {
                        return Left<Error, int>(Error.New(e));
                    }
                }
            }
            catch (Exception e)
            {
                Log.LogError($"ExecuteAsync. Message: {e.Message}");
                return Left<Error, int>(Error.New(e));
            }
        }

        public static async Task<Either<Error, IEnumerable<T>>> QueryAsync2<T>(Connection conn, string query, object param)
        {
            try
            {
                using (var c = new SqlConnection(conn.ConnectionString.ToString()))
                {
                    try
                    {
                        return Right<Error, IEnumerable<T>>(await c.QueryAsync<T>(query));
                    }
                    catch (Exception e)
                    {
                        return Left<Error, IEnumerable<T>>(Error.New(e));
                    }
                }
            }
            catch (Exception e)
            {
                Log.LogError($"ExecuteAsync. Message: {e.Message}");
                return Left<Error, IEnumerable<T>>(Error.New(e));
            }
        }

        public EitherAsync<Error, IEnumerable<T>> QueryAsync<T>(string query, object param) =>
            use(
                DbIsLocal ? new SqlConnection(ConnectionString.ToString()) :
                    new SqlConnection(ConnectionString.ToString())
                    {
                        AccessToken = AccessToken.ToString()
                    },
                conn => TryAsync(async () =>
                {
                    return await conn.QueryAsync<T>(query, param);
                }).ToEither());

        public Either<Exception, IEnumerable<T>> Query<T>(string query) =>
            use(
                DbIsLocal ? new SqlConnection(ConnectionString.ToString()) :
                    new SqlConnection(ConnectionString.ToString())
                    {
                        AccessToken = AccessToken.ToString()
                    },
                conn => Try(() => conn.Query<T>(query)).ToEither());

        public EitherAsync<Error, int> ExecuteAsync(string query) =>
            use(
                DbIsLocal ? new SqlConnection(ConnectionString.ToString()) :
                    new SqlConnection(ConnectionString.ToString())
                    {
                        AccessToken = AccessToken.ToString()
                    },
                conn => TryAsync(async () =>
                {
                    conn.Open();
                    return await conn.ExecuteAsync(query);
                }).ToEither());

        public Either<Exception, int> Execute(string query) =>
            use(
                DbIsLocal ? new SqlConnection(ConnectionString.ToString()) :
                    new SqlConnection(ConnectionString.ToString())
                    {
                        AccessToken = AccessToken.ToString()
                    },
                conn => Try(() => conn.Execute(query)).ToEither());

        public Either<Exception, T> GetTables<T>(Func<DataTable, T> f) =>
            use(
                DbIsLocal ? new SqlConnection(ConnectionString.ToString()) :
                    new SqlConnection(ConnectionString.ToString())
                    {
                        AccessToken = AccessToken.ToString()
                    },
                conn =>
                {
                    conn.Open();
                    var res = conn.GetSchema("Tables");
                    return f(res);
                });

        public EitherAsync<Error, IEnumerable<DbColumnMetadata>> GetColumns()
        {
            var qry = @"SELECT
                        db_name() 'DatabaseName',
                        tbl.name 'TableName',
                        c.name 'ColumnName',
                        c.column_id 'ColumnId',
                        typ.Name 'ColumnType',
                        ISNULL(i.is_primary_key, 0) 'IsPrimaryKey'
                    FROM
                        sys.columns c
                    INNER JOIN
                        sys.tables tbl ON tbl.object_id = c.object_id
                    INNER JOIN
                        sys.types typ ON c.user_type_id = typ.user_type_id
                    LEFT OUTER JOIN
                        sys.index_columns ic ON ic.object_id = c.object_id AND ic.column_id = c.column_id
                    LEFT OUTER JOIN
                        sys.indexes i ON ic.object_id = i.object_id AND ic.index_id = i.index_id
                    ";
            // var res = conn.GetSchema("Columns");
            return QueryAsync2<DbColumnMetadata>(this, qry, new { }).ToAsync();
        }
    }

    public record DbColumnMetadata
    {
        public string DatabaseName;
        public string TableName;
        public string ColumnName;
        public int ColumnId;
        public string ColumnType;
        public bool IsPrimaryKey;
    }

    public class DbConfiguration
    {
        public static EitherAsync<Error, DbAccessToken> FetchAccessToken()
        {
            bool inAzure =
                ReadEnvVar("IN_AZURE")
                    .Bind<bool>(s => parseBool(s).ToEither(Error.New("IN_AZURE could not be parsed as bool.")))
                    .Match(
                        Left: e => throw new Exception(e.Message),
                        Right: b => b
                    );
            return
                inAzure ? AzureAccessToken.GetDbAccessToken() :
                GetExFromEnvVars().ToAsync()
                    .Bind<DbAccessToken>(credentials => AzureAccessToken.GetDbAccessTokenExternal(credentials))
                    .MapLeft(e => Error.New($"'IN_AZURE' envvar is false, envvar not found: {e.Message}"));
        }

        public static Either<Error, ExternalDbCredentials> GetExFromEnvVars() =>
                from tenantId in ReadEnvVar("DB_TENANT_ID")
                from clientId in ReadEnvVar("DB_CLIENT_ID")
                from clientSecret in ReadEnvVar("DB_CLIENT_SECRET")
                select new ExternalDbCredentials(tenantId, clientId, clientSecret);
        public static Either<Error, string> ReadEnvVar(string name) =>
            Optional(System.Environment.GetEnvironmentVariable(name))
                .Bind(s => String.IsNullOrWhiteSpace(s) ? None : Some(s))
                .ToEither(Error.New($"Environment Variable ('{name}') is empty"));
    }

    public class DbConnectionString
    {
        string Value { get; }
        public DbConnectionString(string value) { Value = value; }

        public static implicit operator string(DbConnectionString c)
           => c.Value;
        public static implicit operator DbConnectionString(string s)
           => new DbConnectionString(s);

        public override string ToString() => Value;
    }
    public class DbAccessToken : NewType<DbAccessToken, string>
    {
        public DbAccessToken(string token) : base(token)
        { }
    }
    // public class DbCredentials : Record<DbCredentials>
    // {
    //     public readonly string Username;
    //     public readonly string Password;
    //     public DbCredentials(string username, string password)
    //     {
    //         Username = username;
    //         Password = password;
    //     }
    // }
    public class ExternalDbCredentials : Record<ExternalDbCredentials>
    {
        public readonly string TenantId;
        public readonly string ClientId;
        public readonly string ClientSecret;
        public ExternalDbCredentials(
            string tenantId,
            string clientId,
            string clientSecret
        )
        {
            TenantId = tenantId;
            ClientId = clientId;
            ClientSecret = clientSecret;
        }
    }
    public class AzureAccessToken
    {
        public static EitherAsync<Error, DbAccessToken> GetDbAccessToken()
        {
            // We probably shouldn't use this library in k8s because it'll do a bunch of retries
            //  but the underlying service will also do the retries for us

            // // https://github.com/Azure/azure-libraries-for-net/blob/master/src/ResourceManagement/ResourceManager/Authentication/MSITokenProvider.cs
            // var prov = new MSITokenProvider(
            //                 resource: "https://database.windows.net/",
            //                 new MSILoginInformation(MSIResourceType.VirtualMachine));
            // return TryAsync(() => prov.GetAuthenticationHeaderAsync(new CancellationTokenSource().Token))
            //     .ToEither()
            //     .Map(token => new DbAccessToken(token.Parameter));
            var success = false;
            var token = "";
            var ex = new Exception();
            var iter = 0;


            while (success == false && iter < 100)
            {
                try
                {
                    Log.LogInformation("Start GetDbAccessToken");
                    // Get Access Token
                    var http = new HttpClient();
                    // This apparently is a fixed IP in Azure - should not change
                    var url = "http://169.254.169.254/metadata/identity/oauth2/token?api-version=2019-03-11&resource=https://database.windows.net/";

                    var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.Add("Metadata", "true");

                    var response = http.SendAsync(request, HttpCompletionOption.ResponseContentRead).Result;
                    var responseContent = response.Content.ReadAsStringAsync().Result;
                    if (response.StatusCode != System.Net.HttpStatusCode.OK)
                    {
                        throw new Exception($"Failed to receive identity access token. HttpStatus: '{response.StatusCode}', HttpBody: '{responseContent}'");
                    }

                    var jsonDictionary = JsonSerializer.Deserialize<Dictionary<string, string>>(responseContent);
                    var _token = jsonDictionary["access_token"];

                    token = _token;
                    success = true;
                    // return RightAsync<Error, DbAccessToken>(new DbAccessToken(token));
                }
                catch (Exception _ex)
                {
                    Log.LogInformation($"Failed to get token once. Error: {_ex}");
                    ex = _ex;
                    iter += 1;
                    // System.Threading.Thread.Sleep(50);
                }
            }

            if (success)
            {
                Log.LogInformation("Token Acquired");
                return RightAsync<Error, DbAccessToken>(new DbAccessToken(token));
            }
            else
            {
                Log.LogError($"FINAL: Failed to get token. Error: {ex}");
                return LeftAsync<Error, DbAccessToken>(Error.New("Failed to get token....", ex));
            }
        }

        public static EitherAsync<Error, DbAccessToken> GetDbAccessTokenExternal(ExternalDbCredentials credentials)
        {
            var scope = "https%3A%2F%2Fdatabase.windows.net%2F.default";
            // var scope = "api://e8104152-b8dd-40fe-831a-4cb0325d95bd/reportingdb.write.ads";

            var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"https://login.microsoftonline.com/{credentials.TenantId}/oauth2/v2.0/token")
            {
                Content = new StringContent(
                    $"grant_type=client_credentials&client_id={credentials.ClientId}&client_secret={credentials.ClientSecret}&scope={scope}",
                    System.Text.Encoding.UTF8,
                    "application/x-www-form-urlencoded")
            };

            Func<HttpResponseMessage, string, Either<Error, DbAccessToken>> f =
                (response, responseContent) =>
                    response.StatusCode == System.Net.HttpStatusCode.OK
                    ? ExtractDbAccessToken(responseContent)
                    : Error.New($"Failed to receive identity access token. HttpStatus: '{response.StatusCode}', HttpBody: '{responseContent}'");

            return from response in TryAsync<HttpResponseMessage>(
                                        () => new HttpClient().SendAsync(request, HttpCompletionOption.ResponseContentRead))
                                        .ToEither()
                   from responseContent in TryAsync<string>(() => response.Content.ReadAsStringAsync()).ToEither()
                   from accessToken in f(response, responseContent).ToAsync()
                   select accessToken;

        }
        public static Either<Error, DbAccessToken> ExtractDbAccessToken(string json) =>
            Try(() => ((JsonElement)JsonDocument.Parse(json).RootElement).GetProperty("access_token").GetString())
                .ToEither()
                .Map(token => new DbAccessToken(token))
                .MapLeft(ex => Error.New("Failed to parse JSON DbAccessToken response.", ex));

    }
}