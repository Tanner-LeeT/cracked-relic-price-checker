using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace CrackedRelicPriceChecker
{
	/// <summary>
	/// Interaction logic for OverlayWindow.xaml
	/// </summary>
	public partial class OverlayWindow : Window
	{
		public OverlayWindow()
		{
			InitializeComponent();
			this.Left = 0;
			this.Top = 0;
			this.Width = SystemParameters.PrimaryScreenWidth;
			this.Height = SystemParameters.PrimaryScreenHeight;
		}

		public void ShowPrices(Dictionary<System.Drawing.Rectangle, string> regionToText)
		{
			var canvas = OverlayCanvas;
			canvas.Children.Clear();

			foreach (var kvp in regionToText)
			{
				var rect = kvp.Key;
				var label = kvp.Value;

				var text = new TextBlock
				{
					Text = label,
					FontSize = 18,
					FontWeight = FontWeights.Bold,
					Foreground = System.Windows.Media.Brushes.Yellow,
					Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(180, 0, 0, 0)), // semi-transparent black
					TextWrapping = TextWrapping.Wrap,
					MaxWidth = 280,
					Padding = new Thickness(4)
				};

				Canvas.SetLeft(text, rect.Left);
				Canvas.SetTop(text, rect.Top - 246); // shift above reward box
				canvas.Children.Add(text);

				StartAutoCloseTimer();
			}
		}

		private void StartAutoCloseTimer()
		{
			var timer = new DispatcherTimer
			{
				Interval = TimeSpan.FromSeconds(8)
			};
			timer.Tick += (s, e) =>
			{
				timer.Stop();
				this.Close();
			};
			timer.Start();
		}

	}
}
