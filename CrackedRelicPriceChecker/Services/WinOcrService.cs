using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace CrackedRelicPriceChecker.Services
{
	public static class WinOcrService
	{
		public static async Task<List<string>> ExtractItemNamesAsync(Bitmap bitmap)
		{
			var results = new List<string>();
			var debugOutput = new StringBuilder();

			try
			{
				var softwareBitmap = await ConvertToSoftwareBitmapAsync(bitmap);
				var ocrEngine = OcrEngine.TryCreateFromUserProfileLanguages();
				var result = await ocrEngine.RecognizeAsync(softwareBitmap);

				var grouped = result.Lines
					.OrderBy(line => line.Words.Min(w => w.BoundingRect.Y))
					.ToList();

				var combinedGroups = new List<List<OcrLine>>();
				foreach (var line in grouped)
				{
					if (combinedGroups.Count == 0 ||
						Math.Abs(line.Words.Min(w => w.BoundingRect.Y) -
								 combinedGroups.Last().Last().Words.Min(w => w.BoundingRect.Y)) > 10)
					{
						combinedGroups.Add(new List<OcrLine> { line });
					}
					else
					{
						combinedGroups.Last().Add(line);
					}
				}

				foreach (var group in combinedGroups)
				{
					foreach (var l in group)
						debugOutput.AppendLine($"🧾 Line: {l.Text.Trim()}");

					var fullText = string.Join(" ", group.Select(l => l.Text.Trim()));
					results.Add(fullText);
					debugOutput.AppendLine($"➡️ Combined: {fullText}");
					debugOutput.AppendLine();
				}
			}
			catch (Exception ex)
			{
				results.Add($"❌ OCR Error: {ex.Message}");
				debugOutput.AppendLine($"❌ OCR Error: {ex.Message}");
			}

			// Write the debug log for the OCR step
			File.AppendAllText("log.txt", $"[OCR DEBUG {DateTime.Now:HH:mm:ss}]\n{debugOutput}\n");

			return results;
		}

		private static async Task<SoftwareBitmap> ConvertToSoftwareBitmapAsync(Bitmap bitmap)
		{
			using var ms = new MemoryStream();
			bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
			ms.Position = 0;

			var ras = ms.AsRandomAccessStream();
			var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(ras);
			return await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore);
		}
	}
}
