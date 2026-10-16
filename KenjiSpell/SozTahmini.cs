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
		readonly string[] gSozler;    // ordinal order, so a prefix's words are next to each other
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
			Array.Sort(gSozler, StringComparer.Ordinal);
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

			int bashi = Array.BinarySearch(gSozler, bash, StringComparer.Ordinal);
			if(bashi < 0) bashi = ~bashi;
			int ayaghi = Ayaghi(bashi, bash);

			for(int i = bashi; i < ayaghi; i++){
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

		// The first index at or after bashi whose word does not start with bash.
		int Ayaghi(int bashi, string bash)
		{
			int lo = bashi, hi = gSozler.Length;
			while(lo < hi){
				int mid = lo + (hi - lo) / 2;
				if(gSozler[mid].StartsWith(bash, StringComparison.Ordinal)) lo = mid + 1;
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
