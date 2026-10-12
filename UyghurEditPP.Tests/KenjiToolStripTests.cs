using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UyghurEditPP.Tests
{
	[TestClass]
	[DoNotParallelize]   // one test switches Windows Forms' process-wide DPI flags for a while
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

		[System.Runtime.InteropServices.DllImport("user32.dll")]
		static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);

		class TestStrip : KenjiToolStrip
		{
			public void Rescale(int oldDpi, int newDpi)
			{
				RescaleConstantsForDpi(oldDpi, newDpi);
			}
		}

		// On a real DPI change the strip first gets RescaleConstantsForDpi (WM_DPICHANGED_BEFOREPARENT),
		// whose ResetScaling sets ImageScalingSize to 16 px at the new DPI, and then the form
		// scales it (ScaleControl). The 100% size must survive that, round trip after round trip.
		[TestMethod]
		public void DpiChange_ThroughRescaleConstants_KeepsTheImageSize()
		{
			// The test host is not Per-Monitor V2 (the program is, by app.config), so Windows Forms
			// would skip ResetScaling; switch on the same flags the program runs with.
			Type dpiHelper = typeof(Form).Assembly.GetType("System.Windows.Forms.DpiHelper");
			Assert.IsNotNull(dpiHelper.GetProperty("EnableToolStripPerMonitorV2HighDpiImprovements", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public).GetValue(null, null));   // initializes the flags
			string[] flags = { "enableDpiChangedMessageHandling", "enableToolStripHighDpiImprovements", "enableDpiChangedHighDpiImprovements" };
			object[] saved = new object[flags.Length];
			for(int i = 0; i < flags.Length; i++){
				FieldInfo f = dpiHelper.GetField(flags[i], BindingFlags.Static | BindingFlags.NonPublic);
				saved[i] = f.GetValue(null);
				f.SetValue(null, true);
			}
			try{
				// ... and only on a Per-Monitor V2 thread (DpiHelper.EnableDpiChangedMessageHandling).
				Exception error = null;
				System.Threading.Thread t = new System.Threading.Thread(() => {
					try{
						Assert.AreNotEqual(IntPtr.Zero, SetThreadDpiAwarenessContext(new IntPtr(-4)), "PMv2 thread");
						RescaleConstantsThenScale();
					}
					catch(Exception e){
						error = e;
					}
				});
				t.SetApartmentState(System.Threading.ApartmentState.STA);
				t.Start();
				t.Join();
				if(error != null){
					throw new AssertFailedException(error.Message, error);
				}
			}
			finally{
				for(int i = 0; i < flags.Length; i++){
					dpiHelper.GetField(flags[i], BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, saved[i]);
				}
			}
		}

		static void RescaleConstantsThenScale()
		{
			// What the framework does to a plain ToolStrip: 16 px at the new DPI.
			using(ToolStrip plain = new ToolStrip()){
				plain.ImageScalingSize = new Size(24, 24);
				typeof(ToolStrip).GetMethod("RescaleConstantsForDpi", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(plain, new object[] { 96, 120 });
				Assert.AreEqual(new Size(20, 20), plain.ImageScalingSize, "ResetScaling did not run: the test would not show the problem");
			}
			using(TestStrip strip = new TestStrip())
			using(ToolStripSplitButton split = new ToolStripSplitButton()){
				strip.ImageScalingSize = new Size(24, 24);
				split.AutoSize = false;
				split.Size = new Size(40, 29);
				strip.Items.Add(split);
				int dpi = 96;
				foreach(int next in Steps){
					strip.Rescale(dpi, next);
					strip.Scale(new SizeF((float)next / dpi, (float)next / dpi));
					dpi = next;
					Assert.AreEqual(new Size(Round(24 * dpi / 96.0), Round(24 * dpi / 96.0)), strip.ImageScalingSize, "at " + dpi);
					Assert.AreEqual(Round(11 * dpi / 96.0), split.DropDownButtonWidth, "at " + dpi);
				}
				Assert.AreEqual(new Size(24, 24), strip.ImageScalingSize);
			}
		}

		// A button added after scaling is given at 100%, like the designer's ones.
		[TestMethod]
		public void ItemAddedAfterScaling_IsScaledFrom100Percent()
		{
			using(Bitmap icon = Icon16())
			using(KenjiToolStrip strip = new KenjiToolStrip())
			using(ToolStripButton button = new ToolStripButton()){
				strip.Scale(new SizeF(1.5f, 1.5f));
				button.AutoSize = false;
				button.Size = new Size(40, 29);
				button.Image = icon;
				button.ImageScaling = ToolStripItemImageScaling.None;
				strip.Items.Add(button);
				Assert.AreEqual(new Size(60, 44), button.Size);
				Assert.AreEqual(new Size(24, 24), button.Image.Size);
				strip.Scale(new SizeF(1f / 1.5f, 1f / 1.5f));
				Assert.AreEqual(new Size(40, 29), button.Size);
				Assert.AreSame(icon, button.Image);
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
