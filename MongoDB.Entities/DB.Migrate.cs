using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace MongoDB.Entities
{
    public static partial class DB
    {
        /// <summary>
        /// Discover and run migrations from the same assembly as the specified type.
        /// </summary>
        /// <typeparam name="T">A type that is from the same assembly as the migrations you want to run</typeparam>
        public static async Task MigrateAsync<T>(Action<string> logAction = null) where T : class
        {
            await MigrateAsync(typeof(T), logAction).ConfigureAwait(false);
        }

        /// <summary>
        /// Executes migration classes that implement the IMigration interface in the correct order to transform the database.
        /// <para>TIP: Write classes with names such as: _001_rename_a_field.cs, _002_delete_a_field.cs, etc. and implement IMigration interface on them. Call this method at the startup of the application in order to run the migrations.</para>
        /// </summary>
        public static async Task MigrateAsync(Action<string> logAction = null)
        {
            await MigrateAsync(null, logAction).ConfigureAwait(false);
        }

        private static async Task MigrateAsync(Type targetType, Action<string> logAction)
        {
            IEnumerable<Assembly> assemblies;

            if (targetType == null)
            {
                var excludes = new[]
                {
                    "Microsoft.",
                    "System.",
                    "MongoDB.",
                    "testhost.",
                    "netstandard",
                    "Newtonsoft.",
                    "mscorlib",
                    "NuGet."
                };

                assemblies = AppDomain.CurrentDomain
                    .GetAssemblies()
                    .Where(a =>
                          (!a.IsDynamic && !excludes.Any(n => a.FullName.StartsWith(n))) ||
                          a.FullName.StartsWith("MongoDB.Entities.Tests"));
            }
            else
            {
                assemblies = new[] { targetType.Assembly };
            }

            var types = assemblies
                .SelectMany(a => a.GetTypes())
                .Where(t => t.GetInterfaces().Contains(typeof(IMigration)));

            if (!types.Any())
                throw new InvalidOperationException("Didn't find any classes that implement IMigrate interface.");

            var lastMigNum = (
                await Find<Migration, int>()
                      .Sort(m => m.Number, Order.Descending)
                      .Limit(1)
                      .Project(m => m.Number)
                      .ExecuteAsync()
                      .ConfigureAwait(false))
                .SingleOrDefault();

            var migrations = GetMigrationsList(types);

            logAction("Already applied migrations:");

            foreach (var migration in migrations.Where(pair => pair.Key <= lastMigNum))
            {
                logAction(migration.Key.ToString());
            }

            var sw = new Stopwatch();

            logAction("New migrations:");

            foreach (var migration in migrations.Where(pair => pair.Key > lastMigNum))
            {
                sw.Start();
                await migration.Value.Migration.UpgradeAsync().ConfigureAwait(false);
                var mig = new Migration
                {
                    Number = migration.Key,
                    Name = migration.Value.GetType().Name,
                    TimeTakenSeconds = sw.Elapsed.TotalSeconds,
                    Description = null
                };
                await SaveAsync(mig).ConfigureAwait(false);
                sw.Stop();
                sw.Reset();

                logAction(migration.Key.ToString());
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="types"></param>
        /// <returns></returns>
        private static IDictionary<int, IMigrationProvider> GetMigrationsList(IEnumerable<Type> types)
        {
            var migrations = new SortedDictionary<int, IMigrationProvider>();

            foreach (var type in types)
            {
                var migrationAttribute = type.GetCustomAttribute<MigrationAttribute>();

                int currentMigrationNumber;

                if (migrationAttribute != null)
                    currentMigrationNumber = migrationAttribute.Number;
                else
                {
                    var success = int.TryParse(type.Name.Split('_')[1], out currentMigrationNumber);

                    if (!success)
                        throw new InvalidOperationException("Failed to get migration number not from attribute nor from the class name. " +
                            "Use MigrationAttribute class or make sure that name of the migration classes like: _001_some_migration_name.cs");
                }
                
                migrations.Add(currentMigrationNumber, new MigrationProvider(currentMigrationNumber, type, migrationAttribute?.Description));
            }

            return migrations;
        }
    }

    internal interface IMigrationProvider
    {
        IMigration Migration { get; }

        Migration GetMigrationEntity(double totalSecondsElapsed);
    }

    internal class MigrationProvider : IMigrationProvider
    {
        private readonly int _version;
        private readonly string _description;
        private readonly Type _migrationType;
        private Lazy<IMigration> _lazyMigration;

        public IMigration Migration => _lazyMigration.Value;

        public MigrationProvider(int version, Type migrationType)
            : this(version, migrationType, null)
        {
        }

        public MigrationProvider(int version, Type migrationType, string description)
        {
            _version = version;
            _description = description;
            _migrationType = migrationType;
            _lazyMigration = new Lazy<IMigration>(() => (IMigration)Activator.CreateInstance(_migrationType));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="totalSecondsElapsed"></param>
        /// <returns></returns>
        public Migration GetMigrationEntity(double totalSecondsElapsed)
        {
            return new Migration
            {
                Number = _version,
                Name = Migration.GetType().Name,
                TimeTakenSeconds = totalSecondsElapsed,
                Description = _description
            };
        }
    }
}
