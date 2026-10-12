using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UyghurEditPP.Tests
{
	[TestClass]
	public class KenjiToolStripTests
	{
		static readonly int[] Steps = { 120, 144, 192, 96, 192, 120, 168, 144, 96, 120, 144, 192, 96, 192, 120, 168, 144, 96 };

		static int Round(double v)
		{
			return (int)Math.Round(v, MidpointRounding.AwayFromZero);
		}

		static Bitmap Icon16()
		{
			Bitmap b = new Bitmap(16, 16);
			using(Graphics g = Graphics.FromImage(b)){
				g.Clear(Color.Magenta);
				g.FillRectangle(Brushes.Blue, 4, 4, 8, 8);
			}
			return b;
		}

		// Like the main toolbar: ImageScalingSize 24, buttons of a fixed size, images drawn at
		// their own size, and split buttons.
		[TestMethod]
		public void Toolbar_FollowsTheDpi_WithoutDrift()
		{
			using(Bitmap icon = Icon16())
			using(KenjiToolStrip strip = new KenjiToolStrip())
			using(ToolStripButton button = new ToolStripButton())
			using(ToolStripSplitButton split = new ToolStripSplitButton()){
				strip.ImageScalingSize = new Size(24, 24);
				button.AutoSize = false;
				button.Size = new Size(40, 29);
				button.Image = icon;
				button.ImageScaling = ToolStripItemImageScaling.None;
				button.ImageTransparentColor = Color.Magenta;
				split.AutoSize = false;
				split.Size = new Size(40, 29);
				split.DropDownButtonWidth = ToolStripScaler.DropDownButtonWidth;
				strip.Items.Add(button);
				strip.Items.Add(split);
				int dpi = 96;
				foreach(int next in Steps){
					strip.Scale(new SizeF((float)next / dpi, (float)next / dpi));
					dpi = next;
					string at = "at " + dpi;
					double k = dpi / 96.0;
					Assert.AreEqual(new Size(Round(24 * k), Round(24 * k)), strip.ImageScalingSize, at);
					Assert.AreEqual(new Size(Round(40 * k), Round(29 * k)), button.Size, at);
					Assert.AreEqual(new Size(Round(16 * k), Round(16 * k)), button.Image.Size, at);
					Assert.AreEqual(new Size(Round(40 * k), Round(29 * k)), split.Size, at);
					Assert.AreEqual(Round(11 * k), split.DropDownButtonWidth, at);
				}
				Assert.AreSame(icon, button.Image, "back at 100% the original image is used");
			}
		}

		// One scaling may come as two ScaleControl calls for different bounds (see KenjiTab).
		[TestMethod]
		public void OneScaling_InTwoCalls_ScalesEachDimensionOnce()
		{
			using(KenjiToolStrip strip = new KenjiToolStrip())
			using(ToolStripButton button = new ToolStripButton()){
				button.AutoSize = false;
				button.Size = new Size(40, 29);
				strip.Items.Add(button);
				strip.Scaler.Scale(new SizeF(1.5f, 1.5f), BoundsSpecified.Location);
				Assert.AreEqual(new Size(40, 29), button.Size);
				strip.Scaler.Scale(new SizeF(1.5f, 1.5f), BoundsSpecified.Height);
				strip.Scaler.Scale(new SizeF(1.5f, 1.5f), BoundsSpecified.Location | BoundsSpecified.Width);
				Assert.AreEqual(new Size(60, 44), button.Size);
			}
		}

		// The transparent color must not bleed into the scaled image.
		[TestMethod]
		public void ScaleImage_KeepsTheTransparentColorTransparent()
		{
			using(Bitmap icon = Icon16())
			using(Bitmap scaled = ToolStripScaler.ScaleImage(icon, new Size(24, 24), Color.Magenta)){
				Assert.AreEqual(new Size(24, 24), scaled.Size);
				Assert.AreEqual(0, scaled.GetPixel(0, 0).A, "corner transparent");
				Color center = scaled.GetPixel(12, 12);
				Assert.AreEqual(255, center.A);
				Assert.AreEqual(Color.Blue.ToArgb(), center.ToArgb());
				for(int y = 0; y < 24; y++){
					for(int x = 0; x < 24; x++){
						Color c = scaled.GetPixel(x, y);
						Assert.IsFalse(c.A > 0 && c.R > 128 && c.B > 128 && c.G < 64, "magenta at " + x + "," + y);
					}
				}
			}
		}
	}
}
