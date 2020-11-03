using System;
using System.Collections.Generic;
using static Logging.Logger;
using Microsoft.Extensions.Logging;
using CommandLine;

namespace DbDataDiffr
{
    class Program
    {
        static int Main(string[] args)
        {
            InitLogger(Logging.LogLevel.Debug);

            Log.LogInformation("Starting...");

            var a = Parser.Default.ParseArguments<SchemaOptions, SnapshotOptions, DiffOptions, CleanOptions>(args);
            return a.MapResult(
                (SchemaOptions options) =>
                    Schema.FetchSchema(options.Workspace),
                (SnapshotOptions options) =>
                    Snapshot.CreateSnapshot(options.Workspace, options.SnapshotName),
                (DiffOptions options) =>
                    Diff.CreateDiff(
                        options.Workspace,
                        options.DiffName,
                        options.StartSnapshot,
                        options.EndSnapshot),
                (CleanOptions options) =>
                    Clean.RunClean(options.Workspace),
                errors => 1);

            // Log.LogInformation("Done...");
        }

        [Verb("schema", HelpText = "Read the database schema, output a schema.yml file.")]
        class SchemaOptions
        {
            [Option('w', "workspace", Required = false, Default = ".", HelpText = "Workspace directory. Either fully qualified or relative path.")]
            public string Workspace { get; set; }
        }

        [Verb("snapshot", HelpText = "Take snapshot (export data from all tables in the database) store locally for use in diffs.")]
        class SnapshotOptions
        {
            [Option('n', "name", Required = true, HelpText = "Name of snapshot.")]
            public string SnapshotName { get; set; }
            [Option('w', "workspace", Required = false, Default = ".", HelpText = "Workspace directory. Either fully qualified or relative path.")]
            public string Workspace { get; set; }
        }

        [Verb("diff", HelpText = "Display the difference between two snapshots.")]
        class DiffOptions
        {
            [Option('n', "diff-name", Required = true, HelpText = "Name of the diff to create")]
            public string DiffName { get; set; }
            [Option('s', "start-snapshot", Required = true, HelpText = "Starting snapshot to use for the diff")]
            public string StartSnapshot { get; set; }
            [Option('e', "end-snapshot", Required = true, HelpText = "Ending snapshot to use for the diff")]
            public string EndSnapshot { get; set; }

            [Option('w', "workspace", Required = false, Default = ".", HelpText = "Workspace directory. Either fully qualified or relative path.")]
            public string Workspace { get; set; }
        }
        [Verb("clean", HelpText = "")]
        class CleanOptions
        {
            [Option('w', "workspace", Required = false, Default = ".", HelpText = "Workspace directory. Either fully qualified or relative path.")]
            public string Workspace { get; set; }
        }
    }
}
