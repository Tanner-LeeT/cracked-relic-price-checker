using System.Globalization;
using System.Text.RegularExpressions;
using WarframeRelicScanner;

namespace CrackedRelicPriceChecker.Services
{
	public static class RelicTextMatcher
	{
		public static bool EnableDebugLogging = true; // Toggle this to see debug candidates

		public static List<string> MatchItems(string rawText, IEnumerable<string> knownItems)
		{
			var cleanedInput = rawText.Trim();
			var results = new List<string>();

			// Check for exact match
			var exact = knownItems.FirstOrDefault(item =>
				string.Equals(item, cleanedInput, StringComparison.OrdinalIgnoreCase));

			if (exact != null)
			{
				results.Add(exact);
				return results;
			}

			// Split into phrases by common separators
			var phrases = Regex.Split(cleanedInput, @"[\|\-–•\n]").Select(w => w.Trim()).Where(w => w.Length > 0);

			foreach (var phrase in phrases)
			{
				var match = FindBestMatch(phrase, knownItems.ToList(), out string debug);
				if (match != null)
					results.Add(match);
				else
					results.Add($"❌ Unknown: {phrase}");

				if (EnableDebugLogging && debug != null)
					MainWindow.AppendToLogFile(debug);
			}

			return results;
		}

		private static string? FindBestMatch(string text, List<string> knownItems, out string debugOut)
		{
			var input = text.ToLowerInvariant();
			var inputTokens = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			var bestScore = 0.0;
			string? bestMatch = null;
			var debug = new System.Text.StringBuilder();

			foreach (var item in knownItems)
			{
				var target = item.ToLowerInvariant();
				var score = GetSimilarityScore(input, target);

				// Boost score if tokens overlap (e.g., "braton", "prime")
				foreach (var token in inputTokens)
				{
					if (target.Contains(token))
						score += 0.02;
				}

				// Penalize mismatched prime names (e.g., Wisp vs Ash)
				var inputPrefix = inputTokens.FirstOrDefault();
				var targetPrefix = target.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
				if (!string.Equals(inputPrefix, targetPrefix, StringComparison.OrdinalIgnoreCase))
				{
					score -= 0.05;
				}

				debug.AppendLine($"   🔍 Candidate: {item} → Score: {score:0.000}");

				if (score > bestScore && score >= 0.84)
				{
					bestScore = score;
					bestMatch = item;
				}
			}

			debugOut = $"🧠 Fuzzy match candidates for '{text}':\n{debug}" +
					   (bestMatch != null ? $"✅ Best match: {bestMatch}\n" : $"❌ No good match.\n");

			return bestMatch;
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
