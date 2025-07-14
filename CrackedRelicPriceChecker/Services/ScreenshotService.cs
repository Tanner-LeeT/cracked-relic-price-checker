using System.Drawing;

public static class ScreenshotService
{
	public static Bitmap CaptureRegion(Rectangle region)
	{
		Bitmap bmp = new Bitmap(region.Width, region.Height);
		using Graphics g = Graphics.FromImage(bmp);
		g.CopyFromScreen(region.Location, Point.Empty, region.Size);
		return bmp;
	}
}