using System.Linq;
using System.Media;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UyghurEditPP.Tests
{
	[TestClass]
	public class ThemedMessageBoxTests
	{
		static string Keys(MessageBoxButtons buttons)
		{
			return string.Join(",", ThemedMessageBox.Buttons(buttons).Select(b => b.Key + "=" + b.Result));
		}

		[TestMethod]
		public void Buttons_ComeInTheOrderAndWithTheResultsOfTheSystemBox()
		{
			Assert.AreEqual("OK=OK", Keys(MessageBoxButtons.OK));
			Assert.AreEqual("OK=OK,Cancel=Cancel", Keys(MessageBoxButtons.OKCancel));
			Assert.AreEqual("Yes=Yes,No=No", Keys(MessageBoxButtons.YesNo));
			Assert.AreEqual("Yes=Yes,No=No,Cancel=Cancel", Keys(MessageBoxButtons.YesNoCancel));
			Assert.AreEqual("Retry=Retry,Cancel=Cancel", Keys(MessageBoxButtons.RetryCancel));
		}

		// Not used by the program; CenteredMessageBox shows the system box for it.
		[TestMethod]
		public void Buttons_AbortRetryIgnore_IsNotDrawn()
		{
			Assert.IsNull(ThemedMessageBox.Buttons(MessageBoxButtons.AbortRetryIgnore));
		}

		[TestMethod]
		public void EscapeResult_IsCancelOrOkAndNothingForYesNo()
		{
			Assert.AreEqual(DialogResult.OK, ThemedMessageBox.EscapeResult(MessageBoxButtons.OK));
			Assert.AreEqual(DialogResult.Cancel, ThemedMessageBox.EscapeResult(MessageBoxButtons.OKCancel));
			Assert.AreEqual(DialogResult.Cancel, ThemedMessageBox.EscapeResult(MessageBoxButtons.YesNoCancel));
			Assert.AreEqual(DialogResult.Cancel, ThemedMessageBox.EscapeResult(MessageBoxButtons.RetryCancel));
			Assert.AreEqual(DialogResult.None, ThemedMessageBox.EscapeResult(MessageBoxButtons.YesNo));
		}

		[TestMethod]
		public void FitWidth_StaysBetweenMinAndMax()
		{
			Assert.AreEqual(200, ThemedMessageBox.FitWidth(50, 120, 200, 520));   // short text
			Assert.AreEqual(300, ThemedMessageBox.FitWidth(300, 120, 200, 520));
			Assert.AreEqual(350, ThemedMessageBox.FitWidth(100, 350, 200, 520));  // wide row of buttons
			Assert.AreEqual(520, ThemedMessageBox.FitWidth(900, 120, 200, 520));  // long text wraps
		}

		// In the UEY UI the icon goes to the right edge and the buttons to the left.
		[TestMethod]
		public void MirrorX_MirrorsOnlyRightToLeft()
		{
			Assert.AreEqual(16, ThemedMessageBox.MirrorX(16, 32, 400, false));
			Assert.AreEqual(352, ThemedMessageBox.MirrorX(16, 32, 400, true));
			Assert.AreEqual(12, ThemedMessageBox.MirrorX(300, 88, 400, true));
		}

		[TestMethod]
		public void IconBitmap_HasTheAskedSize_AndNoneWithoutIcon()
		{
			foreach(int size in new[] { 32, 40, 48, 64 }){
				using(System.Drawing.Bitmap b = ThemedMessageBox.IconBitmap(MessageBoxIcon.Error, size)){
					Assert.AreEqual(size, b.Width);
					Assert.AreEqual(size, b.Height);
				}
			}
			Assert.IsNull(ThemedMessageBox.IconBitmap(MessageBoxIcon.None, 32));
		}

		// At 32 pixels the icons are the system message box icons (SystemIcons).
		[TestMethod]
		public void IconBitmap_At32_IsTheSystemIcon()
		{
			Check(MessageBoxIcon.Error, System.Drawing.SystemIcons.Error);
			Check(MessageBoxIcon.Warning, System.Drawing.SystemIcons.Warning);
			Check(MessageBoxIcon.Question, System.Drawing.SystemIcons.Question);
			Check(MessageBoxIcon.Information, System.Drawing.SystemIcons.Information);
		}

		static void Check(MessageBoxIcon icon, System.Drawing.Icon expected)
		{
			using(System.Drawing.Bitmap a = ThemedMessageBox.IconBitmap(icon, 32))
			using(System.Drawing.Bitmap b = expected.ToBitmap()){
				for(int y = 0; y < 32; y++){
					for(int x = 0; x < 32; x++){
						Assert.AreEqual(b.GetPixel(x, y), a.GetPixel(x, y), icon + " at " + x + "," + y);
					}
				}
			}
		}

		[TestMethod]
		public void Wrap_GivesTheHandle_AndNullForNone()
		{
			Assert.IsNull(CenteredMessageBox.Wrap(System.IntPtr.Zero));
			Assert.AreEqual(new System.IntPtr(1234), CenteredMessageBox.Wrap(new System.IntPtr(1234)).Handle);
		}

		[TestMethod]
		public void Sound_FollowsTheIcon()
		{
			Assert.AreSame(SystemSounds.Hand, ThemedMessageBox.Sound(MessageBoxIcon.Error));
			Assert.AreSame(SystemSounds.Exclamation, ThemedMessageBox.Sound(MessageBoxIcon.Warning));
			Assert.AreSame(SystemSounds.Question, ThemedMessageBox.Sound(MessageBoxIcon.Question));
			Assert.AreSame(SystemSounds.Asterisk, ThemedMessageBox.Sound(MessageBoxIcon.Information));
			Assert.IsNull(ThemedMessageBox.Sound(MessageBoxIcon.None));
		}

		static string Text(string languaID, string key)
		{
			Language lang = new Language();
			lang.LanguaID = languaID;
			return lang.GetText(key);
		}

		[TestMethod]
		public void ButtonTexts_EnglishAndJapanese_HaveAccessKeys()
		{
			Assert.AreEqual("&Yes", Text("eng", "Yes"));
			Assert.AreEqual("&No", Text("eng", "No"));
			Assert.AreEqual("OK", Text("eng", "OK"));
			Assert.AreEqual("Cancel", Text("eng", "Cancel"));
			Assert.AreEqual("はい(&Y)", Text("jpn", "Yes"));
			Assert.AreEqual("いいえ(&N)", Text("jpn", "No"));
			Assert.AreEqual("キャンセル", Text("jpn", "Cancel"));
		}

		// The ULY drafts give in UEY the spellings of the Uyghur (Arabic script) MediaWiki and
		// OOUI interface texts: confirmable-yes/no, cancel (languages/i18n/ug-arab.json) and
		// ooui-dialog-message-accept, ooui-dialog-process-retry (oojs-ui i18n/ug-arab.json).
		[TestMethod]
		public void ButtonTexts_Uey_MatchTheMediaWikiSpellings()
		{
			Assert.AreEqual("ھەئە", Text("uey", "Yes"));
			Assert.AreEqual("ياق", Text("uey", "No"));
			Assert.AreEqual("ۋاز كەچ", Text("uey", "Cancel"));
			Assert.AreEqual("تامام", Text("uey", "OK"));
			Assert.AreEqual("قايتا سىنا", Text("uey", "Retry"));
		}
	}
}
