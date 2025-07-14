using System.Net.Http;
using System.Text.Json;

public class WfMarketClient
{
	private readonly HttpClient _http = new();

	public async Task<string> GetItemPriceAsync(string itemName)
	{
		try
		{
			var urlName = FormatMarketName(itemName);
			var url = $"https://api.warframe.market/v1/items/{urlName}/orders";

			var request = new HttpRequestMessage(HttpMethod.Get, url);
			request.Headers.Add("Accept", "application/json");
			request.Headers.Add("platform", "pc");
			request.Headers.Add("language", "en");

			var response = await _http.SendAsync(request);
			response.EnsureSuccessStatusCode();

			var json = await response.Content.ReadAsStringAsync();
			var parsed = System.Text.Json.JsonDocument.Parse(json);

			var orders = parsed.RootElement
				.GetProperty("payload")
				.GetProperty("orders")
				.EnumerateArray()
				.Where(order =>
					order.GetProperty("user").GetProperty("status").GetString() == "ingame" &&
					order.GetProperty("order_type").GetString() == "sell")
				.OrderBy(order => order.GetProperty("platinum").GetInt32())
				.FirstOrDefault();

			if (orders.ValueKind != JsonValueKind.Undefined)
			{
				int price = orders.GetProperty("platinum").GetInt32();
				return $"{price}p";
			}

			return "No in-game sell orders";
		}
		catch
		{
			return "❌ Price lookup failed";
		}
	}

	private string FormatMarketName(string item)
	{
		// Example: "Lavos Prime Blueprint" -> "lavos_prime_blueprint"
		return item.ToLowerInvariant()
				   .Replace("’", "")
				   .Replace("'", "")
				   .Replace("&", "and")
				   .Replace(" ", "_")
				   .Replace("-", "_");
	}
}
