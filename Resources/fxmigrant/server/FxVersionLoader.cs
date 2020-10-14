#region License
//
// Copyright (c) 2007-2018, Sean Chambers <schambers80@gmail.com>
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//   http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
#endregion

using System;
using System.Collections.Generic;
using System.Data;
using System.Reflection;

using FluentMigrator;
using FluentMigrator.Expressions;
using FluentMigrator.Model;
using FluentMigrator.Runner.Versioning;
using FluentMigrator.Runner.VersionTableInfo;
using FluentMigrator.Infrastructure;
using FluentMigrator.Runner;
using FluentMigrator.Runner.Conventions;
using FluentMigrator.Runner.Initialization;
using FluentMigrator.Runner.Processors;

namespace FxMigrant
{
    public class FxVersionLoader : IVersionLoader
    {
        internal const string ResourceColumnName = "resource";

        private readonly IMigrationProcessor _processor;

        private readonly IConventionSet _conventionSet;
        private bool _versionSchemaMigrationAlreadyRun;
        private bool _versionMigrationAlreadyRun;
        private bool _versionUniqueMigrationAlreadyRun;
        private bool _versionDescriptionMigrationAlreadyRun;
        private bool _versionResourceMigrationAlreadyRun;
        private IVersionInfo _versionInfo;
        private IMigrationRunnerConventions Conventions { get; set; }

        [Obsolete]
        protected IAssemblyCollection Assemblies { get; set; }

        public IVersionTableMetaData VersionTableMetaData { get; }

        public IMigrationRunner Runner { get; set; }
        public VersionSchemaMigration VersionSchemaMigration { get; }
        public IMigration VersionMigration { get; }
        public IMigration VersionUniqueMigration { get; }
        public IMigration VersionDescriptionMigration { get; }
        public IMigration VersionResourceMigration { get; }

        public string Resource { get; }

        [Obsolete]
        internal FxVersionLoader(
            IMigrationRunner runner,
            Assembly assembly,
            IConventionSet conventionSet,
            IMigrationRunnerConventions conventions,
            IRunnerContext runnerContext)
            : this(runner, new SingleAssembly(assembly), conventionSet, conventions, runnerContext)
        {
        }

        [Obsolete]
        internal FxVersionLoader(IMigrationRunner runner, IAssemblyCollection assemblies,
            IConventionSet conventionSet,
            IMigrationRunnerConventions conventions,
            IRunnerContext runnerContext,
            IVersionTableMetaData versionTableMetaData = null,
            string resource = "")
        {
            _conventionSet = conventionSet;
            _processor = runner.Processor;

            Runner = runner;
            Assemblies = assemblies;

            Conventions = conventions;
            VersionTableMetaData = versionTableMetaData ?? CreateVersionTableMetaData(runnerContext);
            VersionMigration = new VersionMigration(VersionTableMetaData);
            VersionSchemaMigration = new VersionSchemaMigration(VersionTableMetaData);
            VersionUniqueMigration = new VersionUniqueMigration(VersionTableMetaData);
            VersionDescriptionMigration = new VersionDescriptionMigration(VersionTableMetaData);
            VersionResourceMigration = new VersionResourceMigration(VersionTableMetaData);

            VersionTableMetaData.ApplicationContext = runnerContext.ApplicationContext;

            Resource = resource;

            LoadVersionInfo();
        }

        public FxVersionLoader(
            IProcessorAccessor processorAccessor,
            IConventionSet conventionSet,
            IMigrationRunnerConventions conventions,
            IVersionTableMetaData versionTableMetaData,
            IMigrationRunner runner,
            string resource = "")
        {
            _conventionSet = conventionSet;
            _processor = processorAccessor.Processor;

            Runner = runner;

            Conventions = conventions;
            VersionTableMetaData = versionTableMetaData;
            VersionMigration = new VersionMigration(VersionTableMetaData);
            VersionSchemaMigration = new VersionSchemaMigration(VersionTableMetaData);
            VersionUniqueMigration = new VersionUniqueMigration(VersionTableMetaData);
            VersionDescriptionMigration = new VersionDescriptionMigration(VersionTableMetaData);
            VersionResourceMigration = new VersionResourceMigration(VersionTableMetaData);

            Resource = resource;

            LoadVersionInfo();
        }

        public void UpdateVersionInfo(long version)
        {
            UpdateVersionInfo(version, null);
        }

        public void UpdateVersionInfo(long version, string description)
        {
            var dataExpression = new InsertDataExpression();
            dataExpression.Rows.Add(CreateVersionInfoInsertionData(version, description));
            dataExpression.TableName = VersionTableMetaData.TableName;
            dataExpression.SchemaName = VersionTableMetaData.SchemaName;

            dataExpression.ExecuteWith(_processor);
        }

        public IVersionTableMetaData GetVersionTableMetaData()
        {
            return VersionTableMetaData;
        }

        protected virtual InsertionDataDefinition CreateVersionInfoInsertionData(long version, string description)
        {
            return new InsertionDataDefinition
                       {
                           new KeyValuePair<string, object>(VersionTableMetaData.ColumnName, version),
                           new KeyValuePair<string, object>(VersionTableMetaData.AppliedOnColumnName, DateTime.UtcNow),
                           new KeyValuePair<string, object>(VersionTableMetaData.DescriptionColumnName, description),
                           new KeyValuePair<string, object>(FxVersionLoader.ResourceColumnName, Resource),
                       };
        }

        public IVersionInfo VersionInfo
        {
            get => _versionInfo;
            set => _versionInfo = value ?? throw new ArgumentException("Cannot set VersionInfo to null");
        }

        public bool AlreadyCreatedVersionSchema => string.IsNullOrEmpty(VersionTableMetaData.SchemaName) ||
            _processor.SchemaExists(VersionTableMetaData.SchemaName);

        public bool AlreadyCreatedVersionTable => _processor.TableExists(VersionTableMetaData.SchemaName, VersionTableMetaData.TableName);

        public bool AlreadyMadeVersionUnique => _processor.ColumnExists(VersionTableMetaData.SchemaName, VersionTableMetaData.TableName, VersionTableMetaData.AppliedOnColumnName);

        public bool AlreadyMadeVersionDescription => _processor.ColumnExists(VersionTableMetaData.SchemaName, VersionTableMetaData.TableName, VersionTableMetaData.DescriptionColumnName);

        public bool AlreadyMadeVersionResource => _processor.ColumnExists(VersionTableMetaData.SchemaName, VersionTableMetaData.TableName, FxVersionLoader.ResourceColumnName);

        public bool OwnsVersionSchema => VersionTableMetaData.OwnsSchema;

        public void LoadVersionInfo()
        {
            if (!AlreadyCreatedVersionSchema && !_versionSchemaMigrationAlreadyRun)
            {
                Runner.Up(VersionSchemaMigration);
                _versionSchemaMigrationAlreadyRun = true;
            }

            if (!AlreadyCreatedVersionTable && !_versionMigrationAlreadyRun)
            {
                Runner.Up(VersionMigration);
                _versionMigrationAlreadyRun = true;
            }

            if (!AlreadyMadeVersionUnique && !_versionUniqueMigrationAlreadyRun)
            {
                Runner.Up(VersionUniqueMigration);
                _versionUniqueMigrationAlreadyRun = true;
            }

            if (!AlreadyMadeVersionDescription && !_versionDescriptionMigrationAlreadyRun)
            {
                Runner.Up(VersionDescriptionMigration);
                _versionDescriptionMigrationAlreadyRun = true;
            }

            if (!AlreadyMadeVersionResource && !_versionResourceMigrationAlreadyRun)
            {
                Runner.Up(VersionResourceMigration);
                _versionResourceMigrationAlreadyRun = true;
            }

            _versionInfo = new VersionInfo();

            if (!AlreadyCreatedVersionTable) return;

            var dataSet = _processor.ReadTableData(VersionTableMetaData.SchemaName, VersionTableMetaData.TableName);
            foreach (DataRow row in dataSet.Tables[0].Rows)
            {
                if (row[FxVersionLoader.ResourceColumnName].ToString() == Resource)
                {
                    _versionInfo.AddAppliedMigration(long.Parse(row[VersionTableMetaData.ColumnName].ToString()));
                }
            }
        }

        public void RemoveVersionTable()
        {
            var expression = new DeleteTableExpression { TableName = VersionTableMetaData.TableName, SchemaName = VersionTableMetaData.SchemaName };
            expression.ExecuteWith(_processor);

            if (OwnsVersionSchema && !string.IsNullOrEmpty(VersionTableMetaData.SchemaName))
            {
                var schemaExpression = new DeleteSchemaExpression { SchemaName = VersionTableMetaData.SchemaName };
                schemaExpression.ExecuteWith(_processor);
            }
        }

        public void DeleteVersion(long version)
        {
            var expression = new DeleteDataExpression { TableName = VersionTableMetaData.TableName, SchemaName = VersionTableMetaData.SchemaName };
            expression.Rows.Add(new DeletionDataDefinition
                                    {
                                        new KeyValuePair<string, object>(VersionTableMetaData.ColumnName, version),
                                        new KeyValuePair<string, object>(FxVersionLoader.ResourceColumnName, Resource)
                                    });
            expression.ExecuteWith(_processor);
        }

        [Obsolete]
        private IVersionTableMetaData CreateVersionTableMetaData(IRunnerContext runnerContext)
        {
            var type = typeof(DefaultVersionTableMetaData);

            var instance = (IVersionTableMetaData) Activator.CreateInstance(type);
            if (instance is ISchemaExpression schemaExpression)
            {
                _conventionSet.SchemaConvention?.Apply(schemaExpression);
            }

            return instance;
        }
    }

    public class VersionResourceMigration : Migration
    {
        private readonly IVersionTableMetaData _versionTableMeta;

        public VersionResourceMigration(IVersionTableMetaData versionTableMeta)
        {
            _versionTableMeta = versionTableMeta;
        }

        public override void Up()
        {
            Alter.Table(_versionTableMeta.TableName).InSchema(_versionTableMeta.SchemaName)
                .AddColumn(FxVersionLoader.ResourceColumnName).AsString(1024).NotNullable();

            Delete.Index(_versionTableMeta.UniqueIndexName)
                .OnTable(_versionTableMeta.TableName)
                .InSchema(_versionTableMeta.SchemaName);
        }

        public override void Down()
        {
            Delete.Column(FxVersionLoader.ResourceColumnName)
                  .FromTable(_versionTableMeta.TableName).InSchema(_versionTableMeta.SchemaName);
        }
    }
}