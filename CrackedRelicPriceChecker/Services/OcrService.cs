using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using Tesseract;
using WarframeRelicScanner.Assets;
using CrackedRelicPriceChecker.Services;

public class OcrService
{
	private readonly TesseractEngine _engine = new("./tessdata", "eng", EngineMode.Default);

	public Task<List<string>> ExtractItemNamesAsync(Bitmap bitmap)
	{
		return Task.Run(() =>
		{
			var results = new List<string>();

			// Step 1: Preprocess
			var cleaned = PreprocessImage(bitmap);
			cleaned.SetResolution(300, 300); // improve OCR accuracy

			// Step 2: OCR
			using var img = Pix.LoadFromMemory((byte[])new ImageConverter().ConvertTo(cleaned, typeof(byte[])));
			using var page = _engine.Process(img, PageSegMode.Auto);

			string rawText = page.GetText();

			foreach (var line in rawText.Split('\n'))
			{
				var trimmed = line.Trim();
				if (!string.IsNullOrWhiteSpace(trimmed))
					results.Add(FixCommonErrors(trimmed));
			}

			return results;
		});
	}

	public static Bitmap ApplyGaussianBlur(Bitmap bmp)
	{
		using var image = new Bitmap(bmp);
		using var g = Graphics.FromImage(image);
		g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
		return new Bitmap(image);
	}

	private Bitmap PreprocessImage(Bitmap original)
	{
		Bitmap grayscale = new(original.Width, original.Height);

		// Step 1: Grayscale conversion
		using (Graphics g = Graphics.FromImage(grayscale))
		{
			var matrix = new System.Drawing.Imaging.ColorMatrix(
				new float[][]
				{
				new float[] {0.3f, 0.3f, 0.3f, 0, 0},
				new float[] {0.59f, 0.59f, 0.59f, 0, 0},
				new float[] {0.11f, 0.11f, 0.11f, 0, 0},
				new float[] {0, 0, 0, 1, 0},
				new float[] {0, 0, 0, 0, 1}
				});

			var attributes = new System.Drawing.Imaging.ImageAttributes();
			attributes.SetColorMatrix(matrix);
			g.DrawImage(original, new Rectangle(0, 0, grayscale.Width, grayscale.Height),
				0, 0, original.Width, original.Height, GraphicsUnit.Pixel, attributes);
		}

		// Step 2: Apply aggressive binarization
		Bitmap output = new(grayscale.Width, grayscale.Height);
		for (int y = 0; y < grayscale.Height; y++)
		{
			for (int x = 0; x < grayscale.Width; x++)
			{
				Color pixel = grayscale.GetPixel(x, y);
				int value = pixel.R > 140 ? 255 : 0;  // Increase cutoff
				value = 255 - value; // Invert to black text on white
				output.SetPixel(x, y, Color.FromArgb(value, value, value));
			}
		}

		output.Save("preprocessed_debug.png", System.Drawing.Imaging.ImageFormat.Png);
		return output;
	}

	public static string FixCommonErrors(string input)
	{
		if (string.IsNullOrWhiteSpace(input))
			return input;

		// Normalize to remove accents like é → e
		input = input.Normalize(NormalizationForm.FormD);
		var sb = new StringBuilder();

		foreach (char c in input)
		{
			var cat = CharUnicodeInfo.GetUnicodeCategory(c);
			if (cat != UnicodeCategory.NonSpacingMark)
				sb.Append(c);
		}

		string cleaned = sb.ToString();

		// Aggressive OCR error cleanup
		cleaned = cleaned
			.Replace(@"B\'B", "Bl")
			.Replace(@"elB", "Bl")
			.Replace(@"B\‘", "Bl")
			.Replace("Blue‘print", "Blueprint")
			.Replace("BlB", "Bl")
			.Replace("BlBlueprint", "Blueprint")
			.Replace("B\\Blueprint", "Blueprint")
			.Replace(@"B\Blueprint", "Blueprint")
			.Replace(@"B\", "")
			.Replace("ueprint", "Blueprint")
			.Replace("lueprint", "Blueprint")
			.Replace("eprint", "Blueprint")
			.Replace("é", "e")
			.Replace("'", "")
			.Replace("‘", "")
			.Replace("’", "")
			.Replace("|", " | ")
			.Replace("  ", " ")
			.Replace("Prime Prime", "Prime")
			.Replace("V4", "")
			.Replace("- -", "")
			.Trim();

		// Regex fallback for any weird glitch that ends in "Blueprint"
		cleaned = System.Text.RegularExpressions.Regex.Replace(
			cleaned,
			@"\b\w*Blueprint\b",
			"Blueprint",
			System.Text.RegularExpressions.RegexOptions.IgnoreCase
		);

		// Remove leading "I " from OCR drift
		if (cleaned.StartsWith("I "))
			cleaned = cleaned.Substring(2);

		return cleaned;
	}

	public static string GetBestMatch(string input)
	{
		input = input.Trim().ToLowerInvariant();
		int bestScore = int.MaxValue;
		string? bestMatch = null;

		foreach (var item in KnownItems.KnownRelicRewards)
		{
			int score = LevenshteinDistance(input, item.ToLowerInvariant());
			if (score < bestScore)
			{
				bestScore = score;
				bestMatch = item;
			}
		}

		return bestScore <= 5 ? bestMatch! : input; // return corrected if close enough
	}

	public static int LevenshteinDistance(string a, string b)
	{
		var dp = new int[a.Length + 1, b.Length + 1];

		for (int i = 0; i <= a.Length; i++) dp[i, 0] = i;
		for (int j = 0; j <= b.Length; j++) dp[0, j] = j;

		for (int i = 1; i <= a.Length; i++)
		{
			for (int j = 1; j <= b.Length; j++)
			{
				int cost = a[i - 1] == b[j - 1] ? 0 : 1;
				dp[i, j] = Math.Min(
					Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
					dp[i - 1, j - 1] + cost);
			}
		}

		return dp[a.Length, b.Length];
	}



}
