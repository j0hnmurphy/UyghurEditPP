using System;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UyghurEditPP.Tests
{
	[TestClass]
	public class MainFormLayoutTests
	{
		// On a DPI change (WM_DPICHANGED, Per-Monitor V2) Windows Forms first resizes the window
		// to the new size and then scales every child control by new DPI / old DPI. The tab
		// control and the status bar must still fill the window afterwards; when they were sized
		// by hand in the Resize event, they ended up scaled twice (e.g. 80% wide after 125% -> 100%).
		[TestMethod]
		public void TabsAndStatusBar_FillTheWindow_AfterDpiScaling()
		{
			foreach(float factor in new[] { 0.8f, 1.25f, 1.5f }){
				RunOnSta(() => {
					using(MainForm form = new MainForm()){
						form.SuspendLayout();
						form.ClientSize = new Size(1000, 700);
						foreach(Control c in form.Controls){
							c.Scale(new SizeF(factor, factor));
						}
						form.ResumeLayout(true);

						Control menuBar = Field(form, "menuBar"), toolBar = Field(form, "toolBar");
						Control mainTab = Field(form, "mainTab"), stBar = Field(form, "stBar");
						string at = "factor " + factor;
						Assert.AreEqual(menuBar.Bottom, toolBar.Top, at);
						Assert.AreEqual(new Rectangle(0, toolBar.Bottom, form.ClientSize.Width, stBar.Top - toolBar.Bottom), mainTab.Bounds, at);
						Assert.AreEqual(new Rectangle(0, form.ClientSize.Height - stBar.Height, form.ClientSize.Width, stBar.Height), stBar.Bounds, at);
					}
				});
			}
		}

		static Control Field(MainForm form, string name)
		{
			return (Control)typeof(MainForm).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(form);
		}

		// MainForm hosts WPF objects, which need an STA thread.
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
