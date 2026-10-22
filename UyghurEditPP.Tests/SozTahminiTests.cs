using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UyghurEditPP.Tests
{
	[TestClass]
	public class SozTahminiTests
	{
		// The logic tests use made-up Latin strings, not Uyghur words.
		static SozTahmini Create(params object[] sozTekrar)
		{
			List<KeyValuePair<string, long>> list = new List<KeyValuePair<string, long>>();
			for(int i = 0; i < sozTekrar.Length; i += 2){
				list.Add(new KeyValuePair<string, long>((string)sozTekrar[i], Convert.ToInt64(sozTekrar[i + 1])));
			}
			return new SozTahmini(list);
		}

		[TestMethod]
		public void Find_MostFrequentFirst()
		{
			SozTahmini t = Create("abx", 5, "aby", 50, "abz", 20, "acc", 99);

			CollectionAssert.AreEqual(new[]{ "aby", "abz", "abx" }, t.Find("ab", 5));
		}

		[TestMethod]
		public void Find_SameCount_ShorterThenOrdinal()
		{
			SozTahmini t = Create("abcd", 7, "abd", 7, "abc", 7);

			CollectionAssert.AreEqual(new[]{ "abc", "abd", "abcd" }, t.Find("ab", 5));
		}

		[TestMethod]
		public void Find_NoMatch_IsEmpty()
		{
			SozTahmini t = Create("abc", 1, "abd", 1);

			Assert.AreEqual(0, t.Find("x", 5).Count);
			Assert.AreEqual(0, t.Find("abcz", 5).Count);
			Assert.AreEqual(0, t.Find("", 5).Count);
		}

		[TestMethod]
		public void Find_ReturnsAtMostN()
		{
			SozTahmini t = Create("aa", 1, "ab", 2, "ac", 3, "ad", 4, "ae", 5, "af", 6);

			CollectionAssert.AreEqual(new[]{ "af", "ae", "ad" }, t.Find("a", 3));
		}

		[TestMethod]
		public void Find_WholeWordTyped_IsIncluded()
		{
			SozTahmini t = Create("ab", 1, "abc", 9);

			CollectionAssert.AreEqual(new[]{ "abc", "ab" }, t.Find("ab", 5));
		}

		// The spell checker's tree used to cut counts at 32767; the prediction keeps them.
		[TestMethod]
		public void Find_CountsAbove32767_KeepTheirOrder()
		{
			SozTahmini t = Create("abc", 40000, "abd", 65535, "abe", 32767);

			CollectionAssert.AreEqual(new[]{ "abd", "abc", "abe" }, t.Find("ab", 5));
		}

		[TestMethod]
		public void New_SameWordTwice_CountsAreAdded()
		{
			SozTahmini t = Create("abc", 3, "ABC", 4, "abd", 6);

			CollectionAssert.AreEqual(new[]{ "abc", "abd" }, t.Find("ab", 5));
			Assert.AreEqual(2, t.Count);
		}

		[TestMethod]
		public void Find_NullOrNegative_IsEmpty()
		{
			SozTahmini t = Create("abc", 1);

			Assert.AreEqual(0, t.Find(null, 5).Count);
			Assert.AreEqual(0, t.Find("ab", -3).Count);
		}

		[TestMethod]
		public void UyghurSpell_BeforeLoad_HasEmptyPrediction()
		{
			Assert.AreEqual(0, new KenjiSpell().Tahmin.Find("ab", 5).Count);
		}

		[TestMethod]
		public void Find_TypedPrefixIsLowerCased()
		{
			SozTahmini t = Create("abc", 1);

			CollectionAssert.AreEqual(new[]{ "abc" }, t.Find("AB", 5));
		}

		// ULY: a typed o, u or e also finds ö, ü or é (they take Shift to type); a typed ö, ü or é finds only itself.
		[TestMethod]
		public void Find_PlainVowel_AlsoFindsDottedOne()
		{
			SozTahmini t = Create("köz", 5, "kor", 3, "küz", 4, "kéz", 2, "kez", 1);

			CollectionAssert.AreEqual(new[]{ "köz", "kor" }, t.Find("ko", 5));
			CollectionAssert.AreEqual(new[]{ "küz" }, t.Find("ku", 5));
			CollectionAssert.AreEqual(new[]{ "kéz", "kez" }, t.Find("ke", 5));
		}

		// Words that differ only in a dot sort side by side; the search must start at the first of them.
		[TestMethod]
		public void Find_PlainVowel_FindsEveryWordOfTheSameSpelling()
		{
			CollectionAssert.AreEqual(new[]{ "kx", "kö", "ko" }, Create("ko", 1, "kö", 2, "kx", 3).Find("k", 5));
			CollectionAssert.AreEqual(new[]{ "kö", "ko" }, Create("ko", 1, "kö", 2, "kx", 3).Find("ko", 5));
			CollectionAssert.AreEqual(new[]{ "koz", "köz", "kozz", "közz" },
				Create("kaa", 9, "koz", 5, "köz", 4, "kozz", 3, "közz", 2, "kza", 1).Find("ko", 5));
		}

		// Every word of 1-3 letters from k, o, ö, z (many share a plain spelling), against a plain filter and sort.
		[TestMethod]
		public void Find_DottedLetters_AgreesWithFullSort()
		{
			string herpler = "koöz";
			List<string> sozler = new List<string>{ "" };
			for(int len = 0; len < 3; len++){
				sozler.AddRange(sozler.Where(s => s.Length == len).SelectMany(s => herpler.Select(h => s + h)).ToList());
			}
			sozler.Remove("");
			List<object> sozTekrar = new List<object>();
			for(int i = 0; i < sozler.Count; i++){ sozTekrar.Add(sozler[i]); sozTekrar.Add((i * 7) % 5); }
			SozTahmini t = Create(sozTekrar.ToArray());

			foreach(string bash in sozler.Where(s => s.Length <= 2)){
				string[] expected = sozler.Select((s, i) => new { s, c = (i * 7) % 5 }).Where(x => SozTahmini.Bashlinidu(x.s, bash))
					.OrderByDescending(x => x.c).ThenBy(x => x.s.Length).ThenBy(x => x.s, StringComparer.Ordinal)
					.Take(5).Select(x => x.s).ToArray();
				CollectionAssert.AreEqual(expected, t.Find(bash, 5), bash);
			}
		}

		[TestMethod]
		public void Find_DottedVowel_FindsOnlyItself()
		{
			SozTahmini t = Create("köz", 5, "kor", 3, "kéz", 2, "kez", 1);

			CollectionAssert.AreEqual(new[]{ "köz" }, t.Find("kö", 5));
			CollectionAssert.AreEqual(new[]{ "kéz" }, t.Find("ké", 5));
		}

		[TestMethod]
		public void Find_PlainVowel_KeepsTheCountOrderAcrossBothSpellings()
		{
			SozTahmini t = Create("ola", 2, "öla", 9, "olb", 5, "ölb", 1);

			CollectionAssert.AreEqual(new[]{ "öla", "olb", "ola", "ölb" }, t.Find("ol", 5));
			CollectionAssert.AreEqual(new[]{ "öla", "olb" }, t.Find("OL", 2));
		}

		// A KenjiSpell dictionary also builds the prediction table, in the dictionary's script.
		[TestMethod]
		public void KenjiSpell_Load_BuildsPredictionInEachScript()
		{
			string uey = FirstWordWithLength(4);
			byte[] data = Encoding.UTF8.GetBytes(uey + " 10\n");

			foreach(Uyghur.YEZIQ yeziq in new[]{ Uyghur.YEZIQ.UEY, Uyghur.YEZIQ.ULY, Uyghur.YEZIQ.USY }){
				KenjiSpell spell = NewSpell();
				spell.Load(new MemoryStream(data), yeziq);
				string soz = yeziq == Uyghur.YEZIQ.ULY ? Uyghur.UEY2ULY(uey).ToLowerInvariant()
					: yeziq == Uyghur.YEZIQ.USY ? Uyghur.UEY2USY(uey).ToLowerInvariant() : uey;

				CollectionAssert.AreEqual(new[]{ soz }, spell.Tahmin.Find(soz.Substring(0, 2), 5), yeziq.ToString());
			}
		}

		// The real dictionary: Find agrees with a plain sort of every matching word.
		[TestMethod]
		public void RealDictionary_FindAgreesWithFullSort()
		{
			List<KeyValuePair<string, long>> all = ReadRealDictionary();
			SozTahmini t = new SozTahmini(all);
			Dictionary<string, long> merged = new Dictionary<string, long>(StringComparer.Ordinal);
			foreach(var kv in all){
				string s = SozTahmini.Normalize(kv.Key);
				if(s.Length == 0) continue;
				long c; merged.TryGetValue(s, out c); merged[s] = c + kv.Value;
			}

			// Prefixes taken from the dictionary itself: every 997th word, cut to 1, 2 and 3 letters.
			for(int i = 0; i < all.Count; i += 997){
				string w = SozTahmini.Normalize(all[i].Key);
				for(int len = 1; len <= 3 && len <= w.Length; len++){
					string bash = w.Substring(0, len);
					string[] expected = merged.Where(kv => kv.Key.StartsWith(bash, StringComparison.Ordinal))
						.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key.Length).ThenBy(kv => kv.Key, StringComparer.Ordinal)
						.Take(5).Select(kv => kv.Key).ToArray();
					CollectionAssert.AreEqual(expected, t.Find(bash, 5), bash);
				}
			}
		}

		// Records time and memory for the largest range (the plan asks for these numbers).
		[TestMethod]
		public void RealDictionary_Measure()
		{
			List<KeyValuePair<string, long>> all = ReadRealDictionary();
			long before = GC.GetTotalMemory(true);
			SozTahmini t = new SozTahmini(all);
			long after = GC.GetTotalMemory(true);

			string bash = all.Select(kv => SozTahmini.Normalize(kv.Key)).Where(s => s.Length > 0)
				.GroupBy(s => s.Substring(0, 1)).OrderByDescending(g => g.Count()).First().Key;
			t.Find(bash, 5);
			Stopwatch sw = Stopwatch.StartNew();
			for(int i = 0; i < 100; i++) t.Find(bash, 5);
			sw.Stop();

			Console.WriteLine("words={0} memory={1:N0} bytes, Find(\"{2}\") avg {3:F2} ms", t.Count, after - before, bash, sw.Elapsed.TotalMilliseconds / 100);
			GC.KeepAlive(t);
		}

		// Spell suggestions with the same distance are ordered by count, also above 32767
		// (the counts used to be cut to 32767, so both orders of adding are checked).
		[DataTestMethod]
		[DataRow(true)]
		[DataRow(false)]
		public void KenjiSpell_Lookup_OrdersByFullCount(bool lowFirst)
		{
			KenjiSpell spell = new KenjiSpell();
			if(lowFirst){ spell.Add("abcx", 33000); spell.Add("abcy", 60000); }
			else { spell.Add("abcy", 60000); spell.Add("abcx", 33000); }

			List<string> result = spell.Lookup("abcz");

			Assert.IsTrue(result.IndexOf("abcy") >= 0 && result.IndexOf("abcy") < result.IndexOf("abcx"), string.Join(",", result));
		}

		[TestMethod]
		public void KenjiSpell_Add_KeepsCountsAbove32767()
		{
			KenjiSpell spell = new KenjiSpell();

			Assert.AreEqual(65535L, spell.Add("abc", 65535));
		}

		static KenjiSpell NewSpell()
		{
			string folder = Path.Combine(Path.GetTempPath(), "UyghurEditPP.Tests", Guid.NewGuid().ToString("N"));
			KenjiSpell spell = new KenjiSpell();
			// No user files: keep the test away from %AppData%.
			spell.SetFileNames(Path.Combine(folder, "i.txt"), Path.Combine(folder, "u.txt"), Path.Combine(folder, "s.txt"));
			return spell;
		}

		static string FirstWordWithLength(int len)
		{
			return ReadRealDictionary().Select(kv => kv.Key).First(s => s.Length == len);
		}

		static List<KeyValuePair<string, long>> ReadRealDictionary()
		{
			List<KeyValuePair<string, long>> list = new List<KeyValuePair<string, long>>();
			Stream s = typeof(SozTahmini).Assembly.GetManifestResourceStream("UyghurEditPP.uyghur_imla.txt");
			Assert.IsNotNull(s, "the dictionary resource is missing");
			using(StreamReader sr = new StreamReader(s, Encoding.UTF8)){
				string line;
				while((line = sr.ReadLine()) != null){
					string[] p = line.Split(null);
					long c;
					if(p.Length >= 2 && long.TryParse(p[1], out c)) list.Add(new KeyValuePair<string, long>(p[0], c));
				}
			}
			return list;
		}
	}
}
