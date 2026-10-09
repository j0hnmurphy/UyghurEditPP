using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UyghurEditPP.Tests
{
	[TestClass]
	public class LanguageTests
	{
		[TestMethod]
		public void NewMessage_ShowsTheUyghurText()
		{
			Language lang = new Language();
			string key = "An unexpected error occurred.";

			lang.LanguaID = "uly";
			Assert.AreEqual("Kütülmigen xataliq körüldi.", lang.GetText(key));

			lang.LanguaID = "uey";
			Assert.AreEqual(Uyghur.ULY2UEY("Kütülmigen xataliq körüldi.").Replace("🠊", "🠈"), lang.GetText(key));

			lang.LanguaID = "eng";
			Assert.AreEqual(key, lang.GetText(key));
		}

		// Latin product names and file types stay in Latin letters in UEY and USY.
		[TestMethod]
		public void LatinParts_StayLatinInEveryScript()
		{
			const string LRI = "\u200E", PDI = "\u200E"; // a LEFT-TO-RIGHT MARK at both ends
			string uly = "{Microsoft Word} ambiri kem bolghachqa ({.docx}). {Windows} ni ornitip béqing:";

			Assert.AreEqual("Microsoft Word ambiri kem bolghachqa (.docx). Windows ni ornitip béqing:", Language.Yeziqla(uly, "uly"));
			Assert.AreEqual(
				LRI + "Microsoft Word" + PDI + Uyghur.ULY2UEY(" ambiri kem bolghachqa (") + LRI + ".docx" + PDI + Uyghur.ULY2UEY("). ")
				+ LRI + "Windows" + PDI + Uyghur.ULY2UEY(" ni ornitip béqing:"),
				Language.Yeziqla(uly, "uey"));
			Assert.AreEqual(
				"Microsoft Word" + Uyghur.ULY2USY(" ambiri kem bolghachqa (") + ".docx" + Uyghur.ULY2USY("). ")
				+ "Windows" + Uyghur.ULY2USY(" ni ornitip béqing:"),
				Language.Yeziqla(uly, "usy"));
		}

		[TestMethod]
		public void FollowWindows_KeepsWindowsInLatin()
		{
			Language lang = new Language();
			string key = "Follow Windows";

			lang.LanguaID = "uly";
			Assert.AreEqual("Windows bilen birdek", lang.GetText(key));
			lang.LanguaID = "uey";
			StringAssert.StartsWith(lang.GetText(key), "\u200EWindows\u200E");
			lang.LanguaID = "usy";
			StringAssert.StartsWith(lang.GetText(key), "Windows");
		}

		// Texts without {...} are converted exactly as before.
		[TestMethod]
		public void TextWithoutBraces_IsConvertedAsBefore()
		{
			string uly = "Saqlanmighan özgirishler tashliwétilidu. Dawamlashturamsiz?";

			Assert.AreEqual(Uyghur.ULY2UEY(uly).Replace("🠊", "🠈"), Language.Yeziqla(uly, "uey"));
			Assert.AreEqual(Uyghur.ULY2USY(uly), Language.Yeziqla(uly, "usy"));
			Assert.AreEqual(uly, Language.Yeziqla(uly, "uly"));
		}

		// Paths and English error texts in a message: each line is isolated as left-to-right
		// in the UEY UI, and left alone otherwise.
		[TestMethod]
		public void LeftToRight_IsolatesEachLineOnlyInUey()
		{
			string old = MainForm.gLang.LanguaID;
			try{
				MainForm.gLang.LanguaID = "uey";
				Assert.AreEqual("\u200EE:\\a\\b\u200E\r\n\r\n\u200EFailed.\u200E", CenteredMessageBox.LeftToRight("E:\\a\\b\r\n\r\nFailed."));
				Assert.IsTrue(CenteredMessageBox.RightToLeftUi);

				MainForm.gLang.LanguaID = "uly";
				Assert.AreEqual("E:\\a\\b", CenteredMessageBox.LeftToRight("E:\\a\\b"));
				Assert.IsFalse(CenteredMessageBox.RightToLeftUi);
			}
			finally{
				MainForm.gLang.LanguaID = old;
			}
		}

		[TestMethod]
		public void ExistingMessage_IsUnchanged()
		{
			Language lang = new Language();
			lang.LanguaID = "uly";

			Assert.AreEqual("Höjjetning mezmunida özgirish boldi. Saqlamsiz?", lang.GetText("Höjjetning mezmunida özgirish boldi. Saqlamsiz?"));
		}
	}
}
