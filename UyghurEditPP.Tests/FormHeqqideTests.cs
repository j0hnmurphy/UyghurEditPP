using System.Drawing;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UyghurEditPP.Tests
{
	[TestClass]
	public class FormHeqqideTests
	{
		// The boxes of the links are scaled with the DPI, but text does not grow exactly in
		// proportion to the font: a box that only just fits at 100% cut the address at 150%.
		[TestMethod]
		public void Links_FitTheirBoxes_AtHighDpi()
		{
			AppFonts.Load();
			using(FormHeqqide form = new FormHeqqide()){
				AppFonts.Fix(form);
				foreach(Control c in form.Controls){
					LinkLabel link = c as LinkLabel;
					if(link == null){
						continue;
					}
					foreach(float factor in new[] { 1f, 1.25f, 1.5f, 1.75f, 2f }){
						using(Font font = new Font(link.Font.FontFamily, link.Font.Size * factor, link.Font.Style)){
							int text = TextRenderer.MeasureText(link.Text, font).Width;
							Assert.IsTrue(text <= link.Width * factor, link.Name + " at " + factor + ": text " + text + " > box " + link.Width * factor);
						}
					}
				}
			}
		}
	}
}
