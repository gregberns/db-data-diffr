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
using System.Net.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DbDataDiffr
{
    public class Common
    {
        public static Either<Error, DbConfigFile> ReadDbConfig()
        {
            const string filename = "dbconfig.yml";
            return ParseYamlFile<DbConfigFile>(filename);
        }
        public static Either<Exception, Unit> WriteDbSchema(string schemaYaml)
        {
            const string configFilename = "schema.yml";
            return Try<Unit>(() =>
                {
                    System.IO.File.WriteAllText(configFilename, schemaYaml);
                    return unit;
                })
                .ToEither();
        }
        public static Either<Error, DatabaseMetadata> ReadDbSchema()
        {
            const string filename = "schema.yml";
            return ParseYamlFile<DatabaseMetadata>(filename);
        }
        public static Either<Error, T> ParseYamlFile<T>(string path) =>
            Try(() => System.IO.File.ReadAllText(path))
                .ToEither()
                .MapLeft(ex => Error.New($"Reading '{path}' file failed. Error: {ex.Message}", ex))
                .Bind(str => ParseYaml<T>(str)
                            .MapLeft(ex => Error.New($"Deserializing '{path}' file failed. Error: {ex.Message}", ex)));

        public static Either<Exception, T> ParseYaml<T>(string yaml) =>
            Try(() => new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .Build()
                    .Deserialize<T>(yaml))
                .ToEither();
        public static Either<Exception, string> SerializeToYaml<T>(T obj) =>
            Try(() => new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .DisableAliases()
                .Build()
                .Serialize(obj)
            ).ToEither();

        public static EitherAsync<Error, T> HttpGet<T>(HttpClient client, string requestUri) =>
            TryAsync(() => client.GetAsync(requestUri))
                .ToEither()
                .Bind<Stream>(response =>
                    response.IsSuccessStatusCode
                        ? TryAsync(() => response.Content.ReadAsStreamAsync()).ToEither()
                        : LeftAsync<Error, Stream>(Error.New($"Http status: {response.StatusCode}. Id: {s}"))
                )
                .Bind<T>(s => TryAsync(() => DeserializeFromStream<T>(s).AsTask()).ToEither());

        public static T DeserializeFromStream<T>(Stream stream)
        {
            // StreamReader reader = new StreamReader(stream);
            // string text = reader.ReadToEnd();
            // Console.WriteLine($"SDFGHJKJHGFDS: {text}");
            // stream.Position = 0;

            var serializer = new JsonSerializer();

            using (var sr = new StreamReader(stream))
            using (var jsonTextReader = new JsonTextReader(sr))
            {
                return serializer.Deserialize<T>(jsonTextReader);
            }
        }

    }

    public record DatabaseMetadata
    {
        public string Name;
        public List<TableMetadata> Tables;
    }

    public record TableMetadata
    {
        public string Name;
        public string PrimaryKey;
        public List<ColumnMetadata> Columns;
    }

    public record ColumnMetadata
    {
        public string Name;
        public int Index;
        public string Type;
    }

    public class TableName : NewType<TableName, string>
    {
        public TableName(string name) : base(name)
        { }
    }
    public class ColumnName : NewType<ColumnName, string>
    {
        public ColumnName(string name) : base(name)
        { }
    }

    public record DbConfigFile
    {
        public string ConnectionString;
    }
}