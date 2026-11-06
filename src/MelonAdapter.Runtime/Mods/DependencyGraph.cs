using System;
using System.Collections.Generic;
using System.Linq;

namespace MelonAdapter.Runtime.Mods
{
    /// <summary>
    /// Resolves declared mod dependencies and produces a stable load order
    /// (dependencies first, then priority, then name).
    /// </summary>
    public static class DependencyGraph
    {
        public sealed class Result
        {
            public List<ModRecord> Order { get; } = new List<ModRecord>();
            public List<ModRecord> MissingDependencies { get; } = new List<ModRecord>();
            public List<ModRecord> Cycles { get; } = new List<ModRecord>();
        }

        public static Result Sort(IEnumerable<ModRecord> records, IEnumerable<ModRecord> dependencyOnlyRecords)
        {
            Result result = new Result();
            List<ModRecord> nodes = records.Where(record => record.State != ModState.Rejected).ToList();
            Dictionary<string, ModRecord> byKey = new Dictionary<string, ModRecord>(StringComparer.OrdinalIgnoreCase);
            foreach (ModRecord record in nodes)
            {
                foreach (string key in KeysOf(record))
                {
                    if (!byKey.ContainsKey(key))
                    {
                        byKey[key] = record;
                    }
                }
            }

            HashSet<string> dependencyOnly = new HashSet<string>(
                (dependencyOnlyRecords ?? Enumerable.Empty<ModRecord>()).SelectMany(KeysOf),
                StringComparer.OrdinalIgnoreCase);

            Dictionary<ModRecord, List<ModRecord>> edges = new Dictionary<ModRecord, List<ModRecord>>();
            Dictionary<ModRecord, int> incoming = new Dictionary<ModRecord, int>();
            foreach (ModRecord record in nodes)
            {
                edges[record] = new List<ModRecord>();
                incoming[record] = 0;
            }

            foreach (ModRecord record in nodes)
            {
                foreach (string dependency in record.Dependencies)
                {
                    string key = NormalizeKey(dependency);
                    ModRecord provider;
                    if (byKey.TryGetValue(key, out provider) && provider != record)
                    {
                        if (!edges[provider].Contains(record))
                        {
                            edges[provider].Add(record);
                            incoming[record]++;
                        }

                        continue;
                    }

                    if (dependencyOnly.Contains(key))
                    {
                        // Shipped as a private dependency assembly; nothing to order.
                        continue;
                    }

                    record.State = ModState.Rejected;
                    record.FailureReason = "Missing required dependency '" + dependency + "'. The mod declares it in MelonAdditionalDependencies but no mod or private assembly provides it.";
                    result.MissingDependencies.Add(record);
                }
            }

            List<ModRecord> remaining = nodes.Where(record => record.State != ModState.Rejected).ToList();
            List<ModRecord> ready = remaining.Where(record => incoming[record] == 0).ToList();
            HashSet<ModRecord> ordered = new HashSet<ModRecord>();
            while (ready.Count > 0)
            {
                ready.Sort(Compare);
                ModRecord next = ready[0];
                ready.RemoveAt(0);
                if (!ordered.Add(next))
                {
                    continue;
                }

                result.Order.Add(next);
                foreach (ModRecord dependent in edges[next])
                {
                    if (--incoming[dependent] <= 0 && !ordered.Contains(dependent))
                    {
                        ready.Add(dependent);
                    }
                }
            }

            foreach (ModRecord record in remaining)
            {
                if (ordered.Contains(record))
                {
                    continue;
                }

                record.State = ModState.Rejected;
                record.FailureReason = "The mod is part of a dependency cycle (" + DescribeCycle(record, edges) + "). MelonAdapter refuses cyclic load order.";
                result.Cycles.Add(record);
            }

            return result;
        }

        private static string DescribeCycle(ModRecord start, Dictionary<ModRecord, List<ModRecord>> edges)
        {
            List<string> names = new List<string>();
            HashSet<ModRecord> visited = new HashSet<ModRecord>();
            ModRecord current = start;
            while (current != null && visited.Add(current))
            {
                names.Add(current.DisplayName);
                current = edges[current].FirstOrDefault(next => next == start) ?? edges[current].FirstOrDefault();
            }

            names.Add(start.DisplayName);
            return string.Join(" -> ", names.ToArray());
        }

        /// <summary>
        /// Stable load order: higher priority first, then display name, then shallower placement in the
        /// mods directory, then full path. The last two keys make the order independent of the file
        /// system enumeration order when two records look identical (for example a mod plus its copy in
        /// a subdirectory) — without them the duplicate-id check would pick an arbitrary winner.
        /// </summary>
        private static int Compare(ModRecord left, ModRecord right)
        {
            int priority = right.Priority.CompareTo(left.Priority);
            if (priority != 0)
            {
                return priority;
            }

            int name = string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase);
            if (name != 0)
            {
                return name;
            }

            int depth = left.Depth.CompareTo(right.Depth);
            if (depth != 0)
            {
                return depth;
            }

            return string.Compare(left.AssemblyPath, right.AssemblyPath, StringComparison.OrdinalIgnoreCase);
        }

        private static IEnumerable<string> KeysOf(ModRecord record)
        {
            if (!string.IsNullOrEmpty(record.AssemblySimpleName))
            {
                yield return NormalizeKey(record.AssemblySimpleName);
            }

            if (!string.IsNullOrEmpty(record.ModId))
            {
                yield return NormalizeKey(record.ModId);
            }

            if (!string.IsNullOrEmpty(record.FileName))
            {
                yield return NormalizeKey(record.FileName);
            }
        }

        public static string NormalizeKey(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            value = value.Trim();
            if (value.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                value = value.Substring(0, value.Length - 4);
            }

            return value;
        }
    }
}
