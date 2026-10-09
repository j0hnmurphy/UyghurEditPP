using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UyghurEditPP.Tests
{
	// Uyghur.Detect picks the script with the most letters (F-15). Only letters count:
	// the backtick and Arabic punctuation or digits must not tip a mixed document, and UEY
	// and USY letters outside the first 128 code points of their blocks must be counted.
	[TestClass]
	public class DetectTests
	{
		// UEY letters above U+067F (see Uyghur.UEYHerpler): U+0686, U+06D5, U+06C7, U+06C6,
		// U+06C8, U+06CB, U+06D0, U+06AD, U+06AF, U+06BE.
		const string UeyHighLetters = "چەۇۆۈۋېڭگھ";

		[TestMethod]
		public void Backticks_AreNotLatinLetters()
		{
			string text = "باب ````` ab";
			Assert.AreEqual(Uyghur.YEZIQ.UEY, Uyghur.Detect(text));
		}

		[TestMethod]
		public void UeyLettersAboveU067F_AreCounted()
		{
			string text = UeyHighLetters + " abcde";
			Assert.AreEqual(Uyghur.YEZIQ.UEY, Uyghur.Detect(text));
		}

		[TestMethod]
		public void UlyVowelsWithMarks_AreCounted()
		{
			string text = "éöüÉÖ ab بببببب";
			Assert.AreEqual(Uyghur.YEZIQ.ULY, Uyghur.Detect(text));
		}

		[TestMethod]
		public void UsyLettersAboveU047F_AreCounted()
		{
			// U+04D9 and U+04E9 (see Uyghur.USYHerpler) plus one U+0430.
			string text = "әөәөәа abc";
			Assert.AreEqual(Uyghur.YEZIQ.USY, Uyghur.Detect(text));
		}

		[TestMethod]
		public void ArabicPunctuationAndDigits_AreNotUeyLetters()
		{
			// Arabic comma, Arabic question mark, Arabic-Indic digits one to four.
			string text = "،؟١٢٣٤ ab";
			Assert.AreEqual(Uyghur.YEZIQ.ULY, Uyghur.Detect(text));
		}

		[TestMethod]
		public void OnlyTheFirst5000Characters_AreSampled()
		{
			string text = new string('a', 5000) + new string('ب', 6000);
			Assert.AreEqual(Uyghur.YEZIQ.ULY, Uyghur.Detect(text));
		}

		[TestMethod]
		public void NoLetters_GiveYoq()
		{
			Assert.AreEqual(Uyghur.YEZIQ.YOQ, Uyghur.Detect("12 ` ! ،"));
			Assert.AreEqual(Uyghur.YEZIQ.YOQ, Uyghur.Detect(""));
		}
	}
}
