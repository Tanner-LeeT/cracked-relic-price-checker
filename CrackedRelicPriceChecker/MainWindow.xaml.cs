using CrackedRelicPriceChecker.Services;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using WarframeRelicScanner.Assets;

namespace WarframeRelicScanner
{
	public partial class MainWindow : Window
	{
		private readonly OcrService _ocrService = new();
		private readonly WfMarketClient _marketClient = new();

		[DllImport("user32.dll")]
		private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
		[DllImport("user32.dll")]
		private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

		private const int HOTKEY_ID = 9000;
		private const uint MOD_ALT = 0x0001;     // Alt
		private const uint MOD_CONTROL = 0x0002; // Ctrl
		private const uint MOD_SHIFT = 0x0004;   // Shift
		private const uint MOD_WIN = 0x0008;     // Windows key
		private const int WM_HOTKEY = 0x0312;

		public MainWindow()
		{
			InitializeComponent();

			this.SourceInitialized += (s, e) => SetupHotkey();
		}

		private async void ScanButton_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				ResultsTextBox.Text = "Scanning screen...";

				var firstMonitorBounds = System.Windows.Forms.Screen.AllScreens[0].Bounds;

				// Capture full screen
				using var fullScreenshot = ScreenshotService.CaptureRegion(firstMonitorBounds);

				var rewardRegions = GetRewardRegions();
				var rawLines = new List<string>();
				var matchedItems = new List<string>();
				var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				var debugLog = new StringBuilder();

				for (int i = 0; i < rewardRegions.Count; i++)
				{
					using var rawCrop = fullScreenshot.Clone(rewardRegions[i], fullScreenshot.PixelFormat);

					// No external preprocessing here — it's handled inside ExtractItemNamesAsync
					var ocrLines = await _ocrService.ExtractItemNamesAsync(rawCrop);

					rawCrop.Save($"debug_crop_{i + 1}.png", System.Drawing.Imaging.ImageFormat.Png); // Optional debug

					if (ocrLines.Count == 0)
					{
						debugLog.AppendLine($"❌ OCR returned no text for block {i + 1}");
						continue;
					}
					// Combine all OCR lines for this block into one
					string combinedLine = string.Join(" ", ocrLines.Where(l => !string.IsNullOrWhiteSpace(l))).Trim();
					AppendToLogFile($"[Block {i + 1}] Raw OCR Combined: {combinedLine}");

					var cleaned = OcrService.FixCommonErrors(combinedLine);
					AppendToLogFile($"[Block {i + 1}] Cleaned: {cleaned}");

					var matches = RelicTextMatcher.MatchItems(cleaned, KnownItems.KnownRelicRewards);

					foreach (var match in matches)
					{
						AppendToLogFile($"[Block {i + 1}] Matched: {match}");
						debugLog.AppendLine($"[{i + 1}] Matched: {match}");

						if (!match.StartsWith("❌") && seen.Add(match))
							matchedItems.Add(match);
					}
				}

				if (matchedItems.Count == 0)
				{
					ResultsTextBox.Text = "No matched items found.";
					return;
				}

				debugLog.AppendLine();
				debugLog.AppendLine("✅ Final Matched Items:");
				foreach (var item in matchedItems)
				{
					debugLog.AppendLine(item);
				}

				ResultsTextBox.Text = debugLog.ToString();

				// Price lookup
				var priceResults = new List<string>();
				foreach (var item in matchedItems)
				{
					var price = await _marketClient.GetItemPriceAsync(item);
					priceResults.Add($"{item}: {price}");
				}

				ResultsTextBox.Text = string.Join(Environment.NewLine, priceResults);
			}
			catch (Exception ex)
			{
				System.Windows.MessageBox.Show($"Scan failed:\n{ex.Message}");
			}
		}

		private async void TestOcrButton_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				string imagePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "test_image.jpeg");

				if (!File.Exists(imagePath))
				{

					System.Windows.MessageBox.Show("Test image not found!");
					return;
				}

				using var fullImage = new Bitmap(imagePath);
				using var bmp = CropRelicRewardArea(fullImage);

				// Optional: Save cropped preview to disk
				string outputPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cropped_output.png");
				bmp.Save(outputPath, System.Drawing.Imaging.ImageFormat.Png);

				var ocrService = new OcrService();
				var rawLines = await ocrService.ExtractItemNamesAsync(bmp);

				// Combine all lines into one text blob
				string joinedRaw = string.Join(" ", rawLines);

				// Use Regex to split better — even if '|' is missing
				var roughItems = Regex.Split(joinedRaw, @"(?<=prime blueprint)|(?<=prime barrel)|(?<=prime receiver)|(?<=prime stock)|(?<=prime grip)|(?<=prime link)")
					.Select(s => s.Trim())
					.Where(s => s.Length > 0)
					.ToList();

				var matchedItems = new List<string>();
				var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

				var debugLog = new StringBuilder();

				foreach (var rawLine in rawLines)
				{
					debugLog.AppendLine($"Raw OCR Line: {rawLine}");

					// Split line into candidates using common separators
					var candidates = Regex.Split(rawLine, @"[\|\-•]").Select(s => s.Trim()).Where(s => s.Length > 0);

					foreach (var candidate in candidates)
					{
						debugLog.AppendLine($"Trying: '{candidate}'");

						// Apply cleanup
						var fixedCandidate = OcrService.FixCommonErrors(candidate);

						// Use fuzzy matcher
						var matches = RelicTextMatcher.MatchItems(fixedCandidate, KnownItems.KnownRelicRewards);

						foreach (var match in matches)
						{
							debugLog.AppendLine($"Matched: {match}");

							if (!match.StartsWith("❌") && seen.Add(match))
							{
								matchedItems.Add(match);
							}
						}
					}
				}

				debugLog.AppendLine();
				debugLog.AppendLine("✅ Final Matched Items:");
				foreach (var item in matchedItems)
				{
					debugLog.AppendLine(item);
				}

				ResultsTextBox.Text = debugLog.ToString();

				var marketClient = new WfMarketClient();
				var priceResults = new List<string>();

				foreach (var item in matchedItems)
				{
					var price = await marketClient.GetItemPriceAsync(item);
					priceResults.Add($"{item}: {price}");
				}

				ResultsTextBox.Text = string.Join(Environment.NewLine, priceResults);

			}

			catch (Exception ex)
			{
				System.Windows.MessageBox.Show($"OCR Test failed:\n{ex.Message}");
			}
		}

		private static List<System.Drawing.Rectangle> GetRewardRegions()
		{
			return new List<System.Drawing.Rectangle>
			{
			new(480, 405, 230, 55),  // Reward 1
			new(722, 405, 230, 55),  // Reward 2
			new(964, 405, 230, 55),  // Reward 3
			new(1210, 405, 230, 55), // Reward 4
			};
		}

		public static Bitmap CropRelicRewardArea(Bitmap source)
		{
			var crop = new System.Drawing.Rectangle(450, 405, 1000, 55);
			var cropped = source.Clone(crop, source.PixelFormat);
			return PreprocessForOcr(cropped);
		}

		public static Bitmap PreprocessForOcr(Bitmap original)
		{
			Bitmap gray = new Bitmap(original.Width, original.Height);
			using (Graphics g = Graphics.FromImage(gray))
			{
				var colorMatrix = new System.Drawing.Imaging.ColorMatrix(
					new float[][]
					{
				new float[] {0.3f, 0.3f, 0.3f, 0, 0},
				new float[] {0.59f, 0.59f, 0.59f, 0, 0},
				new float[] {0.11f, 0.11f, 0.11f, 0, 0},
				new float[] {0,    0,    0,    1, 0},
				new float[] {0,    0,    0,    0, 1}
					});

				var attributes = new System.Drawing.Imaging.ImageAttributes();
				attributes.SetColorMatrix(colorMatrix);
				g.DrawImage(original,
					new System.Drawing.Rectangle(0, 0, original.Width, original.Height),
					0, 0, original.Width, original.Height,
					GraphicsUnit.Pixel,
					attributes);
			}

			return gray;
		}

		private void SetupHotkey()
		{
			var helper = new System.Windows.Interop.WindowInteropHelper(this);
			var source = System.Windows.Interop.HwndSource.FromHwnd(helper.Handle);
			source.AddHook(HwndHook);

			RegisterHotKey(helper.Handle, HOTKEY_ID, MOD_SHIFT | MOD_ALT, 0x43); // Shift + Alt + C
		}

		private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
		{
			if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
			{
				ScanButton_Click(null, null); // Trigger your scan
				handled = true;
			}
			return IntPtr.Zero;
		}

		protected override void OnClosed(EventArgs e)
		{
			var helper = new System.Windows.Interop.WindowInteropHelper(this);
			UnregisterHotKey(helper.Handle, HOTKEY_ID);
			base.OnClosed(e);
		}

		private static void AppendToLogFile(string message)
		{
			string logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log.txt");
			File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
		}

	}
}