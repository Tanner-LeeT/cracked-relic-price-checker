using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;

/// <summary>
/// Initial call to the API to fetch all the item names from the relic details. 
/// </summary>
class Program
{
	static async Task Main()
	{
		string url = "https://drops.warframestat.us/data/all.json";
		using var client = new HttpClient();
		var json = await client.GetStringAsync(url);

		using JsonDocument doc = JsonDocument.Parse(json);
		var relicRewards = new HashSet<string>();

		if (doc.RootElement.TryGetProperty("relics", out var relics))
		{
			foreach (var relic in relics.EnumerateArray())
			{
				if (relic.TryGetProperty("rewards", out var rewards))
				{
					foreach (var reward in rewards.EnumerateArray())
					{
						if (reward.TryGetProperty("itemName", out var itemNameProp))
						{
							var itemName = itemNameProp.GetString();
							if (!string.IsNullOrWhiteSpace(itemName))
							{
								relicRewards.Add(itemName);
							}
						}
					}
				}
			}
		}
		else
		{
			Console.WriteLine("❌ 'relics' key not found in the JSON.");
		}

		// Export to KnownItems.cs
		var lines = new List<string>
		{
			"namespace WarframeRelicScanner.Assets",
			"{",
			"\tpublic static class KnownItems",
			"\t{",
			"\t\tpublic static readonly List<string> KnownRelicRewards = new()",
			"\t\t{"
		};

		lines.AddRange(relicRewards.OrderBy(s => s).Select(r => $"\t\t\t\"{r}\","));
		lines.AddRange(new[] { "\t\t};", "\t}", "}" });

		var outputPath = @"..\..\..\..\CrackedRelicPriceChecker\Assets\KnownItems.cs";
		File.WriteAllLines(outputPath, lines);

		System.Console.WriteLine("Scraping complete. Saved to KnownItems.cs");
	}
}
