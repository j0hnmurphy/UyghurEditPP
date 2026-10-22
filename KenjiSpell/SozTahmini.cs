/*
 * Word prediction: the dictionary words that start with what the user has typed,
 * most frequent first. Kept apart from the spell checker's tree, which cuts counts at
 * 32767 and has no way to list a prefix's words with their counts.
 */
using System;
using System.Collections.Generic;

namespace UyghurEditPP
{
	public class SozTahmini
	{
		readonly string[] gSozler;    // in the ordinal order of their Addiy form, so a prefix's words are next to each other
		readonly string[] gAddiylar;  // Addiy(gSozler[i])
		readonly long[] gTekrarlar;

		public SozTahmini(IEnumerable<KeyValuePair<string, long>> sozler)
		{
			Dictionary<string, long> birleshtur = new Dictionary<string, long>(StringComparer.Ordinal);
			foreach(KeyValuePair<string, long> kv in sozler){
				string soz = Normalize(kv.Key);
				if(soz.Length == 0) continue;
				long tekrar;
				birleshtur.TryGetValue(soz, out tekrar);
				birleshtur[soz] = tekrar + kv.Value;  // a word that converts to the same spelling adds up, as in KenjiSpell.Add
			}
			gSozler = new string[birleshtur.Count];
			birleshtur.Keys.CopyTo(gSozler, 0);
			gAddiylar = new string[gSozler.Length];
			for(int i = 0; i < gSozler.Length; i++){
				gAddiylar[i] = Addiy(gSozler[i]);
			}
			Array.Sort(gAddiylar, gSozler, StringComparer.Ordinal);
			gTekrarlar = new long[gSozler.Length];
			for(int i = 0; i < gSozler.Length; i++){
				gTekrarlar[i] = birleshtur[gSozler[i]];
			}
		}

		public int Count{
			get{ return gSozler.Length; }
		}

		// The same clean-up the spell checker applies to its words (KenjiSpell.Add).
		public static string Normalize(string soz)
		{
			return soz.Replace(Uyghur.Sozghuch, "").Trim().ToLowerInvariant();
		}

		// ULY ö, ü and é take Shift to type, so a typed o, u or e finds them too. The other
		// scripts have none of these letters, so for them nothing changes.
		static string Addiy(string soz)
		{
			return soz.Replace('ö', 'o').Replace('ü', 'u').Replace('é', 'e');
		}

		static char Addiy(char herp)
		{
			return herp == 'ö' ? 'o' : herp == 'ü' ? 'u' : herp == 'é' ? 'e' : herp;
		}

		/// <summary>
		/// Whether the normalized word soz starts with the normalized bash, where a typed o, u
		/// or e also matches ö, ü or é (a typed ö, ü or é matches only itself).
		/// </summary>
		public static bool Bashlinidu(string soz, string bash)
		{
			if(soz.Length < bash.Length) return false;
			for(int i = 0; i < bash.Length; i++){
				if(soz[i] != bash[i] && Addiy(soz[i]) != bash[i]) return false;
			}
			return true;
		}

		/// <summary>
		/// Up to n words that start with bash: more frequent first; with the same count the
		/// shorter word, then ordinal order. The last two rules are not real frequency, only a
		/// fixed tie-break (the same rule as the UyghurIME plan).
		/// </summary>
		public List<string> Find(string bash, int n)
		{
			bash = bash == null ? "" : Normalize(bash);
			if(bash.Length == 0 || n <= 0) return new List<string>();
			List<int> eng = new List<int>(n + 1);  // indexes of the best words so far, best first

			string addiy = Addiy(bash);
			int bashi = Bashi(addiy);
			int ayaghi = Ayaghi(bashi, addiy);
			bool tallash = addiy != bash;  // only a typed ö, ü or é needs a check: it finds only itself

			for(int i = bashi; i < ayaghi; i++){
				if(tallash && !Bashlinidu(gSozler[i], bash)) continue;
				int orun = eng.Count;
				while(orun > 0 && Aldida(i, eng[orun - 1])) orun--;
				if(orun < n){
					eng.Insert(orun, i);
					if(eng.Count > n) eng.RemoveAt(n);
				}
			}

			List<string> netije = new List<string>(eng.Count);
			foreach(int i in eng) netije.Add(gSozler[i]);
			return netije;
		}

		// The first index whose Addiy form is not before addiy. Array.BinarySearch would return any of
		// several equal forms (köz and koz are both koz), so it could skip the first ones.
		int Bashi(string addiy)
		{
			int lo = 0, hi = gAddiylar.Length;
			while(lo < hi){
				int mid = lo + (hi - lo) / 2;
				if(string.CompareOrdinal(gAddiylar[mid], addiy) < 0) lo = mid + 1;
				else hi = mid;
			}
			return lo;
		}

		// The first index at or after bashi whose Addiy form does not start with addiy.
		int Ayaghi(int bashi, string addiy)
		{
			int lo = bashi, hi = gAddiylar.Length;
			while(lo < hi){
				int mid = lo + (hi - lo) / 2;
				if(gAddiylar[mid].StartsWith(addiy, StringComparison.Ordinal)) lo = mid + 1;
				else hi = mid;
			}
			return lo;
		}

		bool Aldida(int a, int b)
		{
			if(gTekrarlar[a] != gTekrarlar[b]) return gTekrarlar[a] > gTekrarlar[b];
			if(gSozler[a].Length != gSozler[b].Length) return gSozler[a].Length < gSozler[b].Length;
			return string.CompareOrdinal(gSozler[a], gSozler[b]) < 0;
		}
	}
}
