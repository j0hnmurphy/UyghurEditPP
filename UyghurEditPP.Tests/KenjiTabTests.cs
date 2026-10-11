using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UyghurEditPP.Tests
{
	[TestClass]
	public class KenjiTabTests
	{
		// 96 -> 120 -> 144 -> 192 -> 96 -> 192 -> 120 -> 168 -> 144 -> 96, twice
		static readonly int[] Steps = { 120, 144, 192, 96, 192, 120, 168, 144, 96, 120, 144, 192, 96, 192, 120, 168, 144, 96 };

		// Windows Forms scales controls by ScaleControl, at startup (AutoScaleMode.Dpi) and on a
		// DPI change (new DPI / old DPI); the tab height and padding must follow.
		[TestMethod]
		public void TabSize_FollowsTheDpi_WithoutDrift()
		{
			using(KenjiTab tab = new KenjiTab()){
				tab.ItemSize = new Size(0, 40);
				tab.Padding = new Point(20, 0);
				int dpi = 96;
				foreach(int next in Steps){
					tab.Scale(new SizeF((float)next / dpi, (float)next / dpi));
					dpi = next;
					string at = "at " + dpi;
					Assert.AreEqual(Round(40 * dpi / 96.0), tab.ItemSize.Height, at);
					Assert.AreEqual(Round(20 * dpi / 96.0), tab.ScaledPadding.X, at);
					Assert.AreEqual(new Point(20, 0), tab.Padding, at);   // stays at 100%
				}
				Assert.AreEqual(new Size(0, 40), tab.ItemSize);
				Assert.AreEqual(new Point(20, 0), tab.ScaledPadding);
			}
		}

		// On a DPI change the tabs get wider with the padding, without recreating the handle
		// (which would recreate the editors in the tabs).
		[TestMethod]
		public void DpiChange_WidensTheTabs_WithoutRecreatingTheHandle()
		{
			RunOnSta(() => {
				using(Form form = new Form())
				using(KenjiTab tab = new KenjiTab()){
					tab.ItemSize = new Size(0, 40);
					tab.Padding = new Point(20, 0);
					tab.TabPages.Add(new TabPage("Namsiz_1.txt"));
					form.Controls.Add(tab);
					form.Show();
					IntPtr handle = tab.Handle;
					int width96 = tab.GetTabRect(0).Width;

					tab.Scale(new SizeF(1.5f, 1.5f));
					Assert.AreEqual(handle, tab.Handle, "handle recreated");
					Assert.AreEqual(60, tab.GetTabRect(0).Height);
					Assert.IsTrue(tab.GetTabRect(0).Width >= width96 + 2 * 10, "padding not applied: " + tab.GetTabRect(0).Width + " vs " + width96);

					tab.Scale(new SizeF(96f / 144, 96f / 144));
					Assert.AreEqual(40, tab.GetTabRect(0).Height);
					Assert.AreEqual(width96, tab.GetTabRect(0).Width);
				}
			});
		}

		static int Round(double v)
		{
			return (int)Math.Round(v, MidpointRounding.AwayFromZero);
		}

		static void RunOnSta(Action action)
		{
			Exception error = null;
			Thread t = new Thread(() => {
				try{
					action();
				}
				catch(Exception e){
					error = e;
				}
			});
			t.SetApartmentState(ApartmentState.STA);
			t.Start();
			t.Join();
			if(error != null){
				throw new AssertFailedException(error.Message, error);
			}
		}
	}
}
