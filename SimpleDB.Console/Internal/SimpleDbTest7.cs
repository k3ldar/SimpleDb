using SimpleDB.Console.Abstractions;
using SimpleDB.Console.Tables;

using static System.Console;

namespace SimpleDB.Console.Internal
{
    /// <summary>
    /// Reproduces the "get-or-add" settings pattern reported against real world usage: a settings
    /// table is repeatedly queried by key, missing keys are inserted, existing keys are updated in
    /// place, and the process is restarted (closing and reopening the database over the same
    /// files) between rounds.
    ///
    /// Two distinct call shapes are exercised side by side, because the reported caller
    /// (ScrumOverview.Db.SimpleDb.Internal.Settings.Add) does NOT call InsertOrUpdate - it selects
    /// the row, mutates it, and calls the plain Update overload directly:
    /// - AddOrUpdate: select, and either insert a brand new row or InsertOrUpdate the mutated
    ///   existing one. Kept because it is a useful, stricter round trip in its own right.
    /// - SelectModifyUpdate: seeds a row once, then on every later cycle selects it, mutates it,
    ///   and calls table.Update(existing) directly, matching the real caller exactly.
    ///
    /// This exists to prove/disprove that TableRowDefinition.Immutable can be tripped purely by
    /// this connect / mutate / disconnect / reconnect cycle, without needing to reproduce the
    /// consuming application. Each round performs a fresh Select for every key - which, since
    /// SimpleDBOperations{T}.Select now returns defensive copies rather than the live cached
    /// instance - must be able to mutate and re-save the row it gets back without ever hitting the
    /// Immutable guard in TableRowDefinition.set_Id or any other setter.
    ///
    /// Runs after the checkpoint/crash recovery tests - it does not care about WAL flush timing,
    /// only that the same rows survive and remain editable across repeated restarts.
    /// </summary>
    public sealed class SimpleDbTest7 : ITestExecution
    {
        private const string KeyPrefix = "reconnect-cycle-setting-";
        private const string SelectUpdateKeyPrefix = "reconnect-cycle-select-update-";
        private const int KeyCount = 5;
        private const int CycleCount = 4;

        private readonly ITestDatabaseContext _database;
        private readonly Dictionary<string, string> _expected = [];

        public SimpleDbTest7(ITestDatabaseContext database)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
        }

        public int Order => 70;

        public string TestName => "SimpleDbTest7";

        public void Execute()
        {
            for (int cycle = 0; cycle < CycleCount; cycle++)
            {
                ISimpleDBOperations<SettingsDataRow> settingsTable = _database.Resolve<ISimpleDBOperations<SettingsDataRow>>();

                for (int i = 0; i < KeyCount; i++)
                {
                    string key = $"{KeyPrefix}{i}";
                    string value = $"value-cycle-{cycle}-key-{i}";

                    // Every other cycle touches only half the keys, so some rows are genuinely new
                    // inserts on their first appearance and repeatedly updated afterwards, while
                    // the rest are only ever inserted once and never revisited - mirroring a real
                    // settings table where not every key changes every time.
                    if (cycle == 0 || i % 2 == 0)
                    {
                        AddOrUpdate(settingsTable, key, value);
                        _expected[key] = value;
                    }
                }

                for (int i = 0; i < KeyCount; i++)
                {
                    string key = $"{SelectUpdateKeyPrefix}{i}";
                    string value = $"select-update-value-cycle-{cycle}-key-{i}";

                    if (cycle == 0)
                    {
                        // Seed the row exactly once - the real caller never re-inserts these,
                        // it only ever selects and updates an existing row from here on.
                        settingsTable.Insert(new SettingsDataRow
                        {
                            Name = key,
                            Value = value,
                        });
                    }
                    else if (i % 2 == 0)
                    {
                        SelectModifyUpdate(settingsTable, key, value);
                    }
                    else
                    {
                        continue;
                    }

                    _expected[key] = value;
                }

                // Read every key back within the same session before restarting, exercising the
                // exact select -> mutate -> reinsert/update path again against rows that were just
                // saved.
                AssertCurrentValues(settingsTable, $"cycle {cycle} before restart");

                WriteLine($"  Cycle {cycle}: inserted/updated settings, verified in-session.");

                _database.Restart();
            }
        }

        public void Validate()
        {
            ISimpleDBOperations<SettingsDataRow> settingsTable = _database.Resolve<ISimpleDBOperations<SettingsDataRow>>();

            AssertCurrentValues(settingsTable, "final restart");

            // One more full round trip: select every row, mutate it and save it again, exactly
            // as a caller repeatedly reconnecting and calling an "AddOrUpdate" style method would.
            // This is the step that previously reproduced Immutable being set on a row the caller
            // still intended to write to.
            foreach (string key in _expected.Keys.ToList())
            {
                string newValue = $"{_expected[key]}-revalidated";

                if (key.StartsWith(SelectUpdateKeyPrefix, StringComparison.Ordinal))
                    SelectModifyUpdate(settingsTable, key, newValue);
                else
                    AddOrUpdate(settingsTable, key, newValue);

                _expected[key] = newValue;
            }

            AssertCurrentValues(settingsTable, "post-validation update");

            WriteLine("  Settings survived repeated connect/disconnect cycles and remained editable throughout.");
        }

        /// <summary>
        /// Mirrors the reported real world pattern: look the row up by key, update it in place if
        /// found, otherwise insert a brand new row. Deliberately reuses the exact instance handed
        /// back by Select for the update path rather than constructing a fresh object, since that
        /// reuse is what the caller in the field was doing.
        /// </summary>
        private static void AddOrUpdate(ISimpleDBOperations<SettingsDataRow> table, string key, string value)
        {
            SettingsDataRow existing = table.Select(r => r.Name == key).FirstOrDefault();

            if (existing == null)
            {
                table.InsertOrUpdate(new SettingsDataRow
                {
                    Name = key,
                    Value = value,
                });
            }
            else
            {
                existing.Value = value;
                table.InsertOrUpdate(existing);
            }
        }

        /// <summary>
        /// Mirrors the actual reported caller: the row is expected to already exist, it is
        /// selected, mutated, and saved via the plain Update overload - never InsertOrUpdate and
        /// never a freshly constructed instance. This is the exact shape that must keep working
        /// against the defensive copy Select now returns.
        /// </summary>
        private static void SelectModifyUpdate(ISimpleDBOperations<SettingsDataRow> table, string key, string value)
        {
            SettingsDataRow existing = table.Select(r => r.Name == key).FirstOrDefault()
                ?? throw new InvalidOperationException(
                    $"Settings row '{key}' was expected to already exist for the select/modify/update path.");

            existing.Value = value;
            table.Update(existing);
        }

        private void AssertCurrentValues(ISimpleDBOperations<SettingsDataRow> table, string scenario)
        {
            foreach (KeyValuePair<string, string> expectedSetting in _expected)
            {
                List<SettingsDataRow> matches = [.. table.Select(r => r.Name == expectedSetting.Key)];

                if (matches.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"Settings row '{expectedSetting.Key}' is missing ({scenario}).");
                }

                if (matches.Count > 1)
                {
                    throw new InvalidOperationException(
                        $"Settings row '{expectedSetting.Key}' appears {matches.Count} times ({scenario}); " +
                        "repeated add-or-update cycles must not duplicate rows.");
                }

                if (matches[0].Value != expectedSetting.Value)
                {
                    throw new InvalidOperationException(
                        $"Settings row '{expectedSetting.Key}' has value '{matches[0].Value}', " +
                        $"expected '{expectedSetting.Value}' ({scenario}).");
                }
            }
        }
    }
}
