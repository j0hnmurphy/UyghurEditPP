using System;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UyghurEditPP.FindReplace;

namespace UyghurEditPP.Tests
{
	[TestClass]
	public class FindPatternTests
	{
		static Regex Normal(string text, bool whole = false, bool caseSensitive = false, bool up = false)
		{
			return FindPattern.Build(text, FindMode.Normal, whole, caseSensitive, up);
		}

		[TestMethod]
		public void Normal_TextIsTakenAsTyped()
		{
			Assert.IsTrue(Normal("a.b(c").IsMatch("xx a.b(c yy"));
			Assert.IsFalse(Normal("a.b").IsMatch("axb"));
		}

		[TestMethod]
		public void CaseIsIgnoredUnlessAsked_AndIIsNotTurkish()
		{
			Assert.IsTrue(Normal("uyghur").IsMatch("UYGHUR"));
			Assert.IsFalse(Normal("uyghur", caseSensitive: true).IsMatch("UYGHUR"));
			Assert.IsTrue(Normal("iii").IsMatch("III"));
		}

		[TestMethod]
		public void WholeWord_DoesNotMatchInsideAWord()
		{
			Assert.IsTrue(Normal("tili", whole: true).IsMatch("bu tili bar"));
			Assert.IsFalse(Normal("til", whole: true).IsMatch("bu tili bar"));
		}

		[TestMethod]
		public void WholeWord_WorksWithUyghurArabicLetters()
		{
			Regex r = Normal("ئۇيغۇر", whole: true);

			Assert.IsTrue(r.IsMatch("بۇ ئۇيغۇر تىلى"));
			Assert.IsTrue(r.IsMatch("ئۇيغۇر"));
			Assert.IsFalse(r.IsMatch("بۇ ئۇيغۇرچە تىلى"));
			Assert.IsFalse(r.IsMatch("بۇئۇيغۇر"));
		}

		[TestMethod]
		public void WholeWord_WorksWithLatinLettersWithAccents()
		{
			Assert.IsTrue(Normal("ékin", whole: true).IsMatch("bu ékin bar"));
			Assert.IsFalse(Normal("ékin", whole: true).IsMatch("bu ékinzar bar"));
		}

		[TestMethod]
		public void SearchUp_SetsRightToLeft()
		{
			Assert.IsTrue(Normal("a", up: true).Options.HasFlag(RegexOptions.RightToLeft));
			Assert.IsFalse(Normal("a").Options.HasFlag(RegexOptions.RightToLeft));
		}

		[TestMethod]
		public void RegularExpression_IsUsedAsTyped()
		{
			Regex r = FindPattern.Build("a+b", FindMode.RegularExpression, false, false, false);
			Assert.AreEqual("aaab", r.Match("xaaab").Value);
		}

		[TestMethod]
		public void Wildcards_StarAndQuestionMark()
		{
			Regex r = FindPattern.Build("a\\*c", FindMode.Wildcards, false, false, false);
			Assert.AreEqual("abbbc", r.Match("xabbbc").Value);
			r = FindPattern.Build("a\\?c", FindMode.Wildcards, false, false, false);
			Assert.AreEqual("abc", r.Match("xabc").Value);
			r = FindPattern.Build("a\\tb", FindMode.Wildcards, false, false, false);
			Assert.IsTrue(r.IsMatch("a\tb"));
		}

		[TestMethod]
		public void InvalidRegularExpression_IsAnArgumentException()
		{
			foreach(string bad in new[] { "(", "[a-", "*a", "a{2,1}", "\\" }){
				bool thrown = false;
				try{
					FindPattern.Build(bad, FindMode.RegularExpression, false, false, false);
				}
				catch(ArgumentException){
					thrown = true;
				}
				Assert.IsTrue(thrown, bad);
			}
		}

		[TestMethod]
		public void NormalMode_NeverThrowsForSpecialCharacters()
		{
			foreach(string s in new[] { "(", "[a-", "*a", "\\", "a{2,1}" }){
				Assert.IsTrue(Normal(s).IsMatch("x" + s + "y"), s);
			}
		}

		[TestMethod]
		public void CatastrophicPattern_TimesOut()
		{
			Regex r = FindPattern.Build("(a+)+$", FindMode.RegularExpression, false, false, false, TimeSpan.FromMilliseconds(200));
			string input = new string('a', 40) + "b";

			Assert.ThrowsException<RegexMatchTimeoutException>(() => r.Match(input));
		}

		[TestMethod]
		public void DefaultTimeout_IsSet()
		{
			Assert.AreEqual(FindPattern.Timeout, FindPattern.Build("a", FindMode.Normal, false, false, false).MatchTimeout);
		}
	}
}
