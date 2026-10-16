using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows.Input;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UyghurEditPP.Tests
{
	[TestClass]
	public class TahminQaideTests
	{
		// The same word finder MainForm builds for Latin script; the tests use made-up strings, not Uyghur words.
		static readonly Regex gLatin = new Regex(string.Format("[{0}]+([-]?[{1}]+)*", Uyghur.ULYHerpler, Uyghur.ULYHerpler));

		static SozTahmini Create(params object[] sozTekrar)
		{
			List<KeyValuePair<string, long>> list = new List<KeyValuePair<string, long>>();
			for(int i = 0; i < sozTekrar.Length; i += 2){
				list.Add(new KeyValuePair<string, long>((string)sozTekrar[i], (int)sozTekrar[i + 1]));
			}
			return new SozTahmini(list);
		}

		[TestMethod]
		public void SozBashi_WordEndingAtCaret()
		{
			Assert.AreEqual(3, TahminQaide.SozBashi("xy ab", 5, gLatin));
			Assert.AreEqual(0, TahminQaide.SozBashi("ab", 2, gLatin));
			Assert.AreEqual(0, TahminQaide.SozBashi("ab cd", 2, gLatin));
			Assert.AreEqual(0, TahminQaide.SozBashi("ab-cd", 5, gLatin));
		}

		[TestMethod]
		public void SozBashi_NoWordEndingAtCaret()
		{
			Assert.AreEqual(-1, TahminQaide.SozBashi("abc", 2, gLatin), "the caret is inside a word");
			Assert.AreEqual(-1, TahminQaide.SozBashi("ab ", 3, gLatin));
			Assert.AreEqual(-1, TahminQaide.SozBashi("ab-", 3, gLatin));
			Assert.AreEqual(-1, TahminQaide.SozBashi("ab 12", 5, gLatin));
			Assert.AreEqual(-1, TahminQaide.SozBashi("", 0, gLatin));
		}

		[TestMethod]
		public void Namzatlar_MostFrequentWithoutTheTypedWord()
		{
			SozTahmini t = Create("ab", 90, "abc", 50, "abd", 70, "acc", 99);

			CollectionAssert.AreEqual(new[]{ "abd", "abc" }, TahminQaide.Namzatlar(t, null, "ab", 5));
		}

		[TestMethod]
		public void Namzatlar_AtMostN()
		{
			SozTahmini t = Create("aba", 1, "abb", 2, "abc", 3, "abd", 4);

			CollectionAssert.AreEqual(new[]{ "abd", "abc" }, TahminQaide.Namzatlar(t, null, "ab", 2));
		}

		[TestMethod]
		public void Namzatlar_CapitalLikeTheTypedWord()
		{
			SozTahmini t = Create("abc", 5, "abd", 3);

			CollectionAssert.AreEqual(new[]{ "Abc", "Abd" }, TahminQaide.Namzatlar(t, null, "Ab", 5));
		}

		static TahminHerket Kunupka(Key key, int tallanghan, out int netije, ModifierKeys mods = ModifierKeys.None)
		{
			return TahminQaide.Kunupka(key, mods, tallanghan, 3, out netije);
		}

		[TestMethod]
		public void Kunupka_DigitChoosesThatCandidate()
		{
			int netije;
			Assert.AreEqual(TahminHerket.Choose, Kunupka(Key.D2, -1, out netije));
			Assert.AreEqual(1, netije);
			Assert.AreEqual(TahminHerket.Choose, Kunupka(Key.NumPad1, 2, out netije));
			Assert.AreEqual(0, netije);
			Assert.AreEqual(TahminHerket.Pass, Kunupka(Key.D4, -1, out netije), "only 3 candidates");
			Assert.AreEqual(TahminHerket.Pass, Kunupka(Key.D6, -1, out netije));
			Assert.AreEqual(TahminHerket.Pass, Kunupka(Key.D1, -1, out netije, ModifierKeys.Shift));
		}

		[TestMethod]
		public void Kunupka_ArrowsMoveTheSelection()
		{
			int netije;
			Assert.AreEqual(TahminHerket.Move, Kunupka(Key.Down, -1, out netije));
			Assert.AreEqual(0, netije);
			Assert.AreEqual(TahminHerket.Move, Kunupka(Key.Down, 2, out netije));
			Assert.AreEqual(2, netije, "stays on the last");
			Assert.AreEqual(TahminHerket.Move, Kunupka(Key.Up, 0, out netije));
			Assert.AreEqual(-1, netije, "back to nothing selected");
			Assert.AreEqual(TahminHerket.CloseAndPass, Kunupka(Key.Up, -1, out netije), "the caret goes up");
		}

		[TestMethod]
		public void Kunupka_EnterAndTab()
		{
			int netije;
			Assert.AreEqual(TahminHerket.Choose, Kunupka(Key.Enter, 1, out netije));
			Assert.AreEqual(1, netije);
			Assert.AreEqual(TahminHerket.CloseAndPass, Kunupka(Key.Enter, -1, out netije), "a new line");
			Assert.AreEqual(TahminHerket.Choose, Kunupka(Key.Tab, 2, out netije));
			Assert.AreEqual(2, netije);
			Assert.AreEqual(TahminHerket.Choose, Kunupka(Key.Tab, -1, out netije));
			Assert.AreEqual(0, netije, "nothing selected: the first");
			Assert.AreEqual(TahminHerket.Pass, Kunupka(Key.Tab, -1, out netije, ModifierKeys.Shift));
		}

		[TestMethod]
		public void Kunupka_EscapeClosesOtherKeysPass()
		{
			int netije;
			Assert.AreEqual(TahminHerket.Close, Kunupka(Key.Escape, 0, out netije));
			Assert.AreEqual(TahminHerket.Pass, Kunupka(Key.A, 0, out netije));
			Assert.AreEqual(TahminHerket.Pass, Kunupka(Key.Back, 0, out netije));
			Assert.AreEqual(TahminHerket.Pass, Kunupka(Key.Down, 0, out netije, ModifierKeys.Control));
		}
	}
}
