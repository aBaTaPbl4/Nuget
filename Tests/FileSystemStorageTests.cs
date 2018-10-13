using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnitTests.Bot.NoIsolation
{
    using System.IO;
    using System.Threading;
    using global::Bot.Builder.FileSystemStorage.Microsoft.Bot.Builder;
    using NUnit.Framework;

    public class Obj1
    {
        public int Prop1 { get; set; }
        public string Prop2 { get; set; }
    }

    public class Obj2
    {
        public int Prop3 { get; set; }
        public string Prop4 { get; set; }
    }

    [TestFixture]
    public class FileSystemStorageTests
    {
        private FileSystemStorage _storage;
        private Dictionary<string, object> _dict;
        private CancellationTokenSource _tokenSource;
        private string _folder;


        [SetUp]
        public void Setup()
        {
            _folder = Guid.NewGuid().ToString();
            _storage = new FileSystemStorage(_folder);
            _dict = new Dictionary<string, object>();
            _tokenSource = new CancellationTokenSource();
        }

        [TearDown]
        public void Teardown()
        {
            Directory.Delete(_folder, true);
        }


        [Test]
        public void OverwriteFileTest()
        {
            var obj1 = new Obj1() {Prop1 = 1, Prop2 = "aaaaaaaaaaaaaaaaaaa"};
            _dict["obj"] = obj1;
            var task = _storage.WriteAsync(_dict, _tokenSource.Token);
            task.Wait();
            var fileContent = File.ReadAllText(Path.Combine(_folder, "obj"));
            const string obj1FileContent =
                @"{""$type"":""UnitTests.Bot.NoIsolation.Obj1, Tests"",""Prop1"":1,""Prop2"":""aaaaaaaaaaaaaaaaaaa""}";
            Assert.AreEqual(obj1FileContent, fileContent);

            var obj2 = new Obj2() {Prop3 = 2, Prop4 = "xxx"};
            _dict.Clear();
            _dict["obj"] = obj2;
            task = _storage.WriteAsync(_dict, _tokenSource.Token);
            task.Wait();
            fileContent = File.ReadAllText(Path.Combine(_folder, "obj"));
            const string obj2FileContent =
                @"{""$type"":""UnitTests.Bot.NoIsolation.Obj2, Tests"",""Prop3"":2,""Prop4"":""xxx""}";
            Assert.AreEqual(obj2FileContent, fileContent);
        }
    }
}
