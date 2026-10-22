using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UyghurEditPP.Tests
{
	[TestClass]
	public class TahminIshletkuchiTests
	{
		// Made-up Latin strings, not Uyghur words.
		string gFolder, gFile;

		[TestInitialize]
		public void SetUp()
		{
			gFolder = Path.Combine(Path.GetTempPath(), "TahminIshletkuchiTests_" + Path.GetRandomFileName());
			Directory.CreateDirectory(gFolder);
			gFile = Path.Combine(gFolder, "tahmin.txt");
		}

		[TestCleanup]
		public void TearDown()
		{
			Directory.Delete(gFolder, true);
		}

		static SozTahmini Dictionary(params object[] sozTekrar)
		{
			List<KeyValuePair<string, long>> list = new List<KeyValuePair<string, long>>();
			for(int i = 0; i < sozTekrar.Length; i += 2){
				list.Add(new KeyValuePair<string, long>((string)sozTekrar[i], (int)sozTekrar[i + 1]));
			}
			return new SozTahmini(list);
		}

		[TestMethod]
		public void NoFile_NothingLearned()
		{
			TahminIshletkuchi t = new TahminIshletkuchi(gFile);

			Assert.AreEqual(0, t.Find("ab", 5).Count);
		}

		[TestMethod]
		public void Qosh_MoreChosenFirst_AndKeptInTheFile()
		{
			TahminIshletkuchi t = new TahminIshletkuchi(gFile);
			t.Qosh("abc");
			t.Qosh("abd");
			t.Qosh("abd");
			t.Qosh("xyz");

			CollectionAssert.AreEqual(new[]{ "abd", "abc" }, t.Find("ab", 5));
			CollectionAssert.AreEqual(new[]{ "abd", "abc" }, new TahminIshletkuchi(gFile).Find("ab", 5), "read back from the file");
		}

		[TestMethod]
		public void Qosh_LowerCaseLikeTheDictionary()
		{
			TahminIshletkuchi t = new TahminIshletkuchi(gFile);
			t.Qosh("Abc");

			CollectionAssert.AreEqual(new[]{ "abc" }, t.Find("AB", 5));
		}

		[TestMethod]
		public void Qosh_KeepsWhatAnotherWindowLearned()
		{
			TahminIshletkuchi a = new TahminIshletkuchi(gFile);
			TahminIshletkuchi b = new TahminIshletkuchi(gFile);
			a.Qosh("abc");
			b.Qosh("abd");

			CollectionAssert.AreEquivalent(new[]{ "abc", "abd" }, new TahminIshletkuchi(gFile).Find("ab", 5));
		}

		[TestMethod]
		public void BadLines_AreSkipped()
		{
			File.WriteAllText(gFile, "abc 3\r\n\r\nabd x\r\nabe\r\nabf 1 2\r\n", new UTF8Encoding(true));

			CollectionAssert.AreEqual(new[]{ "abc" }, new TahminIshletkuchi(gFile).Find("ab", 5));
		}

		[TestMethod]
		public void Ochur_ForgetsAndDeletesTheFile()
		{
			TahminIshletkuchi t = new TahminIshletkuchi(gFile);
			t.Qosh("abc");

			t.Ochur();

			Assert.AreEqual(0, t.Find("ab", 5).Count);
			Assert.IsFalse(File.Exists(gFile));
		}

		[TestMethod]
		public void Namzatlar_LearnedWordsBeforeTheDictionary()
		{
			SozTahmini lughet = Dictionary("abc", 90, "abd", 70, "abe", 50);
			TahminIshletkuchi t = new TahminIshletkuchi(gFile);
			t.Qosh("abe");

			CollectionAssert.AreEqual(new[]{ "abe", "abc", "abd" }, TahminQaide.Namzatlar(lughet, t, "ab", 5));
			CollectionAssert.AreEqual(new[]{ "Abe", "Abc" }, TahminQaide.Namzatlar(lughet, t, "Ab", 2));
		}

		[TestMethod]
		public void Find_PlainVowel_AlsoFindsDottedOne()
		{
			TahminIshletkuchi t = new TahminIshletkuchi(gFile);
			t.Qosh("köz");
			t.Qosh("kor");

			CollectionAssert.AreEqual(new[]{ "kor", "köz" }, t.Find("ko", 5));
			CollectionAssert.AreEqual(new[]{ "köz" }, t.Find("kö", 5));
		}

		[TestMethod]
		public void Namzatlar_PlainVowel_DottedWordKeepsCapital()
		{
			SozTahmini lughet = Dictionary("köz", 90, "kor", 70);
			TahminIshletkuchi t = new TahminIshletkuchi(gFile);

			CollectionAssert.AreEqual(new[]{ "Köz", "Kor" }, TahminQaide.Namzatlar(lughet, t, "Ko", 5));
		}

		[TestMethod]
		public void Namzatlar_NotTheTypedWord()
		{
			SozTahmini lughet = Dictionary("ab", 90, "abc", 70);
			TahminIshletkuchi t = new TahminIshletkuchi(gFile);
			t.Qosh("ab");

			CollectionAssert.AreEqual(new[]{ "abc" }, TahminQaide.Namzatlar(lughet, t, "ab", 5));
		}
	}
}
