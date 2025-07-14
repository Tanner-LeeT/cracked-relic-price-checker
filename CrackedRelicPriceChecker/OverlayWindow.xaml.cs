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
			OverlayCanvas.Children.Clear();

			foreach (var kvp in regionToText)
			{
				var rect = kvp.Key;
				var text = kvp.Value;

				var label = new TextBlock
				{
					Text = text,
					Foreground = System.Windows.Media.Brushes.Gold,
					FontSize = 16,
					FontWeight = FontWeights.Bold,
					Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(128, 0, 0, 0)),
					Padding = new Thickness(4)
				};

				Canvas.SetLeft(label, rect.Left + 10);
				Canvas.SetTop(label, rect.Top - 212); // slightly above the reward box
				OverlayCanvas.Children.Add(label);
			}

			StartAutoCloseTimer();
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
