using System.Text.RegularExpressions;
using System.Globalization;

namespace CrackedRelicPriceChecker.Services
{
	public static class RelicTextMatcher
	{
		public static List<string> MatchItems(string rawText, IEnumerable<string> knownItems)
		{
			var cleanedInput = rawText.Trim();
			var results = new List<string>();

			// Check for direct match first
			var exact = knownItems.FirstOrDefault(item =>
				string.Equals(item, cleanedInput, StringComparison.OrdinalIgnoreCase));

			if (exact != null)
			{
				results.Add(exact);
				return results;
			}

			// Otherwise, fuzzy sub-match
			var phrases = Regex.Split(cleanedInput, @"[\|\-–•\n]").Select(w => w.Trim()).Where(w => w.Length > 0);

			foreach (var phrase in phrases)
			{
				var matches = FindKnownSubstrings(phrase, knownItems.ToList());
				if (matches.Count > 0)
					results.AddRange(matches);
				else
					results.Add($"❌ Unknown: {phrase}");
			}

			return results;
		}

		private static List<string> FindKnownSubstrings(string text, List<string> knownItems)
		{
			var matches = new List<string>();
			var input = text.ToLowerInvariant();

			foreach (var item in knownItems)
			{
				var target = item.ToLowerInvariant();

				// Trying fuzzy match if full string contains the known item
				if (input.Contains(target))
				{
					matches.Add(item);
					input = input.Replace(target, ""); // Remove match to avoid overlap
				}
				else
				{
					double score = GetSimilarityScore(input, target);
					if (score >= 0.85 && input.Length >= 10)
					{
						matches.Add(item);
						break;
					}
				}
			}

			return matches;
		}

		private static double GetSimilarityScore(string a, string b)
		{
			int distance = Levenshtein(a, b);
			int maxLen = Math.Max(a.Length, b.Length);
			return maxLen == 0 ? 1.0 : 1.0 - (double)distance / maxLen;
		}

		private static int Levenshtein(string a, string b)
		{
			int[,] d = new int[a.Length + 1, b.Length + 1];

			for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
			for (int j = 0; j <= b.Length; j++) d[0, j] = j;

			for (int i = 1; i <= a.Length; i++)
			{
				for (int j = 1; j <= b.Length; j++)
				{
					int cost = a[i - 1] == b[j - 1] ? 0 : 1;
					d[i, j] = Math.Min(
						Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
						d[i - 1, j - 1] + cost
					);
				}
			}

			return d[a.Length, b.Length];
		}
	}
}
