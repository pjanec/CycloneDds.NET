using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DdsMonitor.Engine;

/// <summary>
/// Computes the set of topic types that should initially be <em>excluded</em> from
/// auto-subscription, honouring explicit CLI include/exclude patterns and the saved
/// workspace exclusion list.
/// </summary>
public static class TopicFilterService
{
    /// <summary>
    /// Determines which topic types to exclude from auto-subscription.
    /// </summary>
    /// <param name="allTopics">All topics discovered from the loaded assemblies.</param>
    /// <param name="cliIncludes">
    ///   Optional explicit-include patterns provided via CLI (e.g. <c>--AppSettings:IncludeTopics=…</c>).
    ///   Wildcard <c>*</c> matches any sequence of characters.
    ///   When non-empty the initial subscription set starts <em>empty</em> and is extended only by
    ///   the topics matching these patterns; the saved exclusion list is ignored.
    /// </param>
    /// <param name="cliExcludes">
    ///   Optional explicit-exclude patterns provided via CLI (e.g. <c>--AppSettings:ExcludeTopics=…</c>).
    ///   Applied after the include pass (if any).  When non-empty without <paramref name="cliIncludes"/>,
    ///   the saved exclusion list is ignored.
    /// </param>
    /// <param name="savedExcludes">
    ///   Fully-qualified type names persisted in the workspace file.  Used only when neither
    ///   <paramref name="cliIncludes"/> nor <paramref name="cliExcludes"/> are specified.
    /// </param>
    /// <returns>
    ///   A read-only set of <see cref="Type"/> objects that must be placed into
    ///   <c>IDdsBridge.ExplicitlyUnsubscribedTopicTypes</c> before auto-subscription runs.
    /// </returns>
    public static IReadOnlySet<Type> ComputeExcluded(
        IReadOnlyCollection<TopicMetadata> allTopics,
        IEnumerable<string>? cliIncludes,
        IEnumerable<string>? cliExcludes,
        IEnumerable<string>? savedExcludes)
    {
        if (allTopics == null) throw new ArgumentNullException(nameof(allTopics));

        var includePatterns = NormalizePatterns(cliIncludes);
        var excludePatterns = NormalizePatterns(cliExcludes);
        var savedPatterns   = NormalizePatterns(savedExcludes);

        bool hasCli = includePatterns.Count > 0 || excludePatterns.Count > 0;

        if (includePatterns.Count > 0)
        {
            // Start all-excluded; topics that match an include pattern are added back;
            // topics that additionally match an exclude pattern are removed again.
            var result = new HashSet<Type>();
            foreach (var topic in allTopics)
            {
                var fqn = topic.TopicType.FullName ?? topic.TopicType.Name;
                bool included = MatchesAny(fqn, includePatterns);
                bool excluded = MatchesAny(fqn, excludePatterns);

                if (!included || excluded)
                    result.Add(topic.TopicType);
            }
            return result;
        }

        if (excludePatterns.Count > 0)
        {
            // Start all-subscribed; topics matching an exclude pattern become excluded.
            var result = new HashSet<Type>();
            foreach (var topic in allTopics)
            {
                var fqn = topic.TopicType.FullName ?? topic.TopicType.Name;
                if (MatchesAny(fqn, excludePatterns))
                    result.Add(topic.TopicType);
            }
            return result;
        }

        // Neither CLI option provided – apply saved exclusions by resolving names to types.
        if (savedPatterns.Count > 0)
        {
            var result = new HashSet<Type>();
            foreach (var topic in allTopics)
            {
                var fqn = topic.TopicType.FullName ?? topic.TopicType.Name;
                if (MatchesAny(fqn, savedPatterns))
                    result.Add(topic.TopicType);
            }
            return result;
        }

        return new HashSet<Type>();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true if <paramref name="fullName"/> matches at least one of the given
    /// glob-style patterns (only <c>*</c> wildcards are supported).
    /// </summary>
    public static bool MatchesAny(string fullName, IReadOnlyList<string> patterns)
    {
        foreach (var pattern in patterns)
        {
            if (GlobMatch(fullName, pattern))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Performs case-sensitive glob matching where <c>*</c> matches any sequence of
    /// characters (including none) and <c>?</c> matches any single character.
    /// </summary>
    public static bool GlobMatch(string input, string pattern)
    {
        // Convert glob pattern to regex equivalents.
        var regexPattern = "^" + Regex.Escape(pattern)
            .Replace(@"\*", ".*")
            .Replace(@"\?", ".") + "$";

        return Regex.IsMatch(input, regexPattern, RegexOptions.None);
    }

    private static IReadOnlyList<string> NormalizePatterns(IEnumerable<string>? raw)
    {
        if (raw == null) return Array.Empty<string>();
        var result = new List<string>();
        foreach (var s in raw)
        {
            if (!string.IsNullOrWhiteSpace(s))
                result.Add(s.Trim());
        }
        return result;
    }
}
